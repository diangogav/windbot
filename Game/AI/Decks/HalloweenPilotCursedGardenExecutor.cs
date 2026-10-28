using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Plant Synchro pilot for the Edison format (TCG April 2010 card pool).
    //
    // Game plan:
    // - Lonefire Blossom is the engine: release a face-up Plant to Special
    //   Summon any Plant from the deck. Release priority is Dandylion (two
    //   Level 1 tokens), Botanical Girl (searches a DEF <= 1000 Plant), a
    //   token, then Lonefire itself. The deck target is Tytannial (2800 body
    //   that negates targeting effects) while none is on the field, otherwise
    //   whichever Plant completes the best Synchro with the tuners already
    //   out (Nettles + Gigaplant = 8, Twilight Rose Knight + Gigaplant = 9,
    //   Copy Plant + Gigaplant = 7, a tuner straight from the deck when a
    //   matching non-Tuner is already on board).
    // - Twilight Rose Knight's summon trigger drops a Level 4 or lower Plant
    //   from the hand: Lonefire first (it then pulls from the deck), else the
    //   body whose Level plus 3 hits a Synchro (Botanical Girl -> Level 6 and
    //   a search, Lord Poison / Mystic Tomato -> Black Rose Dragon).
    // - Foolish Burial and One for One (discard) both dump Dandylion for two
    //   tokens; One for One fetches Copy Plant, the only Level 1 monster.
    //   Botanical Girl searches the missing tuner, otherwise Dandylion.
    //   Mystic Tomato floats into another Tomato or Twilight Rose Knight.
    // - Gigaplant is Gemini-summoned once it is on the field and revives
    //   Tytannial, Lonefire or a Synchro body from the hand or GY each turn.
    // - Copy Plant copies a face-up Plant's Level only when that turns the
    //   board into a better Synchro (copying Mystic Tomato: 4 + 4 = 8 Dark
    //   End Dragon; copying Gigaplant: 6 + 3 = 9 Mist Wurm).
    // - Synchro ladder: 9 Mist Wurm, 8 Stardust / Red Dragon Archfiend / Dark
    //   End, 7 Black Rose Dragon (its wipe only fires when behind), 6 Gaia
    //   Knight / Drill Warrior / Queen of Thorns (Plant-only materials, taxes
    //   non-Plant summons), 5 Catastor / Magical Android, 4 Armory Arm.
    //   Tytannial and Synchro monsters are never spent as material.
    // - Starlight Road and Stardust protect the board from mass destruction;
    //   the 2010 trap line handles attacks and summons.
    // Armory Arm uses the pre-errata passcode required by the Edison whitelist.
    // Deck: AI_PilotCursedGarden.ydk.
    [Deck("PilotCursedGarden", "AI_PilotCursedGarden")]
    public class HalloweenPilotCursedGardenExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Plants.
            public const int LonefireBlossom = 48686504;
            public const int Dandylion = 15341821;
            public const int BotanicalGirl = 84824601;
            public const int Gigaplant = 53257892;
            public const int TwilightRoseKnight = 2986553;
            public const int CopyPlant = 66457407;
            public const int Nettles = 19505896;
            public const int LordPoison = 40320754;
            public const int MysticTomato = 83011277;
            public const int CactusFighter = 74440055;
            public const int Tytannial = 11819616;

            // Spells.
            public const int OneForOne = 2295440;
            public const int FoolishBurial = 81439173;
            public const int GoldSarcophagus = 75500286;

            // Traps.
            public const int MirrorForce = 44095762;
            public const int BottomlessTrapHole = 29401950;
            public const int DimensionalPrison = 70342110;
            public const int StarlightRoad = 58120309;

            // Extra Deck.
            public const int BlackRoseDragon = 73580471;
            public const int QueenOfThorns = 45500495;
            public const int StardustDragon = 44508094;
            public const int RedDragonArchfiend = 70902743;
            public const int AncientSacredWyvern = 25958491;
            public const int ColossalFighter = 23693634;
            public const int MagicalAndroid = 43385557;
            public const int ArmoryArm = 910003009; // PRE-ERRATA passcode (Edison whitelist).
            public const int Catastor = 26593852;
            public const int DrillWarrior = 3429238;
            public const int GaiaKnight = 97204936;
            public const int MistWurm = 27315304;
            public const int ThoughtRulerArchfiend = 70780151;
            public const int DarkEndDragon = 88643579;
        }

        // Armory Arm's strings live under the original passcode.
        private const int ArmoryArmStringOwner = 29071332;

        // Copies per main-deck Plant, for deck-remaining checks.
        private static readonly Dictionary<int, int> DeckCopies = new Dictionary<int, int>
        {
            { CardId.LonefireBlossom, 2 }, { CardId.Dandylion, 2 }, { CardId.BotanicalGirl, 3 },
            { CardId.Gigaplant, 2 }, { CardId.TwilightRoseKnight, 2 }, { CardId.CopyPlant, 2 },
            { CardId.Nettles, 1 }, { CardId.LordPoison, 2 }, { CardId.MysticTomato, 2 },
            { CardId.CactusFighter, 2 }, { CardId.Tytannial, 1 }
        };

        // Plants that may be summoned from the deck (Lonefire Blossom), in
        // tie-break order.
        private static readonly int[] DeckPlantOrder =
        {
            CardId.Tytannial, CardId.Gigaplant, CardId.Nettles, CardId.TwilightRoseKnight,
            CardId.CopyPlant, CardId.CactusFighter, CardId.LordPoison, CardId.MysticTomato,
            CardId.BotanicalGirl, CardId.Dandylion, CardId.LonefireBlossom
        };

        // Botanical Girl's search pool (Plant with 1000 or less DEF).
        private static readonly int[] BotanicalSearchPool =
        {
            CardId.Dandylion, CardId.TwilightRoseKnight, CardId.CopyPlant,
            CardId.Nettles, CardId.LordPoison, CardId.CactusFighter
        };

        // A body on the field, real or hypothetical, for Synchro planning.
        private class Body
        {
            public ClientCard Card;
            public int Level;
            public bool Tuner;
            public bool Plant;
            public bool Dark;
            public int Attack;
        }

        private class SynchroPlan
        {
            public int Level;
            public int Score;
            public ClientCard Tuner;
        }

        private SynchroPlan plannedSynchro;

        public HalloweenPilotCursedGardenExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Counters and protection first: they only fire in response.
            AddExecutor(ExecutorType.Activate, CardId.Tytannial, TytannialEffect);
            AddExecutor(ExecutorType.Activate, CardId.StarlightRoad, StarlightRoadEffect);
            AddExecutor(ExecutorType.Activate, CardId.StardustDragon, DefaultStardustDragonEffect);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);
            AddExecutor(ExecutorType.Activate, CardId.Nettles, NettlesEffect);

            // Optional triggers that come up outside the Main Phase.
            AddExecutor(ExecutorType.Activate, CardId.BotanicalGirl, BotanicalGirlEffect);
            AddExecutor(ExecutorType.Activate, CardId.MysticTomato, MysticTomatoEffect);
            AddExecutor(ExecutorType.Activate, CardId.TwilightRoseKnight, TwilightRoseKnightEffect);
            AddExecutor(ExecutorType.Activate, CardId.BlackRoseDragon, BlackRoseDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.MistWurm, MistWurmEffect);
            AddExecutor(ExecutorType.Activate, CardId.ColossalFighter);

            // Removal and disruption staples.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, _CardId.BookOfMoon, DefaultBookOfMoon);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.DimensionalPrison, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, CallOfTheHauntedEffect);
            AddExecutor(ExecutorType.Activate, CardId.GoldSarcophagus, GoldSarcophagusEffect);

            // Deck thinning and graveyard setup.
            AddExecutor(ExecutorType.Activate, CardId.FoolishBurial, FoolishBurialEffect);
            AddExecutor(ExecutorType.Activate, CardId.OneForOne, OneForOneEffect);

            // Engine starters: summons that chain into more summons.
            AddExecutor(ExecutorType.Summon, CardId.Gigaplant, GigaplantGeminiSummon);
            AddExecutor(ExecutorType.Summon, CardId.TwilightRoseKnight, TwilightRoseKnightSummon);
            AddExecutor(ExecutorType.Summon, CardId.LonefireBlossom, LonefireBlossomSummon);

            // Engine effects run before any Synchro so the board is complete.
            AddExecutor(ExecutorType.Activate, CardId.LonefireBlossom, LonefireBlossomEffect);
            AddExecutor(ExecutorType.Activate, CardId.Gigaplant, GigaplantEffect);
            AddExecutor(ExecutorType.Summon, CardId.Nettles, TunerSummon);
            AddExecutor(ExecutorType.Summon, CardId.CopyPlant, TunerSummon);
            AddExecutor(ExecutorType.Activate, CardId.CopyPlant, CopyPlantEffect);

            // Synchro ladder: only the best Level the board reaches is made.
            AddExecutor(ExecutorType.SpSummon, CardId.MistWurm, () => SynchroReady(9));
            AddExecutor(ExecutorType.SpSummon, CardId.DarkEndDragon, DarkEndDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.RedDragonArchfiend, RedDragonArchfiendSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.StardustDragon, () => SynchroReady(8));
            AddExecutor(ExecutorType.SpSummon, CardId.RedDragonArchfiend, () => SynchroReady(8));
            AddExecutor(ExecutorType.SpSummon, CardId.ColossalFighter, () => SynchroReady(8));
            AddExecutor(ExecutorType.SpSummon, CardId.ThoughtRulerArchfiend, () => SynchroReady(8));
            AddExecutor(ExecutorType.SpSummon, CardId.BlackRoseDragon, () => SynchroReady(7));
            AddExecutor(ExecutorType.SpSummon, CardId.AncientSacredWyvern, () => SynchroReady(7));
            AddExecutor(ExecutorType.SpSummon, CardId.QueenOfThorns, QueenOfThornsLockSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.GaiaKnight, () => SynchroReady(6));
            AddExecutor(ExecutorType.SpSummon, CardId.DrillWarrior, () => SynchroReady(6));
            AddExecutor(ExecutorType.SpSummon, CardId.QueenOfThorns, () => SynchroReady(6));
            AddExecutor(ExecutorType.SpSummon, CardId.Catastor, CatastorSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.MagicalAndroid, () => SynchroReady(5));
            AddExecutor(ExecutorType.SpSummon, CardId.Catastor, () => SynchroReady(5));
            AddExecutor(ExecutorType.SpSummon, CardId.ArmoryArm, () => SynchroReady(4));

            // Synchro effects.
            AddExecutor(ExecutorType.Activate, CardId.DarkEndDragon, DarkEndDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.DrillWarrior, DrillWarriorEffect);
            AddExecutor(ExecutorType.Activate, CardId.ArmoryArm, ArmoryArmEffect);
            AddExecutor(ExecutorType.Activate, CardId.ThoughtRulerArchfiend, ThoughtRulerArchfiendEffect);

            // Remaining summons: beaters, floaters, Lonefire fodder.
            AddExecutor(ExecutorType.Summon, CardId.BotanicalGirl, LonefireFodderSummon);
            AddExecutor(ExecutorType.Summon, CardId.Dandylion, LonefireFodderSummon);
            AddExecutor(ExecutorType.Summon, CardId.CactusFighter, BeaterSummon);
            AddExecutor(ExecutorType.Summon, CardId.Gigaplant, GigaplantHandSummon);
            AddExecutor(ExecutorType.Summon, CardId.Tytannial, TytannialHandSummon);
            AddExecutor(ExecutorType.Summon, CardId.LordPoison, BeaterSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.MysticTomato);
            AddExecutor(ExecutorType.MonsterSet, CardId.BotanicalGirl);
            AddExecutor(ExecutorType.MonsterSet, CardId.LordPoison);
            AddExecutor(ExecutorType.MonsterSet, CardId.Dandylion);

            // Fallback.
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        public override void OnNewTurn()
        {
            plannedSynchro = null;
            base.OnNewTurn();
        }

        // ---- Board helpers ------------------------------------------------

        private bool IsMainPhaseOfOurs()
        {
            return Duel.Player == 0 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2);
        }

        private int FreeMonsterZones()
        {
            return 5 - Bot.GetMonsterCount();
        }

        private static bool IsPlant(ClientCard card)
        {
            return card != null && card.IsMonster() && card.HasRace(CardRace.Plant);
        }

        private static bool IsToken(ClientCard card)
        {
            return card != null && card.HasType(CardType.Token);
        }

        private int RemainingInDeck(int id)
        {
            int copies;
            if (!DeckCopies.TryGetValue(id, out copies))
                return 0;
            return Bot.GetRemainingCount(id, copies);
        }

        private bool DeckHasPlantTarget()
        {
            return DeckPlantOrder.Any(id => RemainingInDeck(id) > 0);
        }

        private static Body BodyOf(ClientCard card)
        {
            return new Body
            {
                Card = card,
                Level = card.Level,
                Tuner = card.IsTuner(),
                Plant = card.HasRace(CardRace.Plant),
                Dark = card.HasAttribute(CardAttribute.Dark),
                Attack = card.Attack
            };
        }

        private static Body BodyOf(int id)
        {
            NamedCard data = NamedCard.Get(id);
            if (data == null)
                return null;
            return new Body
            {
                Level = data.Level & 0xff,
                Tuner = (data.Type & (int)CardType.Tuner) != 0,
                Plant = (data.Race & (int)CardRace.Plant) != 0,
                Dark = (data.Attribute & (int)CardAttribute.Dark) != 0,
                Attack = data.Attack
            };
        }

        private static Body TokenBody()
        {
            return new Body { Level = 1, Tuner = false, Plant = true, Dark = false, Attack = 0 };
        }

        // Face-up monsters that may be spent as Synchro material: never
        // Tytannial and never a Synchro monster.
        private List<Body> CurrentMaterialBodies(ClientCard exclude = null)
        {
            return Bot.GetMonsters()
                .Where(card => card != exclude && card.IsFaceup() && card.Level > 0
                    && !card.IsExtraCard() && !card.IsCode(CardId.Tytannial))
                .Select(BodyOf).ToList();
        }

        // Value of a Synchro Level given the tuner and the non-Tuners used,
        // or 0 when no matching Synchro is left in the Extra Deck.
        private int SynchroValue(int level, Body tuner, List<Body> mats)
        {
            bool allPlant = mats.All(m => m.Plant);
            bool darkLine = tuner.Dark && mats.All(m => m.Dark);
            switch (level)
            {
                case 9:
                    if (!Bot.HasInExtra(CardId.MistWurm))
                        return 0;
                    return Enemy.GetFieldCount() >= 2 ? 88 : 70;
                case 8:
                    if (Bot.HasInExtra(CardId.StardustDragon) || Bot.HasInExtra(CardId.RedDragonArchfiend)
                        || (darkLine && Bot.HasInExtra(CardId.DarkEndDragon)))
                        return 85;
                    return 0;
                case 7:
                    if (!Bot.HasInExtra(CardId.BlackRoseDragon))
                        return 0;
                    return IsBehindOnBoard() ? 95 : 75;
                case 6:
                    if (Bot.HasInExtra(CardId.GaiaKnight) || Bot.HasInExtra(CardId.DrillWarrior)
                        || (allPlant && Bot.HasInExtra(CardId.QueenOfThorns)))
                        return 65;
                    return 0;
                case 5:
                    return Bot.HasInExtra(CardId.Catastor) || Bot.HasInExtra(CardId.MagicalAndroid) ? 55 : 0;
                case 4:
                    return Bot.HasInExtra(CardId.ArmoryArm) ? 35 : 0;
                default:
                    return 0;
            }
        }

        // Best Synchro the bodies can make: one tuner plus any subset of
        // non-Tuners. Cheaper materials win ties.
        private SynchroPlan BestSynchro(List<Body> bodies)
        {
            SynchroPlan best = null;
            List<Body> nonTuners = bodies.Where(b => !b.Tuner && b.Level > 0).ToList();
            foreach (Body tuner in bodies.Where(b => b.Tuner && b.Level > 0))
            {
                int upper = Math.Min(4, nonTuners.Count);
                for (int k = 1; k <= upper; ++k)
                {
                    foreach (IEnumerable<Body> combo in nonTuners.GetCombinations(k))
                    {
                        List<Body> mats = combo.ToList();
                        int level = tuner.Level + mats.Sum(m => m.Level);
                        int value = SynchroValue(level, tuner, mats);
                        if (value == 0)
                            continue;
                        int score = value * 100 - (tuner.Attack + mats.Sum(m => m.Attack)) / 10;
                        if (best == null || score > best.Score)
                            best = new SynchroPlan { Level = level, Score = score, Tuner = tuner.Card };
                    }
                }
            }
            return best;
        }

        private int BestSynchroScore(List<Body> bodies)
        {
            SynchroPlan plan = BestSynchro(bodies);
            return plan == null ? 0 : plan.Score;
        }

        private bool SynchroReady(int level)
        {
            SynchroPlan plan = BestSynchro(CurrentMaterialBodies());
            if (plan == null || plan.Level != level)
                return false;
            plannedSynchro = plan;
            return true;
        }

        // Standalone worth of summoning a Plant, on the same scale as a
        // Synchro score. Lonefire from the hand or GY is worth a free deck
        // pull; from the deck it only replaces itself.
        private int BodyValue(int id, bool fromDeck)
        {
            switch (id)
            {
                case CardId.Tytannial:
                    return Bot.HasInMonstersZone(CardId.Tytannial, false, false, true) ? 4500 : 9000;
                case CardId.LonefireBlossom:
                    return !fromDeck && DeckHasPlantTarget() ? 8700 : 2000;
                case CardId.Gigaplant:
                    return 5000;
                case CardId.CactusFighter:
                    return 3000;
                case CardId.Dandylion:
                    return 2800;
                case CardId.BotanicalGirl:
                    return 2700;
                case CardId.LordPoison:
                    return 2600;
                case CardId.MysticTomato:
                    return 2500;
                case CardId.TwilightRoseKnight:
                    return 1800;
                case CardId.Nettles:
                    return 1700;
                case CardId.CopyPlant:
                    return 1000;
                default:
                    return 0;
            }
        }

        // Score for adding a Plant to the board described by bodies.
        private int SummonScore(int id, List<Body> bodies, bool fromDeck)
        {
            Body body = BodyOf(id);
            int synchro = 0;
            if (body != null && id != CardId.Tytannial)
            {
                List<Body> with = new List<Body>(bodies) { body };
                synchro = BestSynchroScore(with);
            }
            return Math.Max(BodyValue(id, fromDeck), synchro);
        }

        private int ChooseDeckPlant(List<Body> bodies, IEnumerable<int> pool)
        {
            int best = 0;
            int bestScore = int.MinValue;
            foreach (int id in pool)
            {
                if (RemainingInDeck(id) <= 0)
                    continue;
                int score = SummonScore(id, bodies, true);
                if (score > bestScore)
                {
                    best = id;
                    bestScore = score;
                }
            }
            return best;
        }

        private ClientCard ChooseBodyCard(IEnumerable<ClientCard> cards, List<Body> bodies)
        {
            return cards.OrderByDescending(card => SummonScore(card.Id, bodies, card.Location == CardLocation.Deck))
                .FirstOrDefault();
        }

        // How readily a face-up Plant is released or destroyed (higher first).
        private int ReleaseScore(ClientCard card)
        {
            if (card.IsCode(CardId.Dandylion))
                return FreeMonsterZones() >= 1 ? 100 : 60;
            if (card.IsCode(CardId.BotanicalGirl))
                return 90;
            if (IsToken(card))
                return 80;
            if (card.IsCode(CardId.LonefireBlossom))
                return 70;
            if (card.IsCode(CardId.MysticTomato))
                return 55;
            if (card.IsCode(CardId.LordPoison))
                return 50;
            if (card.IsCode(CardId.CactusFighter))
                return 45;
            if (card.IsCode(CardId.CopyPlant))
                return 40;
            if (card.IsCode(CardId.Nettles))
                return 35;
            if (card.IsCode(CardId.TwilightRoseKnight))
                return 30;
            if (card.IsCode(CardId.Gigaplant))
                return 20;
            if (card.IsCode(CardId.Tytannial))
                return 0;
            return card.IsExtraCard() ? 5 : 25;
        }

        private bool IsBehindOnBoard()
        {
            int ours = Bot.GetFieldCount();
            int theirs = Enemy.GetFieldCount();
            return theirs >= ours + 2 || (Util.GetBestPower(Enemy) > 2500 && theirs >= ours);
        }

        // ---- Summons ------------------------------------------------------

        // Twilight Rose Knight first when the hand holds a Plant it can drop.
        private bool TwilightRoseKnightSummon()
        {
            if (FreeMonsterZones() < 2)
                return false;
            return Bot.Hand.Any(card => card != Card && IsPlant(card) && card.Level <= 4 && !card.IsTuner());
        }

        private bool LonefireBlossomSummon()
        {
            return DeckHasPlantTarget();
        }

        // Gemini summon a Gigaplant already on the field when it has a body
        // to bring back.
        private bool GigaplantGeminiSummon()
        {
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            if (FreeMonsterZones() < 1)
                return false;
            return Bot.Hand.Concat(Bot.Graveyard).Any(card => card != Card && card.IsMonster()
                && (card.HasRace(CardRace.Plant) || card.HasRace(CardRace.Insect)) && card.IsCanRevive());
        }

        // Gigaplant from the hand needs one tribute; tokens cannot be used.
        private bool GigaplantHandSummon()
        {
            if (Card.Location != CardLocation.Hand)
                return false;
            return Bot.GetMonsters().Any(card => !IsToken(card) && !card.IsExtraCard()
                && card.Level <= 4 && card.GetDefensePower() < Card.Attack
                && !card.IsCode(CardId.LonefireBlossom, CardId.TwilightRoseKnight));
        }

        private bool TytannialHandSummon()
        {
            return Bot.GetMonsters().Count(card => !IsToken(card) && !card.IsExtraCard()
                && card.Level <= 4 && card.GetDefensePower() < Card.Attack) >= 2;
        }

        // A tuner hits the field from the hand only when it completes a Synchro.
        private bool TunerSummon()
        {
            List<Body> bodies = CurrentMaterialBodies();
            bodies.Add(BodyOf(Card));
            if (Card.IsCode(CardId.CopyPlant))
            {
                foreach (ClientCard plant in Bot.GetMonsters().Concat(Enemy.GetMonsters())
                    .Where(card => card.IsFaceup() && IsPlant(card) && card.Level > 0))
                {
                    List<Body> copied = CurrentMaterialBodies();
                    Body copy = BodyOf(Card);
                    copy.Level = plant.Level;
                    copied.Add(copy);
                    if (BestSynchroScore(copied) > 0)
                        return true;
                }
            }
            return BestSynchroScore(bodies) > 0;
        }

        // Botanical Girl / Dandylion: Lonefire fodder when a Lonefire is on
        // the field with its effect still pending.
        private bool LonefireFodderSummon()
        {
            if (Bot.HasInMonstersZone(CardId.LonefireBlossom, false, false, true))
                return true;
            return Card.IsCode(CardId.BotanicalGirl) && Enemy.GetMonsterCount() == 0;
        }

        private bool BeaterSummon()
        {
            return !Util.IsAllEnemyBetter() || Enemy.GetMonsterCount() == 0;
        }

        // ---- Engine effects -------------------------------------------------

        // Lonefire Blossom: release the cheapest Plant, pull the Plant that
        // gives the best board from the deck.
        private bool LonefireBlossomEffect()
        {
            if (Card.Location != CardLocation.MonsterZone || !IsMainPhaseOfOurs())
                return false;
            if (!DeckHasPlantTarget())
                return false;
            ClientCard cost = Bot.GetMonsters()
                .Where(card => card.IsFaceup() && IsPlant(card))
                .OrderByDescending(ReleaseScore).FirstOrDefault();
            if (cost == null || cost.IsCode(CardId.Tytannial))
                return false;

            List<Body> after = CurrentMaterialBodies(cost);
            if (cost.IsCode(CardId.Dandylion) && FreeMonsterZones() >= 1)
            {
                after.Add(TokenBody());
                after.Add(TokenBody());
            }
            int target = ChooseDeckPlant(after, DeckPlantOrder);
            if (target == 0)
                return false;
            AI.SelectCard(cost);
            AI.SelectNextCard(target);
            return true;
        }

        // Gigaplant: Tytannial first, then Lonefire (a fresh deck pull), then
        // whatever completes a Synchro.
        private bool GigaplantEffect()
        {
            if (!IsMainPhaseOfOurs() || FreeMonsterZones() < 1)
                return false;
            List<ClientCard> pool = Bot.Hand.Concat(Bot.Graveyard)
                .Where(card => card.IsMonster() && card.IsCanRevive()
                    && (card.HasRace(CardRace.Plant) || card.HasRace(CardRace.Insect)))
                .ToList();
            ClientCard target = ChooseBodyCard(pool, CurrentMaterialBodies());
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // Copy Plant: copy the Level that upgrades the best Synchro.
        private bool CopyPlantEffect()
        {
            if (!IsMainPhaseOfOurs())
                return false;
            int baseScore = BestSynchroScore(CurrentMaterialBodies());
            ClientCard bestTarget = null;
            int bestScore = baseScore;
            foreach (ClientCard plant in Bot.GetMonsters().Concat(Enemy.GetMonsters())
                .Where(card => card != Card && card.IsFaceup() && IsPlant(card) && card.Level > 0 && card.Level != Card.Level))
            {
                List<Body> bodies = CurrentMaterialBodies();
                Body copy = bodies.FirstOrDefault(b => b.Card == Card);
                if (copy == null)
                    continue;
                copy.Level = plant.Level;
                int score = BestSynchroScore(bodies);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = plant;
                }
            }
            if (bestTarget == null)
                return false;
            AI.SelectCard(bestTarget);
            return true;
        }

        // Twilight Rose Knight's summon trigger: the hand Plant that gives
        // the best board.
        private bool TwilightRoseKnightEffect()
        {
            List<ClientCard> pool = Bot.Hand
                .Where(card => IsPlant(card) && card.Level <= 4).ToList();
            ClientCard target = ChooseBodyCard(pool, CurrentMaterialBodies());
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // Botanical Girl's search: the missing tuner, otherwise Dandylion.
        private bool BotanicalGirlEffect()
        {
            AI.SelectCard(GetBotanicalSearchPriority());
            return true;
        }

        private int[] GetBotanicalSearchPriority()
        {
            List<int> priority = new List<int>();
            bool tunerInHand = Bot.Hand.Any(card => card.IsTuner());
            bool lonefireReady = Bot.HasInHandOrHasInMonstersZone(CardId.LonefireBlossom);
            if (!tunerInHand)
            {
                if (Bot.Hand.Any(card => IsPlant(card) && card.Level <= 4 && !card.IsTuner()))
                    priority.Add(CardId.TwilightRoseKnight);
                priority.Add(CardId.Nettles);
            }
            if (lonefireReady)
                priority.Add(CardId.Dandylion);
            priority.AddRange(new[] { CardId.TwilightRoseKnight, CardId.Dandylion, CardId.CopyPlant,
                CardId.Nettles, CardId.CactusFighter, CardId.LordPoison });
            return priority.Where(id => RemainingInDeck(id) > 0).Distinct()
                .Concat(BotanicalSearchPool).Distinct().ToArray();
        }

        // Mystic Tomato: another Tomato on the opponent's turn, a tuner for
        // a Main Phase 2 Synchro on ours. Never Copy Plant in Attack Position.
        private bool MysticTomatoEffect()
        {
            int pick = 0;
            if (Duel.Player == 0 && RemainingInDeck(CardId.TwilightRoseKnight) > 0
                && SummonScore(CardId.TwilightRoseKnight, CurrentMaterialBodies(), true) > BodyValue(CardId.TwilightRoseKnight, true))
                pick = CardId.TwilightRoseKnight;
            else if (RemainingInDeck(CardId.MysticTomato) > 0)
                pick = CardId.MysticTomato;
            else if (RemainingInDeck(CardId.TwilightRoseKnight) > 0)
                pick = CardId.TwilightRoseKnight;
            if (pick == 0)
                return false;
            AI.SelectCard(pick);
            return true;
        }

        // Nettles: let a cheaper Plant (token, Dandylion, Botanical Girl) be
        // destroyed in its place.
        private bool NettlesEffect()
        {
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            ClientCard sub = Bot.GetMonsters()
                .Where(card => card != Card && card.IsFaceup() && IsPlant(card) && ReleaseScore(card) >= 80)
                .OrderByDescending(ReleaseScore).FirstOrDefault();
            if (sub == null)
                return false;
            AI.SelectCard(sub);
            return true;
        }

        // Tytannial: negate a targeting effect, releasing the cheapest Plant.
        private bool TytannialEffect()
        {
            if (Duel.LastChainPlayer != 1)
                return false;
            ClientCard cost = Bot.GetMonsters()
                .Where(card => card.IsFaceup() && IsPlant(card))
                .OrderByDescending(card => card == Card ? -1 : ReleaseScore(card)).FirstOrDefault();
            if (cost == null)
                return false;
            AI.SelectCard(cost);
            return true;
        }

        // ---- Spells -------------------------------------------------------

        private bool FoolishBurialEffect()
        {
            if (!IsMainPhaseOfOurs())
                return false;
            List<int> priority = new List<int>();
            if (FreeMonsterZones() >= 2)
                priority.Add(CardId.Dandylion);
            bool reviver = Bot.HasInMonstersZone(CardId.Gigaplant, false, false, true)
                || Bot.HasInHandOrInSpellZone(_CardId.CallOfTheHaunted);
            if (reviver)
                priority.Add(CardId.Tytannial);
            priority.AddRange(new[] { CardId.Gigaplant, CardId.Dandylion, CardId.LonefireBlossom, CardId.Tytannial,
                CardId.LordPoison, CardId.CactusFighter });
            AI.SelectCard(priority.Where(id => RemainingInDeck(id) > 0).Distinct().ToArray());
            return priority.Any(id => RemainingInDeck(id) > 0);
        }

        // One for One: discard a monster (Dandylion for tokens) to fetch
        // Copy Plant, when the result is a Synchro or Lonefire fodder.
        private bool OneForOneEffect()
        {
            if (!IsMainPhaseOfOurs() || FreeMonsterZones() < 1 || RemainingInDeck(CardId.CopyPlant) <= 0)
                return false;
            List<ClientCard> discards = Bot.Hand.Where(card => card != Card && card.IsMonster()).ToList();
            if (discards.Count == 0)
                return false;
            ClientCard discard = discards.FirstOrDefault(card => card.IsCode(CardId.Dandylion))
                ?? discards.FirstOrDefault(card => card.IsCode(CardId.Tytannial) && Bot.HasInMonstersZone(CardId.Gigaplant, false, false, true))
                ?? discards.Where(card => !card.IsCode(CardId.LonefireBlossom, CardId.TwilightRoseKnight, CardId.Tytannial))
                    .OrderBy(card => Bot.Hand.Count(other => other.IsCode(card.Id)) > 1 ? 0 : 1)
                    .ThenBy(card => card.Attack).FirstOrDefault();
            if (discard == null)
                return false;

            List<Body> bodies = CurrentMaterialBodies();
            bool tokens = discard.IsCode(CardId.Dandylion) && FreeMonsterZones() >= 3;
            if (tokens)
            {
                bodies.Add(TokenBody());
                bodies.Add(TokenBody());
            }
            bool worth = false;
            foreach (int level in bodies.Where(b => b.Plant).Select(b => b.Level).Concat(new[] { 1 }).Distinct())
            {
                List<Body> with = new List<Body>(bodies);
                Body copy = BodyOf(CardId.CopyPlant);
                copy.Level = level;
                with.Add(copy);
                if (BestSynchroScore(with) > 0)
                    worth = true;
            }
            bool fodder = Bot.HasInMonstersZone(CardId.LonefireBlossom, false, false, true) || tokens;
            if (!worth && !fodder)
                return false;
            AI.SelectCard(discard);
            AI.SelectNextCard(CardId.CopyPlant);
            return true;
        }

        private bool GoldSarcophagusEffect()
        {
            if (!IsMainPhaseOfOurs())
                return false;
            List<int> priority = new List<int>();
            if (!Bot.HasInHand(CardId.LonefireBlossom))
                priority.Add(CardId.LonefireBlossom);
            priority.AddRange(new[] { _CardId.HeavyStorm, CardId.MirrorForce, _CardId.TorrentialTribute,
                CardId.LonefireBlossom, CardId.StarlightRoad, _CardId.SolemnJudgment, CardId.Tytannial });
            AI.SelectCard(priority);
            return true;
        }

        // Call of the Haunted: on our Main Phase revive Tytannial or a big
        // Synchro; otherwise the default "revive when behind".
        private bool CallOfTheHauntedEffect()
        {
            if (FreeMonsterZones() < 1)
                return false;
            if (IsMainPhaseOfOurs())
            {
                ClientCard best = Bot.Graveyard
                    .Where(card => card.IsMonster() && card.IsCanRevive() && card.Attack >= 2400)
                    .OrderByDescending(card => card.IsCode(CardId.Tytannial) ? 1 : 0)
                    .ThenByDescending(card => card.Attack).FirstOrDefault();
                if (best != null)
                {
                    AI.SelectCard(best);
                    return true;
                }
                return false;
            }
            return DefaultCallOfTheHaunted();
        }

        // The engine gates activation (2 or more of our cards would be
        // destroyed by a negatable effect).
        private bool StarlightRoadEffect()
        {
            return Duel.LastChainPlayer == 1 || Util.IsChainTarget(Card);
        }

        // ---- Synchro choices ----------------------------------------------

        private bool DarkEndDragonSummon()
        {
            return SynchroReady(8) && Enemy.GetMonsters().Any(card => !card.IsShouldNotBeTarget() && card.Attack >= 1800);
        }

        // Its End Phase sweep destroys our monsters that did not attack, so
        // only against defensive walls or onto a thin board.
        private bool RedDragonArchfiendSummon()
        {
            if (!SynchroReady(8))
                return false;
            return Enemy.GetMonsters().Any(card => card.IsDefense()) || Bot.GetMonsterCount() <= 3;
        }

        // Queen of Thorns taxes non-Plant summons: a lock when the opponent
        // has to rebuild.
        private bool QueenOfThornsLockSummon()
        {
            return SynchroReady(6) && Enemy.GetMonsterCount() <= 1
                && !Bot.HasInMonstersZone(CardId.QueenOfThorns, false, false, true);
        }

        private bool CatastorSummon()
        {
            return SynchroReady(5) && Enemy.GetMonsters().Any(card => card.IsFaceup() && !card.HasAttribute(CardAttribute.Dark));
        }

        // ---- Synchro effects ----------------------------------------------

        // Black Rose Dragon: wipe only when behind; flip a defender to
        // attack with 0 ATK when we can run it over.
        private bool BlackRoseDragonEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.BlackRoseDragon, 0))
                return IsBehindOnBoard();
            if (ActivateDescription == Util.GetStringId(CardId.BlackRoseDragon, 1))
            {
                if (Duel.Phase != DuelPhase.Main1)
                    return false;
                ClientCard target = Enemy.GetMonsters()
                    .Where(card => card.IsDefense() && !card.IsShouldNotBeTarget())
                    .OrderByDescending(card => card.GetDefensePower()).FirstOrDefault();
                ClientCard cost = Bot.Graveyard.Where(IsPlant)
                    .OrderBy(card => BodyValue(card.Id, false)).FirstOrDefault();
                if (target == null || cost == null)
                    return false;
                AI.SelectCard(cost);
                AI.SelectNextCard(target);
                return true;
            }
            return false;
        }

        private bool MistWurmEffect()
        {
            List<ClientCard> targets = Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Where(card => !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.IsMonster() ? card.Attack + 1000 : 500)
                .Take(3).ToList();
            if (targets.Count == 0)
                return false;
            AI.SelectCard(targets);
            return true;
        }

        private bool DarkEndDragonEffect()
        {
            if (Card.Attack < 2100)
                return false;
            ClientCard target = Util.GetBestEnemyMonster(false, true);
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // Drill Warrior: halve ATK and attack directly when the opponent's
        // board walls it.
        private bool DrillWarriorEffect()
        {
            if (ActivateDescription != Util.GetStringId(CardId.DrillWarrior, 0))
                return false;
            return Duel.Phase == DuelPhase.Main1 && Util.GetBestPower(Enemy) >= Card.Attack;
        }

        // Armory Arm: equip our best attacker in Main Phase 1 when the +1000
        // turns a losing battle into a win.
        private bool ArmoryArmEffect()
        {
            if (ActivateDescription != Util.GetStringId(ArmoryArmStringOwner, 0))
                return false;
            if (Duel.Phase != DuelPhase.Main1 || Card.Location != CardLocation.MonsterZone)
                return false;
            ClientCard target = Bot.GetMonsters()
                .Where(card => card != Card && card.IsFaceup() && card.IsAttack())
                .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (target == null || Enemy.GetMonsterCount() == 0)
                return false;
            if (target.Attack + 1000 <= Util.GetBestPower(Enemy))
                return false;
            AI.SelectCard(target);
            return true;
        }

        private bool ThoughtRulerArchfiendEffect()
        {
            if (Bot.LifePoints <= 1000 || Duel.LastChainPlayer != 1)
                return false;
            return Util.IsChainTarget(Card);
        }

        // ---- Selection overrides ------------------------------------------

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (AI.HaveSelectedCards())
                return base.OnSelectCard(cards, min, max, hint, cancelable);

            // Release costs (Lonefire Blossom, Tytannial): cheapest Plant.
            if (hint == HintMsg.Release && cards.All(card => card.Controller == 0 && card.Location == CardLocation.MonsterZone))
                return cards.OrderByDescending(ReleaseScore).Take(min).ToList();

            if (hint == HintMsg.DestroyReplace && cards.All(card => card.Controller == 0))
                return cards.OrderByDescending(ReleaseScore).Take(min).ToList();

            // Forced or unqueued summons (Lord Poison, Gigaplant, Lonefire).
            if (hint == HintMsg.SpSummon && min == 1 && cards.All(card => card.Controller == 0))
            {
                ClientCard pick = ChooseBodyCard(cards, CurrentMaterialBodies());
                if (pick != null)
                    return new[] { pick };
            }

            // Botanical Girl's search when no pick was queued.
            if (hint == HintMsg.AddToHand && cards.All(card => card.Location == CardLocation.Deck))
            {
                foreach (int id in GetBotanicalSearchPriority())
                {
                    ClientCard pick = cards.FirstOrDefault(card => card.IsCode(id));
                    if (pick != null)
                        return new[] { pick };
                }
            }

            // Banish costs from our GY: the least useful Plant.
            if (hint == HintMsg.Remove && cards.All(card => card.Controller == 0 && card.Location == CardLocation.Grave))
                return cards.OrderBy(card => BodyValue(card.Id, false)).Take(min).ToList();

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        // Synchro materials: the planned tuner, then the cheapest non-Tuners
        // that hit the Level. Tytannial and Synchro monsters go last;
        // Botanical Girl and Dandylion are cheap because they replace
        // themselves.
        public override IList<ClientCard> OnSelectSynchroMaterial(IList<ClientCard> cards, int sum, int min, int max)
        {
            if (sum <= 0)
            {
                if (plannedSynchro != null && plannedSynchro.Tuner != null && cards.Contains(plannedSynchro.Tuner))
                    return new[] { plannedSynchro.Tuner };
                return null;
            }
            List<ClientCard> best = null;
            int bestCost = int.MaxValue;
            int upper = Math.Min(max, cards.Count);
            for (int k = Math.Max(1, min); k <= upper; ++k)
            {
                foreach (IEnumerable<ClientCard> combo in cards.GetCombinations(k))
                {
                    List<ClientCard> list = combo.ToList();
                    int s1 = list.Sum(card => card.OpParam1);
                    int s2 = list.Sum(card => card.OpParam2 != 0 ? card.OpParam2 : card.OpParam1);
                    if (s1 != sum && s2 != sum)
                        continue;
                    int cost = list.Sum(MaterialCost);
                    if (cost < bestCost)
                    {
                        best = list;
                        bestCost = cost;
                    }
                }
            }
            return best;
        }

        private int MaterialCost(ClientCard card)
        {
            if (card.IsCode(CardId.Tytannial) || card.IsExtraCard())
                return 100000;
            if (card.IsCode(CardId.BotanicalGirl, CardId.Dandylion))
                return card.Attack - 800;
            if (card.IsCode(CardId.Gigaplant))
                return card.Attack + 500;
            return card.Attack;
        }
    }
}
