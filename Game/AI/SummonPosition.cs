using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI
{
    /// <summary>
    /// Opt-in position advice for Special Summons: a monster that cannot attack anything
    /// profitably this turn, and is weaker than the opponent's best face-up monster, is
    /// safer in defence than left in attack for the opponent's next turn.
    /// </summary>
    public static class SummonPosition
    {
        public static CardPosition DefenceIfExposed(Duel duel, int cardId, IList<CardPosition> positions)
        {
            if (!positions.Contains(CardPosition.FaceUpDefence) || !positions.Contains(CardPosition.FaceUpAttack))
                return 0;
            NamedCard data = NamedCard.Get(cardId);
            if (data == null)
                return 0;

            List<ClientCard> enemies = duel.Fields[1].GetMonsters();
            List<ClientCard> faceUp = enemies.Where(card => card.IsFaceup()).ToList();
            if (faceUp.Count == 0 || data.Attack >= faceUp.Max(card => card.Attack))
                return 0;

            bool canStillAttack = duel.Player == 0 && duel.Turn > 1 && duel.Phase < DuelPhase.Main2;
            if (canStillAttack)
            {
                if (enemies.Count == 0)
                    return 0;
                bool beatsSomething = faceUp.Any(card => card.IsAttack() ? card.Attack < data.Attack : card.Defense < data.Attack);
                if (beatsSomething)
                    return 0;
            }
            return CardPosition.FaceUpDefence;
        }
    }
}
