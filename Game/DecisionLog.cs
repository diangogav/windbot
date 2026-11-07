using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using YGOSharp.Network;
using YGOSharp.Network.Enums;

namespace WindBot.Game
{
    /// <summary>
    /// Writes one JSON line per bot decision to &lt;DecisionLogDir&gt;/&lt;name&gt;.jsonl.
    /// Any failure disables the log for this bot instead of breaking the duel.
    /// </summary>
    public class DecisionLog
    {
        private static readonly Regex NamePattern = new Regex("^[A-Za-z0-9._-]{1,64}$");

        private readonly string _path;
        private readonly string _botName;
        private readonly string _executorName;
        private readonly Duel _duel;
        private readonly YGOClient _connection;

        private StreamWriter _writer;
        private bool _disabled;
        private bool _duelOpen;
        private int _duelCount;
        private int _seq;

        private bool _capturing;
        private string _prompt;
        private string _header;
        private string _state;
        private readonly List<string> _responses = new List<string>();

        public static DecisionLog Create(string directory, string name, string botName, string executorName, Duel duel, YGOClient connection)
        {
            if (string.IsNullOrEmpty(directory) || name == null)
                return null;
            if (!NamePattern.IsMatch(name))
            {
                Logger.WriteErrorLine("Decision log disabled: invalid name '" + name + "'.", null);
                return null;
            }
            return new DecisionLog(Path.Combine(directory, name + ".jsonl"), botName, executorName, duel, connection);
        }

        private DecisionLog(string path, string botName, string executorName, Duel duel, YGOClient connection)
        {
            _path = path;
            _botName = botName;
            _executorName = executorName;
            _duel = duel;
            _connection = connection;
            _connection.PacketSent += OnPacketSent;
            _connection.Disconnected += OnDisconnected;
        }

