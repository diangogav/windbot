using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    [Deck("Blitzclique", "AI_Blitzclique")]
    public class BlitzcliqueExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int Whisker = 85523502;
            public const int Crackle = 58916810;
            public const int Surge = 22912101;
            public const int Emi = 11895663;
            public const int Grain = 84401954;
            public const int Urania = 18738846;
            public const int ThunderKing = 48770333;
            public const int VortexTrooper = 7736719;
            public const int LinkApple = 7925734;
            // _CardId.GhostOgreAndSnowRabbit = 59438930;

            public const int Coulomb = 37654623;
            public const int Breakaway = 64049762;
            public const int Steppleader = 433377;
            // _CardId.MysticalSpaceTyphoon = 5318639;
            public const int Alternator = 59921227;
            public const int ReturnStroke = 23526128;

            public const int Utopia = 84013237;
            public const int UtopiaLightning = 56832966;

            public const int MechBeastToken = 37654624;
        }

        private const int HintFieldFirst = 575;
        // System string the scripts use to offer an effect together with the card's activation.
        private const int AskUseEffectOnActivation = 94;

        private bool surgeHandUsed;
        private bool grainHandUsed;
        private bool emiHandUsed;
        private bool crackleHandUsed;
        private bool whiskerHandUsed;
        private bool coulombSearchUsed;
        private bool steppleaderSummonUsed;
        private bool breakawayUsed;
        private bool alternatorGraveUsed;
        private bool extraDeckLocked;
        // Whisker's hand effect needs a card destroyed by a Blitzclique effect this turn.
        private bool blitzcliqueDestroyedThisTurn;

        private bool breakawayWantsDestroy;
        private bool alternatorKeepsEmi;
        private bool uraniaProcedurePending;
        private int uraniaRevealRemaining;

        public BlitzcliqueExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.GhostOgreAndSnowRabbit, DefaultGhostOgreAndSnowRabbit);
            AddExecutor(ExecutorType.Activate, CardId.ReturnStroke, ReturnStrokeNegate);
            AddExecutor(ExecutorType.Activate, CardId.Whisker, WhiskerNegate);
            AddExecutor(ExecutorType.Activate, CardId.Crackle, CrackleEffect);
            AddExecutor(ExecutorType.Activate, CardId.Emi, EmiEffect);
            AddExecutor(ExecutorType.Activate, CardId.Surge, FieldTrigger);
            AddExecutor(ExecutorType.Activate, CardId.Grain, FieldTrigger);
            AddExecutor(ExecutorType.Activate, CardId.Coulomb, GraveReturn);
            AddExecutor(ExecutorType.Activate, CardId.Steppleader, SteppleaderTrigger);
            AddExecutor(ExecutorType.Activate, CardId.Breakaway, GraveReturn);
            AddExecutor(ExecutorType.Activate, CardId.Urania, UraniaEffect);
            AddExecutor(ExecutorType.Activate, CardId.VortexTrooper, VortexTrooperEffect);
            AddExecutor(ExecutorType.Activate, CardId.ReturnStroke, FlipContinuousTrap);
            AddExecutor(ExecutorType.Activate, CardId.Alternator, FlipContinuousTrap);
            AddExecutor(ExecutorType.Activate, CardId.Whisker, WhiskerOpponentTurn);

            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, CardId.LinkApple);
            AddExecutor(ExecutorType.Activate, CardId.Alternator, AlternatorGrave);
            AddExecutor(ExecutorType.Activate, CardId.Coulomb, CoulombActivate);
            AddExecutor(ExecutorType.SpSummon, CardId.ThunderKing, ThunderKingSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Utopia, UtopiaSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.UtopiaLightning, UtopiaLightningSummon);
            AddExecutor(ExecutorType.Activate, CardId.UtopiaLightning, DefaultNumberS39UtopiaTheLightningEffect);

            AddExecutor(ExecutorType.Summon, CardId.Surge, PlannedNormalSummon);
            AddExecutor(ExecutorType.Summon, CardId.Grain, PlannedNormalSummon);
            AddExecutor(ExecutorType.Summon, CardId.Emi, PlannedNormalSummon);
            AddExecutor(ExecutorType.Summon, CardId.Crackle, PlannedNormalSummon);
            AddExecutor(ExecutorType.Summon, CardId.VortexTrooper, VortexTrooperSummon);

            // Reveals go first so the revealed monster is on the field for every later destruction.
            AddExecutor(ExecutorType.Activate, CardId.Surge, SurgeHand);
            AddExecutor(ExecutorType.Activate, CardId.Grain, GrainHand);
            AddExecutor(ExecutorType.Activate, CardId.Steppleader, SteppleaderActivate);
            AddExecutor(ExecutorType.Activate, CardId.Steppleader, SteppleaderSummon);
            AddExecutor(ExecutorType.Activate, CardId.Coulomb, CoulombSearch);
            AddExecutor(ExecutorType.Activate, CardId.Breakaway, BreakawayActivate);
            AddExecutor(ExecutorType.Activate, CardId.ReturnStroke, ReturnStrokeGrave);
            AddExecutor(ExecutorType.Activate, CardId.Alternator, AlternatorSwap);
            AddExecutor(ExecutorType.SpSummon, CardId.Urania, UraniaSummon);
            AddExecutor(ExecutorType.Activate, CardId.Whisker, WhiskerOwnTurn);

            AddExecutor(ExecutorType.MonsterSet, CardId.VortexTrooper);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Surge, LateSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Grain, LateSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Emi, LateSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Crackle, LateSummon);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.SpellSet, SpellSetCheck);
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        public override void OnNewTurn()
        {
            alternatorKeepsEmi = false;
            surgeHandUsed = false;
            grainHandUsed = false;
            emiHandUsed = false;
            crackleHandUsed = false;
            whiskerHandUsed = false;
            coulombSearchUsed = false;
            steppleaderSummonUsed = false;
            breakawayUsed = false;
            alternatorGraveUsed = false;
            extraDeckLocked = false;
            blitzcliqueDestroyedThisTurn = false;
            uraniaProcedurePending = false;
            uraniaRevealRemaining = 0;
            base.OnNewTurn();
        }

        public override void OnChainSolved(int chainIndex)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0 && !Duel.IsCurrentSolvingChainNegated())
            {
                bool handReveal = link.HasLocation(CardLocation.Hand)
                    && link.IsActivateCode(CardId.Surge, CardId.Grain, CardId.Emi, CardId.Crackle);
                bool steppleader = link.IsActivateCode(CardId.Steppleader)
                    && link.ActivateDescription == Util.GetStringId(CardId.Steppleader, 1);
                bool breakaway = link.IsActivateCode(CardId.Breakaway) && !link.HasLocation(CardLocation.Grave) && breakawayWantsDestroy;
                bool returnStroke = link.IsActivateCode(CardId.ReturnStroke)
                    && link.ActivateDescription == Util.GetStringId(CardId.ReturnStroke, 1);
                if (handReveal || steppleader || breakaway || returnStroke || link.IsActivateCode(CardId.Whisker))
                    blitzcliqueDestroyedThisTurn = true;
            }
            base.OnChainSolved(chainIndex);
        }

        // ---------------------------------------------------------------- helpers

        private bool IsMyMainPhaseIdle()
        {
            return Duel.Player == 0 && Duel.CurrentChain.Count == 0
                && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2);
        }

        // Type and race come from the card database: cards in the Deck or Graveyard
        // are not always refreshed by the server.
        private static bool IsMonsterCard(ClientCard card)
        {
            if (card == null)
                return false;
            return card.Data != null ? card.Data.HasType(CardType.Monster) : card.IsMonster();
        }

        private static bool IsThunder(ClientCard card)
        {
            if (!IsMonsterCard(card))
                return false;
            return card.Data != null ? (card.Data.Race & (int)CardRace.Thunder) != 0 : card.HasRace(CardRace.Thunder);
        }

        private static bool IsBlitzclique(ClientCard card)
        {
            return card != null && card.HasSetcode(0x1df);
        }

        private bool HasRecoverableBlitzclique()
        {
            return Bot.Graveyard.Any(card => IsMonsterCard(card) && IsBlitzclique(card) && RecoverScore(card) >= 80);
        }

        private int HandCount(int id)
        {
            return Bot.Hand.Count(card => card != null && card.IsCode(id));
        }

        private bool HasFreeMonsterZone()
        {
            return Bot.GetMonstersInMainZone().Count < 5;
        }

        private bool HasCoulombOnField()
        {
            ClientCard field = Bot.GetFieldSpellCard();
            return field != null && field.IsFaceup() && field.IsCode(CardId.Coulomb);
        }

        private bool HasFaceupSpell(int id)
        {
            return Bot.GetSpells().Any(card => card.IsFaceup() && card.IsCode(id));
        }

        private IEnumerable<ClientCard> AllFieldCards()
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells()).Concat(Bot.GetMonsters()).Concat(Bot.GetSpells());
        }

        /// <summary>
        /// Worth of destroying a card: above 0 is a play we want, enemy cards first,
        /// then our own cards that pay back when destroyed.
        /// </summary>
        private int DestroyScore(ClientCard card, bool targeting)
        {
            if (card == null)
                return -1;
            if (card.Controller == 1)
            {
                if (targeting && card.IsShouldNotBeTarget())
                    return 1;
                if (card.Location == CardLocation.MonsterZone)
                {
                    if (card.IsCode(CardId.MechBeastToken))
                        return 40;
                    if (card.HasType(CardType.Token))
                        return 45;
                    if (card.IsFacedown())
                        return 90;
                    int score = 100 + Math.Max(card.Attack, 0) / 100;
                    if (card.IsFloodgate() || card.IsMonsterDangerous() || card.IsMonsterInvincible())
                        score += 100;
                    return score;
                }
                if (card.IsFacedown())
                    return 75;
                if (card.IsFloodgate())
                    return 250;
                if (card.HasType(CardType.Continuous) || card.HasType(CardType.Field)
                    || card.HasType(CardType.Equip) || card.HasType(CardType.Pendulum))
                    return 85;
                return 5;
            }

            if (card.Location == CardLocation.MonsterZone && card.IsCode(CardId.VortexTrooper))
                return 30;
            if (card.Location == CardLocation.SpellZone && card.IsFaceup()
                && card.IsCode(CardId.Steppleader) && steppleaderSummonUsed)
                return 20;
            return -1;
        }

        private ClientCard BestDestroyTarget(Func<ClientCard, bool> filter, bool targeting = true)
        {
            ClientCard best = null;
            int bestScore = 0;
            foreach (ClientCard card in AllFieldCards())
            {
                if (filter != null && !filter(card))
                    continue;
                int score = DestroyScore(card, targeting);
                if (score > bestScore)
                {
                    best = card;
                    bestScore = score;
                }
            }
            return best;
        }

        private ClientCard SurgeTarget()
        {
            return BestDestroyTarget(card => card.Location == CardLocation.MonsterZone);
        }

        private ClientCard GrainTarget()
        {
            return BestDestroyTarget(card => card.Location == CardLocation.SpellZone);
        }

        private bool CanSummonAfterDestroying(ClientCard target)
        {
            if (target == null)
                return false;
            return HasFreeMonsterZone()
                || (target.Controller == 0 && target.Location == CardLocation.MonsterZone && target.Sequence < 5);
        }

        private bool EmiUsable()
        {
            if (emiHandUsed)
                return false;
            return CanSummonAfterDestroying(BestDestroyTarget(null));
        }

        private bool SurgeHandUsable()
        {
            return !surgeHandUsed && CanSummonAfterDestroying(SurgeTarget());
        }

        private bool GrainHandUsable()
        {
            return !grainHandUsed && CanSummonAfterDestroying(GrainTarget());
        }

        private bool CoulombSearchAvailable()
        {
            if (coulombSearchUsed)
                return false;
            if (!HasCoulombOnField() && !Bot.HasInHand(CardId.Coulomb))
                return false;
            return Enemy.GetMonstersInMainZone().Count < 5;
        }

        private int EnemyDestroyableCount(bool targeting)
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells()).Count(card => DestroyScore(card, targeting) > 1);
        }

        private bool EnemyHasStrongMonster(int attack)
        {
            return Enemy.GetMonsters().Any(card => card.IsFaceup() && card.Attack >= attack);
        }

        /// <summary>
        /// Worth of a Thunder monster as the monster Special Summoned from the hand:
        /// cards whose hand effect is spent or dead this turn go first.
        /// </summary>
        private int SummonScore(ClientCard card)
        {
            if (card.IsCode(CardId.Whisker))
            {
                if (whiskerHandUsed)
                    return 100;
                if (HandCount(CardId.Whisker) >= 2)
                    return 75;
                return Bot.Hand.Count(IsThunder) <= 1 ? 60 : 10;
            }
            if (card.IsCode(CardId.ThunderKing))
                return EnemyHasStrongMonster(3300) || Enemy.GetMonsters().Any(c => c.IsMonsterDangerous()) ? 15 : 70;
            if (card.IsCode(CardId.Urania))
                return 55;
            if (card.IsCode(CardId.Emi))
                return 80;
            if (card.IsCode(CardId.Surge))
            {
                if (surgeHandUsed)
                    return 65;
                return Duel.Player == 0 && SurgeTarget() != null ? 20 : 50;
            }
            if (card.IsCode(CardId.Grain))
            {
                if (grainHandUsed)
                    return 55;
                return Duel.Player == 0 && GrainTarget() != null ? 20 : 45;
            }
            if (card.IsCode(CardId.Crackle))
            {
                if (Duel.Player == 0)
                    return HandCount(CardId.Crackle) >= 2 ? 50 : 30;
                return crackleHandUsed ? 50 : 25;
            }
            return 1;
        }

        private int SearchScore(ClientCard card)
        {
            int id = card.Id;
            switch (id)
            {
                case CardId.Emi:
                    return EmiUsable() ? 100 : 20;
                case CardId.Whisker:
                    if (Bot.HasInHand(CardId.Whisker))
                        return 30;
                    if (whiskerHandUsed)
                        return 45;
                    return blitzcliqueDestroyedThisTurn ? 85 : 60;
                case CardId.Surge:
                    if (surgeHandUsed || Bot.HasInHand(CardId.Surge))
                        return 35;
                    return SurgeTarget() != null ? 75 : 50;
                case CardId.Grain:
                    if (grainHandUsed || Bot.HasInHand(CardId.Grain))
                        return 25;
                    return GrainTarget() != null ? 70 : 40;
                case CardId.Crackle:
                    if (Bot.HasInHand(CardId.Crackle))
                        return 20;
                    return Duel.Player == 1 && !crackleHandUsed ? 85 : 55;
                case CardId.ThunderKing:
                    return EnemyHasStrongMonster(2800) ? 72 : 42;
                case CardId.Urania:
                    return 30;
                case CardId.Coulomb:
                    if (HasCoulombOnField() || Bot.HasInHand(CardId.Coulomb))
                        return 10;
                    return coulombSearchUsed ? 60 : 95;
                case CardId.Steppleader:
                    if (Bot.HasInHand(CardId.Steppleader) || HasFaceupSpell(CardId.Steppleader))
                        return 20;
                    return steppleaderSummonUsed ? 50 : 70;
                case CardId.Breakaway:
                    if (Bot.HasInHand(CardId.Breakaway))
                        return 15;
                    return breakawayUsed ? 45 : 65;
                case CardId.ReturnStroke:
                    return Bot.HasInHandOrInSpellZone(CardId.ReturnStroke) ? 10 : 80;
                case CardId.Alternator:
                    return Bot.HasInHandOrInSpellZone(CardId.Alternator) ? 30 : 60;
                default:
                    return 0;
            }
        }

        private int RecoverScore(ClientCard card)
        {
            if (card.Location != CardLocation.Grave)
                return BounceScore(card) - 60;
            switch (card.Id)
            {
                case CardId.Emi:
                    return EmiUsable() ? 100 : 30;
                case CardId.Whisker:
                    return !whiskerHandUsed && blitzcliqueDestroyedThisTurn ? 90 : 70;
                case CardId.Surge:
                    return surgeHandUsed ? 40 : 70;
                case CardId.ThunderKing:
                    return 60;
                case CardId.Crackle:
                    return 50;
                case CardId.Grain:
                    return 45;
                case CardId.Urania:
                    return 40;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Worth of returning one of our monsters to the hand as a cost: a monster whose
        /// hand effect becomes live again is the best payment.
        /// </summary>
        private int BounceScore(ClientCard card)
        {
            switch (card.Id)
            {
                case CardId.Emi:
                    return EmiUsable() ? 100 : 45;
                case CardId.Crackle:
                    return Duel.Player == 1 && !crackleHandUsed ? 75 : 40;
                case CardId.Surge:
                    return Duel.Player == 0 && !surgeHandUsed ? 70 : 50;
                case CardId.Grain:
                    return Duel.Player == 0 && !grainHandUsed ? 60 : 48;
                case CardId.VortexTrooper:
                    return 35;
                case CardId.Urania:
                    return 30;
                case CardId.ThunderKing:
                    return 12;
                case CardId.Whisker:
                    return 5;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// How much we want to keep a card when something must be shuffled back.
        /// </summary>
        private int KeepValue(ClientCard card)
        {
            if (card.Location != CardLocation.Hand)
            {
                if (card.IsCode(CardId.Steppleader) && card.IsFaceup() && steppleaderSummonUsed)
                    return 45;
                return 200;
            }
            bool duplicate = HandCount(card.Id) >= 2;
            switch (card.Id)
            {
                case CardId.LinkApple:
                    return 10;
                case CardId.Coulomb:
                    return HasCoulombOnField() || duplicate ? 15 : 90;
                case CardId.Emi:
                    return 25;
                case CardId.VortexTrooper:
                    return 30;
                case CardId.Urania:
                    return 30;
                case CardId.Breakaway:
                    return breakawayUsed || duplicate ? 28 : 60;
                case CardId.Steppleader:
                    return steppleaderSummonUsed || duplicate || HasFaceupSpell(CardId.Steppleader) ? 28 : 65;
                case CardId.Alternator:
                    return 40;
                case CardId.ReturnStroke:
                    return 55;
                case CardId.ThunderKing:
                    return 50;
                case CardId.Crackle:
                    return duplicate ? 35 : 60;
                case CardId.Grain:
                    return duplicate ? 35 : 55;
                case CardId.Surge:
                    return duplicate ? 40 : 75;
                case CardId.Whisker:
                    return duplicate ? 45 : 90;
                default:
                    return 52;
            }
        }

        private List<ClientCard> NegateTargets()
        {
            return Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsDisabled() && card.HasType(CardType.Effect))
                .OrderByDescending(card => card.Attack)
                .Concat(Enemy.GetSpells().Where(card => card.IsFaceup() && !card.IsDisabled()
                    && (card.HasType(CardType.Continuous) || card.HasType(CardType.Field))))
                .ToList();
        }

        // ---------------------------------------------------------------- chain responses

        private bool ReturnStrokeNegate()
        {
            if (Card.Location != CardLocation.SpellZone || Card.IsFacedown())
                return false;
            return Duel.LastChainPlayer == 1;
        }

        private bool WhiskerNegate()
        {
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            return Duel.LastChainPlayer == 1;
        }

        private bool CrackleEffect()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return true;
            if (Card.Location != CardLocation.Hand)
                return false;
            crackleHandUsed = true;
            extraDeckLocked = true;
            return true;
        }

        private bool EmiEffect()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return true;
            if (Card.Location != CardLocation.Hand || !EmiUsable())
                return false;
            emiHandUsed = true;
            extraDeckLocked = true;
            return true;
        }

        private bool FieldTrigger()
        {
            return Card.Location == CardLocation.MonsterZone;
        }

        private bool GraveReturn()
        {
            return Card.Location == CardLocation.Grave;
        }

        private bool SteppleaderTrigger()
        {
            if (Card.Location == CardLocation.Grave)
                return true;
            return Card.Location == CardLocation.SpellZone && Card.IsFaceup()
                && ActivateDescription != Util.GetStringId(CardId.Steppleader, 1);
        }

        private bool UraniaEffect()
        {
            if (Card.Location == CardLocation.Grave)
                return true;
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            int targets = NegateTargets().Count;
            if (targets == 0)
                return false;
            uraniaRevealRemaining = Math.Min(targets, 3);
            return true;
        }

        private bool VortexTrooperEffect()
        {
            if (ActivateDescription != Util.GetStringId(CardId.VortexTrooper, 0))
                return true;
            return Bot.Hand.Count(card => KeepValue(card) <= 35) >= 2;
        }

        private bool FlipContinuousTrap()
        {
            return Card.Location == CardLocation.SpellZone && Card.IsFacedown();
        }

        private bool WhiskerOpponentTurn()
        {
            if (Card.Location != CardLocation.Hand || Duel.Player != 1)
                return false;
            if (Duel.LastChainPlayer == 0)
                return false;
            whiskerHandUsed = true;
            return true;
        }

        // ---------------------------------------------------------------- main phase

        private bool AlternatorGrave()
        {
            if (Card.Location != CardLocation.Grave || !IsMyMainPhaseIdle())
                return false;
            bool worthIt = (Bot.GetCardCountInDeck(CardId.Coulomb) > 0 && !HasCoulombOnField() && !Bot.HasInHand(CardId.Coulomb))
                || (Bot.GetCardCountInDeck(CardId.Steppleader) > 0 && !Bot.HasInHand(CardId.Steppleader) && !HasFaceupSpell(CardId.Steppleader))
                || (Bot.GetCardCountInDeck(CardId.Breakaway) > 0 && !Bot.HasInHand(CardId.Breakaway));
            if (!worthIt)
                return false;
            alternatorGraveUsed = true;
            return true;
        }

        private bool CoulombActivate()
        {
            if (Card.Location != CardLocation.Hand)
                return false;
            return !HasCoulombOnField();
        }

        private bool CoulombSearch()
        {
            if (Card.Location != CardLocation.SpellZone || Card.IsFacedown())
                return false;
            bool canAnswerToken = (!emiHandUsed && Bot.GetCardCountInDeck(CardId.Emi) > 0 && HasFreeMonsterZone())
                || !surgeHandUsed
                || Bot.HasInHand(CardId.Whisker)
                || Util.GetBestAttack(Bot) > 2000;
            if (!canAnswerToken)
                return false;
            coulombSearchUsed = true;
            return true;
        }

        private bool ThunderKingSummon()
        {
            if (Enemy.GetMonsters().Any(card => card.HasSetcode(0xd3)))
                return false;
            ClientCard target = Enemy.MonsterZone.GetFloodgate();
            if (target == null)
                target = Enemy.MonsterZone.GetDangerousMonster();
            if (target == null)
                target = Enemy.MonsterZone.GetInvincibleMonster();
            if (target == null)
            {
                ClientCard best = Enemy.GetMonsters()
                    .Where(card => card.IsFaceup() && !card.HasType(CardType.Token))
                    .OrderByDescending(card => card.Attack)
                    .FirstOrDefault();
                bool canRemoveKaiju = (Bot.HasInHand(CardId.Surge) && !surgeHandUsed)
                    || Bot.HasInHand(CardId.Whisker)
                    || (Bot.HasInHand(CardId.Breakaway) && !breakawayUsed && Bot.GetMonsters().Any(IsThunder));
                if (best != null && canRemoveKaiju && (best.Attack >= 2500 || best.IsShouldNotBeTarget()))
                    target = best;
            }
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        private bool UtopiaSummon()
        {
            if (extraDeckLocked || !Bot.HasInExtra(CardId.UtopiaLightning))
                return false;
            if (SurgeHandUsable() && Bot.HasInHand(CardId.Surge))
                return false;
            if (Bot.HasInHand(CardId.Whisker))
                return false;
            return DefaultNumberS39UtopiaTheLightningSummon();
        }

        private bool UtopiaLightningSummon()
        {
            return Bot.HasInMonstersZone(CardId.Utopia);
        }

        private bool HasOtherStarter(int exceptId)
        {
            if (CoulombSearchAvailable())
                return true;
            if (!steppleaderSummonUsed && (Bot.HasInHand(CardId.Steppleader) || HasFaceupSpell(CardId.Steppleader))
                && Bot.Hand.Count(IsThunder) >= 2)
                return true;
            if (exceptId != CardId.Grain && Bot.HasInHand(CardId.Grain) && GrainHandUsable())
                return true;
            if (exceptId != CardId.Surge && Bot.HasInHand(CardId.Surge) && SurgeHandUsable())
                return true;
            if (Bot.HasInHand(CardId.Breakaway) && !breakawayUsed && Bot.GetCardCountInDeck(CardId.ReturnStroke) + Bot.GetCardCountInDeck(CardId.Alternator) == 0
                && EnemyDestroyableCount(true) > 0)
                return true;
            return false;
        }

        private int NormalSummonScore(ClientCard card)
        {
            int copies = HandCount(card.Id);
            switch (card.Id)
            {
                case CardId.Emi:
                    return 70;
                case CardId.Crackle:
                    if (copies >= 2)
                        return 75;
                    return HasOtherStarter(0) ? 55 : 0;
                case CardId.Grain:
                    if (!GrainHandUsable())
                        return 60;
                    return copies >= 2 ? 62 : 25;
                case CardId.Surge:
                    // With a target on the field the reveal is worth more than the Normal Summon.
                    if (SurgeHandUsable())
                        return copies >= 2 ? 90 : 0;
                    return HasOtherStarter(CardId.Surge) ? 90 : 65;
                default:
                    return 0;
            }
        }

        private bool HasAnyStarter()
        {
            if (HasOtherStarter(0))
                return true;
            if (Bot.Graveyard.Any(card => card != null && card.IsCode(CardId.ReturnStroke)) && HasRecoverableBlitzclique())
                return true;
            return HasFaceupSpell(CardId.Alternator) && Bot.GetCardCountInDeck(CardId.Emi) > 0 && !emiHandUsed
                && Bot.Hand.Count(IsBlitzclique) >= 2;
        }

        private bool PlannedNormalSummon()
        {
            if (!HasAnyStarter())
                return false;
            ClientCard best = null;
            int bestScore = 49;
            foreach (ClientCard card in Bot.Hand)
            {
                if (card == null)
                    continue;
                int score = NormalSummonScore(card);
                if (score > bestScore)
                {
                    best = card;
                    bestScore = score;
                }
            }
            return best != null && Card.IsCode(best.Id);
        }

        private bool VortexTrooperSummon()
        {
            // Face-up only when Surge is about to destroy it for the draw, or to dig
            // with its effect when nothing else can start and nothing can attack it.
            if (Bot.HasInHand(CardId.Surge) && !surgeHandUsed
                && Enemy.GetMonsterCount() == 0 && !CoulombSearchAvailable() && HasFreeMonsterZone())
                return true;
            return !HasAnyStarter() && Enemy.GetMonsterCount() == 0
                && Bot.Hand.Count(card => card != Card && KeepValue(card) <= 35) >= 2;
        }

        // Steppleader destroying itself triggers every Blitzclique monster already on the
        // field, so with one out even a card we would rather hold is worth summoning.
        private int SteppleaderSummonThreshold()
        {
            return Bot.GetMonsters().Any(card => card.IsFaceup() && IsBlitzclique(card)) ? 25 : 40;
        }

        private bool SteppleaderActivate()
        {
            if (Card.Location != CardLocation.Hand && !(Card.Location == CardLocation.SpellZone && Card.IsFacedown()))
                return false;
            if (!IsMyMainPhaseIdle() || HasFaceupSpell(CardId.Steppleader))
                return false;
            if (Bot.GetSpellCountWithoutField() >= 5)
                return false;
            bool willSummon = !steppleaderSummonUsed && HasFreeMonsterZone()
                && Bot.Hand.Any(card => IsThunder(card) && SummonScore(card) >= SteppleaderSummonThreshold());
            return willSummon || Bot.Graveyard.Any(IsThunder);
        }

        private bool SteppleaderSummon()
        {
            if (Card.Location != CardLocation.SpellZone || Card.IsFacedown())
                return false;
            if (ActivateDescription != Util.GetStringId(CardId.Steppleader, 1))
                return false;
            if (!Bot.Hand.Any(card => IsThunder(card) && SummonScore(card) >= SteppleaderSummonThreshold()))
                return false;
            steppleaderSummonUsed = true;
            return true;
        }

        private bool BreakawayActivate()
        {
            if (Card.Location == CardLocation.Grave || !IsMyMainPhaseIdle())
                return false;
            int freeZones = 5 - Bot.GetSpellCountWithoutField() - (Card.Location == CardLocation.Hand ? 1 : 0);
            bool canPlace = freeZones > 0
                && ((Bot.GetCardCountInDeck(CardId.ReturnStroke) > 0 && !HasFaceupSpell(CardId.ReturnStroke))
                    || (Bot.GetCardCountInDeck(CardId.Alternator) > 0 && !HasFaceupSpell(CardId.Alternator)));

            ClientCard bounce = Bot.GetMonsters().Where(card => card.IsFaceup() && IsThunder(card))
                .OrderByDescending(BounceScore).FirstOrDefault();
            ClientCard target = BestDestroyTarget(card => card.Controller == 1);
            bool canDestroy = bounce != null && target != null;
            bool strongDestroy = canDestroy && DestroyScore(target, true) >= 100 && BounceScore(bounce) >= 60;

            if (strongDestroy || (canDestroy && !canPlace))
                breakawayWantsDestroy = true;
            else if (canPlace)
                breakawayWantsDestroy = false;
            else
                return false;
            breakawayUsed = true;
            return true;
        }

        private bool ReturnStrokeGrave()
        {
            if (Card.Location != CardLocation.Grave || !IsMyMainPhaseIdle())
                return false;
            return HasRecoverableBlitzclique();
        }

        /// <summary>
        /// The swap is live on both turns: trading any other Blitzclique card for Emi
        /// triggers her, which destroys a card and Special Summons a Thunder.
        /// </summary>
        private bool AlternatorSwap()
        {
            if (Card.Location != CardLocation.SpellZone)
                return false;
            bool withActivation = ActivateDescription == AskUseEffectOnActivation;
            if (!withActivation && (Card.IsFacedown() || ActivateDescription != Util.GetStringId(CardId.Alternator, 1)))
                return false;
            if (!withActivation && Duel.Player == 0 && !IsMyMainPhaseIdle())
                return false;

            List<ClientCard> costs = Bot.Hand.Where(IsBlitzclique).OrderBy(KeepValue).ToList();
            if (costs.Count == 0)
                return false;

            if (Bot.GetCardCountInDeck(CardId.Emi) > 0 && EmiUsable() && costs.Any(card => !card.IsCode(CardId.Emi) && KeepValue(card) <= 65))
            {
                alternatorKeepsEmi = true;
                return true;
            }

            ClientCard cost = costs[0];
            if (KeepValue(cost) > 40)
                return false;
            bool whiskerLive = !cost.IsCode(CardId.Whisker) && Bot.GetCardCountInDeck(CardId.Whisker) > 0 && !Bot.HasInHand(CardId.Whisker);
            bool surgeLive = Duel.Player == 0 && !cost.IsCode(CardId.Surge) && Bot.GetCardCountInDeck(CardId.Surge) > 0
                && !Bot.HasInHand(CardId.Surge) && !surgeHandUsed && SurgeTarget() != null;
            bool crackleLive = Duel.Player == 1 && !cost.IsCode(CardId.Crackle) && Bot.GetCardCountInDeck(CardId.Crackle) > 0
                && !Bot.HasInHand(CardId.Crackle) && !crackleHandUsed;
            if (!whiskerLive && !surgeLive && !crackleLive)
                return false;
            alternatorKeepsEmi = false;
            return true;
        }

        private bool UraniaSummon()
        {
            if (!IsMyMainPhaseIdle())
                return false;
            ClientCard bounce = Bot.GetMonsters().OrderByDescending(BounceScore).FirstOrDefault();
            if (bounce == null)
                return false;
            int score = BounceScore(bounce);
            bool reuse = score >= 60;
            bool negate = score >= 35 && NegateTargets().Count > 0 && Bot.Hand.Any(card => card != Card && card.IsMonster());
            if (!reuse && !negate)
                return false;
            uraniaProcedurePending = true;
            return true;
        }

        private bool SurgeHand()
        {
            if (Card.Location != CardLocation.Hand || !SurgeHandUsable())
                return false;
            surgeHandUsed = true;
            extraDeckLocked = true;
            return true;
        }

        private bool GrainHand()
        {
            if (Card.Location != CardLocation.Hand || !GrainHandUsable())
                return false;
            grainHandUsed = true;
            extraDeckLocked = true;
            return true;
        }

        private bool WhiskerOwnTurn()
        {
            if (Card.Location != CardLocation.Hand || !IsMyMainPhaseIdle())
                return false;
            whiskerHandUsed = true;
            extraDeckLocked = true;
            return true;
        }

        private bool LateSummon()
        {
            if (Card.IsCode(CardId.Crackle) && HandCount(CardId.Crackle) <= 1 && Bot.GetMonsterCount() > 0)
                return false;
            return true;
        }

        public override bool OnSelectMonsterSummonOrSet(ClientCard card)
        {
            // A leftover Normal Summon has no combo behind it: keep the body face-down
            // unless it can attack an empty field.
            if (Enemy.GetMonsterCount() == 0 && Duel.Turn > 1 && Duel.Phase == DuelPhase.Main1)
                return false;
            // Face-up it still triggers when our hand traps or Return Stroke destroy something.
            bool destructionExpected = Bot.HasInHand(CardId.Crackle) || Bot.HasInHand(CardId.Whisker)
                || Bot.HasInHand(_CardId.GhostOgreAndSnowRabbit) || Bot.HasInSpellZone(CardId.ReturnStroke);
            if (IsBlitzclique(card) && destructionExpected && !Util.IsOneEnemyBetterThanValue(card.Attack + 1000, true))
                return false;
            return true;
        }

        private bool SpellSetCheck()
        {
            if (Card.IsCode(CardId.ReturnStroke))
                return Bot.GetSpellCountWithoutField() < 4;
            if (Card.IsCode(CardId.Alternator))
                return Bot.GetSpellCountWithoutField() < 4 && !Bot.HasInSpellZone(CardId.Alternator);
            if (Card.IsCode(_CardId.MysticalSpaceTyphoon))
                return Bot.GetSpellCountWithoutField() < 3;
            return false;
        }

        // ---------------------------------------------------------------- selections

        public override bool OnSelectYesNo(int desc)
        {
            if (desc == Util.GetStringId(CardId.Whisker, 2))
                return BestDestroyTarget(null, false) != null;
            return base.OnSelectYesNo(desc);
        }

        public override int OnSelectOption(IList<int> options)
        {
            int place = Util.GetStringId(CardId.Breakaway, 2);
            int destroy = Util.GetStringId(CardId.Breakaway, 3);
            if (options.Contains(place) || options.Contains(destroy))
            {
                int wanted = breakawayWantsDestroy ? destroy : place;
                int index = options.IndexOf(wanted);
                return index >= 0 ? index : 0;
            }
            return base.OnSelectOption(options);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (AI.HaveSelectedPosition())
                return base.OnSelectPosition(cardId, positions);
            if (cardId == CardId.MechBeastToken)
                return positions.Contains(CardPosition.FaceUpAttack) ? CardPosition.FaceUpAttack : 0;

            NamedCard data = NamedCard.Get(cardId);
            if (data == null || !positions.Contains(CardPosition.FaceUpDefence) || !positions.Contains(CardPosition.FaceUpAttack))
                return base.OnSelectPosition(cardId, positions);

            int attack = data.Attack + (HasCoulombOnField() ? 500 : 0);
            if (Duel.Player == 1)
            {
                bool outclassed = Enemy.GetMonsters().Any(card => card.IsFaceup() && card.IsAttack() && card.Attack >= attack);
                return outclassed || attack < 2000 ? CardPosition.FaceUpDefence : CardPosition.FaceUpAttack;
            }
            if (Duel.Turn == 1 || Duel.Phase == DuelPhase.Main2)
                return attack < 2500 && data.Attack < 2000 ? CardPosition.FaceUpDefence : CardPosition.FaceUpAttack;

            CardPosition safer = SummonPosition.DefenceIfExposed(Duel, cardId, positions);
            if (safer != 0)
                return safer;
            return base.OnSelectPosition(cardId, positions);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            // Only our own effects are answered here: the link being resolved, the link being
            // activated (costs and targets), or no chain at all for summon procedures.
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            ClientCard activating = Duel.GetCurrentChainCard();
            bool enemyEffect = solving != null ? solving.ActivatePlayer != 0 : (activating != null && Duel.LastChainPlayer != 0);
            if (enemyEffect || AI.HaveSelectedCards() || cards.Count == 0)
                return base.OnSelectCard(cards, min, max, hint, cancelable);

            switch (hint)
            {
                case HintMsg.Destroy:
                    return PickDestroy(cards, min, max);
                case HintMsg.SpSummon:
                    if (cards.All(card => card.Location == CardLocation.Hand && card.Controller == 0))
                        return PickSummons(cards, min, max);
                    break;
                case HintMsg.AddToHand:
                    if (cards.All(card => card.Location == CardLocation.Deck))
                        return PickTop(cards, min, max, SearchScore);
                    if (cards.All(card => card.Controller == 0))
                        return PickTop(cards, min, max, RecoverScore);
                    break;
                case HintFieldFirst:
                    // Return Stroke offers our field first; cancelling opens the Graveyard choice.
                    if (cancelable && HasRecoverableBlitzclique())
                        return new List<ClientCard>();
                    return PickTop(cards, min, max, BounceScore);
                case HintMsg.ReturnToHand:
                    if (cards.All(card => card.Controller == 0 && card.Location == CardLocation.MonsterZone))
                    {
                        if (min == 0)
                        {
                            if (!uraniaProcedurePending)
                                return new List<ClientCard>();
                            uraniaProcedurePending = false;
                            return PickTop(cards, 1, 1, BounceScore);
                        }
                        uraniaProcedurePending = false;
                        return PickTop(cards, min, min, BounceScore);
                    }
                    break;
                case HintMsg.ToDeck:
                    if (cards.All(card => card.Controller == 0))
                    {
                        bool keepEmi = alternatorKeepsEmi && cards.Any(card => !card.IsCode(CardId.Emi));
                        alternatorKeepsEmi = false;
                        return PickTop(cards, min, min, card => (keepEmi && card.IsCode(CardId.Emi) ? -1000 : 0) - KeepValue(card));
                    }
                    break;
                case HintMsg.ToGrave:
                    if (cards.All(card => card.Location == CardLocation.Deck))
                        return PickTop(cards, min, min, MillScore);
                    break;
                case HintMsg.ToField:
                    if (cards.All(card => card.Location == CardLocation.Deck))
                        return PickTop(cards, min, min, card => card.IsCode(CardId.ReturnStroke) ? 2 : 1);
                    break;
                case HintMsg.Confirm:
                    if (cards.All(card => card.Location == CardLocation.Hand && card.Controller == 0))
                    {
                        if (min == 0 && uraniaRevealRemaining <= 0)
                            return new List<ClientCard>();
                        uraniaRevealRemaining--;
                        return PickTop(cards, 1, 1, card => -KeepValue(card));
                    }
                    break;
                case HintMsg.Disable:
                    if (cards.All(card => card.Controller == 1))
                    {
                        List<ClientCard> wanted = NegateTargets();
                        return PickTop(cards, min, min, card =>
                        {
                            int index = wanted.IndexOf(card);
                            return index >= 0 ? 100 - index : 0;
                        });
                    }
                    break;
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private int MillScore(ClientCard card)
        {
            switch (card.Id)
            {
                case CardId.Alternator:
                    return alternatorGraveUsed ? 50 : 90;
                case CardId.Steppleader:
                    return 60;
                case CardId.Breakaway:
                    return 55;
                default:
                    return 40;
            }
        }

        private static IList<ClientCard> PickTop(IList<ClientCard> cards, int min, int count, Func<ClientCard, int> score)
        {
            int take = Math.Max(min, Math.Min(count, cards.Count));
            return cards.OrderByDescending(score).Take(take).ToList();
        }

        private IList<ClientCard> PickDestroy(IList<ClientCard> cards, int min, int max)
        {
            bool targeting = max == 1;
            List<ClientCard> ordered = cards.OrderByDescending(card => DestroyScore(card, targeting)).ToList();
            int wanted = ordered.Count(card => DestroyScore(card, targeting) > 0);
            int take = Math.Max(min, Math.Min(max, wanted));
            return ordered.Take(take).ToList();
        }

        private IList<ClientCard> PickSummons(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = cards.OrderByDescending(SummonScore).ToList();
            int wanted = 1;
            if (max > 1)
            {
                wanted = Math.Max(wanted, EnemyDestroyableCount(false));
                wanted = Math.Max(wanted, ordered.Count(card => SummonScore(card) >= 60));
            }
            int take = Math.Max(min, Math.Min(Math.Min(max, wanted), ordered.Count));
            return ordered.Take(take).ToList();
        }
    }
}