        public void OnDuelStart()
        {
            if (_disabled)
                return;
            try
            {
                EnsureDuelStarted();
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
        }

        public void OnDuelEnd(int localResult)
        {
            if (_disabled)
                return;
            try
            {
                EnsureDuelStarted();
                // A draw arrives as 2 but becomes -1 after local player conversion when going second.
                string result = localResult == 0 ? "win" : localResult == 1 ? "lose" : "draw";
                WriteLine("{\"type\":\"duel_end\",\"duel\":" + Int(_duelCount) + ",\"result\":" + Str(result) + "}");
                _duelOpen = false;
                CloseWriter();
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
        }

        public void BeginMessage(string prompt)
        {
            if (_disabled)
                return;
            try
            {
                _prompt = prompt;
                _responses.Clear();
                _header = null;
                _state = null;
                _capturing = true;
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
        }

        public void EndMessage(BinaryReader packet, int promptOffset)
        {
            if (_disabled || !_capturing)
                return;
            _capturing = false;
            if (_responses.Count == 0)
                return;
            try
            {
                EnsureDuelStarted();
                _seq++;
                byte[] message = ((MemoryStream)packet.BaseStream).ToArray();
                StringBuilder line = new StringBuilder(_state.Length + message.Length * 2 + 256);
                line.Append("{\"type\":\"decision\",\"duel\":").Append(Int(_duelCount));
                line.Append(",\"seq\":").Append(Int(_seq));
                line.Append(_header);
                line.Append(",\"prompt\":").Append(Str(_prompt));
                line.Append(",\"promptBytes\":").Append(Str(Convert.ToBase64String(message, promptOffset, message.Length - promptOffset)));
                line.Append(",\"responses\":[");
                for (int i = 0; i < _responses.Count; ++i)
                {
                    if (i > 0)
                        line.Append(',');
                    line.Append(_responses[i]);
                }
                line.Append("],\"state\":").Append(_state);
                line.Append('}');
                WriteLine(line.ToString());
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
            finally
            {
                _responses.Clear();
                _header = null;
                _state = null;
            }
        }

        private void OnPacketSent(byte[] packet)
        {
            if (_disabled || !_capturing || packet.Length == 0)
                return;
            try
            {
                CtosMessage id = (CtosMessage)packet[0];
                if (id != CtosMessage.Response && id != CtosMessage.HandResult && id != CtosMessage.TpResult)
                    return;
                // The state is captured at the first answer only, so messages that ask
                // nothing cost no snapshot; the handler may already have revealed candidates.
                if (_state == null)
                    TakeSnapshot();
                _responses.Add("{\"ctos\":" + Str(id.ToString()) + ",\"bytes\":"
                    + Str(Convert.ToBase64String(packet, 1, packet.Length - 1)) + "}");
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
        }

        private void OnDisconnected(Exception error)
        {
            try
            {
                CloseWriter();
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
        }

        private void EnsureDuelStarted()
        {
            if (_duelOpen)
                return;
            _duelOpen = true;
            _duelCount++;
            _seq = 0;
            WriteLine("{\"type\":\"duel_start\",\"duel\":" + Int(_duelCount)
                + ",\"bot\":" + Str(_botName)
                + ",\"deck\":" + Str(_executorName)
                + ",\"time\":" + Str(DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
                + "}");
        }

        private void TakeSnapshot()
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.Append(",\"turn\":").Append(Int(_duel.Turn));
            sb.Append(",\"turnPlayer\":").Append(Int(_duel.Player));
            sb.Append(",\"phase\":").Append(Str(_duel.Phase.ToString()));
            sb.Append(",\"lp\":[").Append(Int(_duel.Fields[0].LifePoints)).Append(',').Append(Int(_duel.Fields[1].LifePoints)).Append(']');
            _header = sb.ToString();
            sb.Clear();
            sb.Append("{\"bot\":");
            AppendSide(sb, _duel.Fields[0]);
            sb.Append(",\"opponent\":");
            AppendSide(sb, _duel.Fields[1]);
            sb.Append(",\"chain\":[");
            for (int i = 0; i < CountOf(_duel.CurrentChain); ++i)
            {
                ClientCard card = _duel.CurrentChain[i];
                if (i > 0)
                    sb.Append(',');
                sb.Append("{\"id\":").Append(Int(card.Id)).Append(",\"controller\":").Append(Int(card.Controller)).Append('}');
            }
            sb.Append("]}");
            _state = sb.ToString();
        }

        private static void AppendSide(StringBuilder sb, ClientField field)
        {
            sb.Append("{\"hand\":");
            AppendIds(sb, field.Hand);
            sb.Append(",\"handCount\":").Append(Int(CountOf(field.Hand)));
            sb.Append(",\"monsters\":");
            AppendZone(sb, field.MonsterZone);
            sb.Append(",\"spells\":");
            AppendZone(sb, field.SpellZone);
            sb.Append(",\"graveyard\":");
            AppendIds(sb, field.Graveyard);
            sb.Append(",\"banished\":");
            AppendIds(sb, field.Banished);
            sb.Append(",\"deckCount\":").Append(Int(CountOf(field.Deck)));
            sb.Append(",\"extraCount\":").Append(Int(CountOf(field.ExtraDeck)));
            sb.Append('}');
        }

        private static int CountOf<T>(ICollection<T> cards)
        {
            return cards == null ? 0 : cards.Count;
        }

        // Zones and lists are created when a duel starts, so a prompt that arrives
        // before that (rock-paper-scissors, turn order) sees them as empty.
        private static void AppendIds(StringBuilder sb, IList<ClientCard> cards)
        {
            sb.Append('[');
            for (int i = 0; i < CountOf(cards); ++i)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(Int(cards[i] == null ? 0 : cards[i].Id));
            }
            sb.Append(']');
        }

        private static void AppendZone(StringBuilder sb, ClientCard[] zone)
        {
            sb.Append('[');
            for (int i = 0; zone != null && i < zone.Length; ++i)
            {
                if (i > 0)
                    sb.Append(',');
                ClientCard card = zone[i];
                if (card == null)
                {
                    sb.Append("null");
                    continue;
                }
                sb.Append("{\"id\":").Append(Int(card.Id));
                sb.Append(",\"position\":").Append(Int(card.Position));
                sb.Append(",\"sequence\":").Append(Int(card.Sequence));
                sb.Append('}');
            }
            sb.Append(']');
        }

        private void WriteLine(string line)
        {
            if (_writer == null)
            {
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                FileStream stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
                _writer = new StreamWriter(stream, new UTF8Encoding(false));
            }
            _writer.Write(line);
            _writer.Write('\n');
            _writer.Flush();
        }

        private void CloseWriter()
        {
            if (_writer == null)
                return;
            StreamWriter writer = _writer;
            _writer = null;
            writer.Dispose();
        }

        private void Disable(Exception ex)
        {
            if (_disabled)
                return;
            _disabled = true;
            _capturing = false;
            _responses.Clear();
            _connection.PacketSent -= OnPacketSent;
            _connection.Disconnected -= OnDisconnected;
            try
            {
                CloseWriter();
            }
            catch (Exception)
            {
                _writer = null;
            }
            Logger.WriteErrorLine("Decision log disabled for " + _path, ex);
        }

        private static string Int(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Str(string value)
        {
            if (value == null)
                return "null";
            StringBuilder sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c == '\u2028' || c == '\u2029')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
