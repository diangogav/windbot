using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    [Deck("DarkTimeWizard", "AI_DarkTimeWizard")]
    public class DarkTimeWizardExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int Gazelle = 23076639;
            public const int Berfomet = 55461744;
            public const int Mirror = 28954097;
            public const int Apprentice = 58143852;
            public const int Diabellze = 53765052;
            public const int SkullArchfiend = 24088928;
            public const int BlackChaos = 44001993;
            public const int SkullMeister = 67750322;
            public const int Alligator = 4881365;
            public const int Panther = 77482666;
            public const int Jinzo = 40275470;

            public const int DarkTimeWizard = 40235813;
            public const int Scapegoats = 3129527;
            public const int Dice = 76630812;
            public const int ChimeraFusion = 63136489;
            public const int Ritual = 33599853;
            // _CardId.BookOfMoon = 14087893;
            public const int Graverobber = 65118318;
            public const int ReversalBox = 2606417;

            public const int Exceed = 17242022;
            public const int IllusionBeast = 38264974;
            public const int BerfometKing = 69601012;
            public const int ChimeraKing = 1769875;
            public const int Magnum = 43227;
            public const int Dragostapelia = 69946549;
            public const int MoaRegina = 46174776;
            public const int SolErysichthon = 9785661;
            public const int Restrict = 30138615;

            public const int ScapegoatToken = 3129528;

            // Opposing cards the lines are ordered around.
            public const int RyzealCross = 6798031;
            public const int RyzealDetonator = 34909328;
            public const int RyzealDuoDrive = 7511613;
            public const int ExcitonKnight = 46772449;
            public const int StarliegePhotonBlastDragon = 16643334;
        }

        // System string the Scapegoat Tokens use to offer the destruction replacement.
        private const int AskDestroyReplace = 96;

        private static readonly int[] FusionTargets =
        {
            CardId.IllusionBeast, CardId.BerfometKing, CardId.ChimeraKing, CardId.SolErysichthon,
            CardId.Restrict, CardId.MoaRegina, CardId.Dragostapelia, CardId.Magnum
        };

        private static readonly int[] WizardCards =
        {
            CardId.Alligator, CardId.Panther, CardId.Jinzo, CardId.DarkTimeWizard, CardId.Scapegoats,
            CardId.Dice, CardId.Graverobber, CardId.ReversalBox
        };

        private bool normalSummonUsed;
        private bool gazelleSearchUsed;
        private bool gazelleMaterialUsed;
        private bool berfometSearchUsed;
        private bool berfometMaterialUsed;
        private bool mirrorTributeUsed;
        private bool apprenticeSummonUsed;
        private bool apprenticeSearchUsed;
        private bool alligatorSummonUsed;
        private bool alligatorSearchUsed;
        private bool pantherUsed;
        private bool skullTriggerUsed;
        private bool fusionRecoverUsed;
        private bool wizardSearchUsed;
        private bool wizardCoinUsed;
        private bool berfometKingUsed;
        private bool chimeraKingUsed;
        private bool diceUsed;
        private bool tributedThisTurn;
        private bool crossAnswered;
        private bool enemyUsesExtraDeck;

        private bool wizardWantsCoin;
        private bool exceedSummoning;
        private int fusionPlanTarget;
        private List<ClientCard> fusionPlanMaterials = new List<ClientCard>();
        private List<ClientCard> ritualPlan = new List<ClientCard>();

        private class FusionPlan
        {
            public int TargetId;
            public ClientCard First;
            public ClientCard Second;
            public int Score;
        }

        public DarkTimeWizardExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, CardId.SkullMeister, MeisterNegate);
            AddExecutor(ExecutorType.Activate, CardId.Mirror, MirrorNegateGrave);
            AddExecutor(ExecutorType.Activate, CardId.Mirror, MirrorNegateField);
            AddExecutor(ExecutorType.Activate, CardId.ReversalBox, BoxEffect);
            AddExecutor(ExecutorType.Activate, CardId.Magnum, MagnumDestroy);
            AddExecutor(ExecutorType.Activate, CardId.Dragostapelia, DragostapeliaCounter);
            AddExecutor(ExecutorType.Activate, CardId.Dice, DiceGrave);
            AddExecutor(ExecutorType.Activate, CardId.Dice, DiceDefence);
            AddExecutor(ExecutorType.Activate, _CardId.BookOfMoon, BookOfMoonOpponentTurn);
            AddExecutor(ExecutorType.Activate, CardId.Scapegoats, ScapegoatsOpponentTurn);
            AddExecutor(ExecutorType.Activate, CardId.Graverobber, GraverobberTrap);
            AddExecutor(ExecutorType.Activate, CardId.DarkTimeWizard, WizardOpponentTurn);
            AddExecutor(ExecutorType.Activate, CardId.ChimeraKing, ChimeraKingRevive);
            AddExecutor(ExecutorType.Activate, CardId.BerfometKing, BerfometKingRevive);
            AddExecutor(ExecutorType.Activate, CardId.ReversalBox, BoxFlip);
            for (int i = 0; i < 4; ++i)
                AddExecutor(ExecutorType.Activate, CardId.ScapegoatToken + i, TokenReplace);

            AddExecutor(ExecutorType.Activate, CardId.Gazelle, GazelleEffect);
            AddExecutor(ExecutorType.Activate, CardId.Berfomet, BerfometEffect);
            AddExecutor(ExecutorType.Activate, CardId.Apprentice, ApprenticeSearch);
            AddExecutor(ExecutorType.Activate, CardId.Alligator, AlligatorSearch);
            AddExecutor(ExecutorType.Activate, CardId.Jinzo);
            AddExecutor(ExecutorType.Activate, CardId.SkullArchfiend, SkullTrigger);
            AddExecutor(ExecutorType.Activate, CardId.BlackChaos, BlackChaosRecover);
            AddExecutor(ExecutorType.Activate, CardId.ChimeraKing, ChimeraKingDiscard);
            AddExecutor(ExecutorType.Activate, CardId.BerfometKing, BerfometKingSend);
            AddExecutor(ExecutorType.Activate, CardId.IllusionBeast);
            AddExecutor(ExecutorType.Activate, CardId.SolErysichthon, SolEffect);
            AddExecutor(ExecutorType.Activate, CardId.Diabellze, DiabellzeEffect);
            AddExecutor(ExecutorType.Activate, CardId.Restrict, RestrictEffect);
            AddExecutor(ExecutorType.Activate, CardId.Exceed);

            // The coin toss goes first: it is free while we control nothing.
            AddExecutor(ExecutorType.Activate, _CardId.BookOfMoon, BookOfMoonBreaker);
            AddExecutor(ExecutorType.Activate, CardId.DarkTimeWizard, WizardCoin);
            AddExecutor(ExecutorType.SpSummon, CardId.Exceed, ExceedSummon);
            AddExecutor(ExecutorType.Activate, CardId.DarkTimeWizard, WizardSearch);
            AddExecutor(ExecutorType.Activate, CardId.Alligator, AlligatorSummon);
            AddExecutor(ExecutorType.Activate, CardId.Scapegoats, ScapegoatsMain);
            AddExecutor(ExecutorType.SpSummon, CardId.Apprentice, ApprenticeBait);
            AddExecutor(ExecutorType.Summon, CardId.Mirror, SummonStarter);
            AddExecutor(ExecutorType.Activate, CardId.Mirror, MirrorTribute);
            AddExecutor(ExecutorType.Summon, CardId.Gazelle, SummonStarter);
            AddExecutor(ExecutorType.MonsterSet, CardId.Mirror, SetMirrorWall);
            AddExecutor(ExecutorType.Summon, CardId.Berfomet, SummonBerfomet);
            AddExecutor(ExecutorType.SpSummon, CardId.Apprentice, ApprenticeSummon);
            AddExecutor(ExecutorType.Summon, CardId.Panther, SummonPanther);
            AddExecutor(ExecutorType.Activate, CardId.Panther, PantherEffect);
            AddExecutor(ExecutorType.Activate, CardId.ChimeraFusion, FusionRecover);
            AddExecutor(ExecutorType.Activate, CardId.ChimeraFusion, FusionActivate);
            AddExecutor(ExecutorType.Activate, CardId.Ritual, RitualRecover);
            AddExecutor(ExecutorType.Activate, CardId.Ritual, RitualActivate);
            AddExecutor(ExecutorType.Activate, CardId.SkullArchfiend, SkullSummon);
            AddExecutor(ExecutorType.Activate, CardId.BlackChaos, BlackChaosBanish);
            AddExecutor(ExecutorType.Activate, CardId.MoaRegina, MoaRevive);
            AddExecutor(ExecutorType.Activate, CardId.Graverobber, GraverobberGrave);
            AddExecutor(ExecutorType.Activate, _CardId.BookOfMoon, BookOfMoonMain);
            AddExecutor(ExecutorType.Activate, CardId.Dice, DiceMain);

            AddExecutor(ExecutorType.Summon, CardId.Panther, SummonBody);
            AddExecutor(ExecutorType.MonsterSet, CardId.Panther, SetBody);
            AddExecutor(ExecutorType.MonsterSet, CardId.SkullMeister, SetWall);
            AddExecutor(ExecutorType.Repos, ReposCheck);
            AddExecutor(ExecutorType.SpellSet, SpellSetCheck);
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        public override void OnNewTurn()
        {
            normalSummonUsed = false;
            gazelleSearchUsed = false;
            gazelleMaterialUsed = false;
            berfometSearchUsed = false;
            berfometMaterialUsed = false;
            mirrorTributeUsed = false;
            apprenticeSummonUsed = false;
            apprenticeSearchUsed = false;
            alligatorSummonUsed = false;
            alligatorSearchUsed = false;
            pantherUsed = false;
            skullTriggerUsed = false;
            fusionRecoverUsed = false;
            wizardSearchUsed = false;
            wizardCoinUsed = false;
            berfometKingUsed = false;
            chimeraKingUsed = false;
            diceUsed = false;
            tributedThisTurn = false;
            crossAnswered = false;
            exceedSummoning = false;
            fusionPlanTarget = 0;
            fusionPlanMaterials.Clear();
            ritualPlan.Clear();
            base.OnNewTurn();
        }

        public override void OnChainSolved(int chainIndex)
        {
            ChainInfo link = Duel.GetCurrentSolvingChainInfo();
            if (link != null && link.ActivatePlayer == 0 && link.RelatedCard != null && IsMonsterCard(link.RelatedCard)
                && Enemy.HasInSpellZone(CardId.RyzealCross, true, true))
                crossAnswered = true;
            base.OnChainSolved(chainIndex);
        }

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (!base.OnPreBattleBetween(attacker, defender))
                return false;
            // No even trades: a body left on the field is worth more than the exchange.
            return attacker.RealPower > defender.RealPower || attacker.IsCode(CardId.Mirror, CardId.Apprentice, CardId.IllusionBeast);
        }

        // ---------------------------------------------------------------- helpers

        // Idle command only: in the chain windows of our own turn the server cannot offer
        // Summons, so answering there would run a play ahead of the Summons ranked above it.
        private bool IsMyMainPhaseIdle()
        {
            return Duel.Player == 0 && Duel.CurrentChain.Count == 0 && CurrentTiming == -1
                && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2);
        }

        private bool IsOpponentTurnWindow()
        {
            return Duel.Player == 1 && Duel.LastChainPlayer != 0;
        }

        // Type, race, level and attribute come from the card database unless the card is
        // face-up on the field: cards in the Deck or Graveyard are not always refreshed.
        private static bool IsMonsterCard(ClientCard card)
        {
            if (card == null)
                return false;
            if (card.Location == CardLocation.MonsterZone)
                return true;
            return card.Data != null ? card.Data.HasType(CardType.Monster) : card.IsMonster();
        }

        private static bool OnFieldFaceup(ClientCard card)
        {
            return card.Location == CardLocation.MonsterZone && card.IsFaceup();
        }

        private static int RaceOf(ClientCard card)
        {
            if (OnFieldFaceup(card) && card.Race != 0)
                return card.Race;
            return card.Data != null ? card.Data.Race : card.Race;
        }

        private static int LevelOf(ClientCard card)
        {
            if (OnFieldFaceup(card) && card.Level != 0)
                return card.Level;
            return card.Data != null ? card.Data.Level : card.Level;
        }

        private static bool HasRaceOf(ClientCard card, CardRace race)
        {
            return IsMonsterCard(card) && (RaceOf(card) & (int)race) != 0;
        }

        private static bool IsBeast(ClientCard card)
        {
            return HasRaceOf(card, CardRace.Beast);
        }

        private static bool IsFiend(ClientCard card)
        {
            return HasRaceOf(card, CardRace.Fiend);
        }

        private static bool IsIllusion(ClientCard card)
        {
            return HasRaceOf(card, CardRace.Illusion);
        }

        private static bool IsSpellcaster(ClientCard card)
        {
            return HasRaceOf(card, CardRace.SpellCaster);
        }

        private static bool IsDark(ClientCard card)
        {
            if (!IsMonsterCard(card))
                return false;
            int attribute = OnFieldFaceup(card) && card.Attribute != 0 ? card.Attribute
                : (card.Data != null ? card.Data.Attribute : card.Attribute);
            return (attribute & (int)CardAttribute.Dark) != 0;
        }

        private static bool IsFusionMonster(ClientCard card)
        {
            return card.Data != null ? card.Data.HasType(CardType.Fusion) : card.HasType(CardType.Fusion);
        }

        private static bool IsToken(ClientCard card)
        {
            return card.HasType(CardType.Token) || (card.Id >= CardId.ScapegoatToken && card.Id < CardId.ScapegoatToken + 4);
        }

        // These Fusion Monsters are named "Chimera the Flying Mythical Beast" on the field and in the GY.
        private static bool IsChimera(ClientCard card)
        {
            return card != null && card.IsCode(CardId.ChimeraKing, CardId.BerfometKing, CardId.IllusionBeast);
        }

        private bool HasChimeraOnField()
        {
            return Bot.GetMonsters().Any(card => card.IsFaceup() && IsChimera(card));
        }

        private bool HasChimeraAvailable()
        {
            return HasChimeraOnField() || Bot.Graveyard.Any(IsChimera);
        }

        private static bool MentionsWizard(ClientCard card)
        {
            return card != null && card.IsCode(WizardCards);
        }

        private int HandCount(int id)
        {
            return Bot.Hand.Count(card => card != null && card.IsCode(id));
        }

        private int FreeMonsterZones()
        {
            return 5 - Bot.GetMonstersInMainZone().Count;
        }

        private bool EnemyXyzWithMaterial(params int[] ids)
        {
            return Enemy.GetMonsters().Any(card => card.IsFaceup() && !card.IsDisabled() && card.IsCode(ids) && card.HasXyzMaterial());
        }

        // Ryzeal Cross negates one of our monster effects per turn by detaching from a Ryzeal Xyz.
        private bool CrossLive()
        {
            return !crossAnswered && Enemy.HasInSpellZone(CardId.RyzealCross, true, true)
                && EnemyXyzWithMaterial(CardId.RyzealDetonator, CardId.RyzealDuoDrive);
        }

        // Ryzeal Detonator destroys a card on the field in response to each of our activations.
        private bool DetonatorLive()
        {
            return EnemyXyzWithMaterial(CardId.RyzealDetonator);
        }

        // Negated, Alligator's hand effect leaves him in hand: a free answer to the one negate.
        private bool CanAlligatorBait()
        {
            return Bot.HasInHand(CardId.Alligator) && !alligatorSummonUsed && FreeMonsterZones() > 0
                && Bot.Hand.Count(MentionsWizard) >= 2;
        }

        private bool CanApprenticeBait()
        {
            return Bot.HasInHand(CardId.Apprentice) && !apprenticeSummonUsed && FreeMonsterZones() > 0
                && Bot.Hand.Count >= 2;
        }

        // A starter waits while a cheaper monster effect can still draw out the negate.
        private bool StarterShouldWait()
        {
            return CrossLive() && (CanAlligatorBait() || CanApprenticeBait());
        }

        private bool HasFusionSpell()
        {
            return Bot.HasInHand(CardId.ChimeraFusion) || Bot.HasInSpellZone(CardId.ChimeraFusion);
        }

        private bool SkullTriggerLive()
        {
            return !skullTriggerUsed && Bot.GetCardCountInDeck(CardId.BlackChaos) > 0
                && (Bot.GetCardCountInDeck(CardId.Ritual) > 0 || Bot.HasInHand(CardId.Ritual));
        }

        private int EnemyBestAttack()
        {
            return Enemy.GetMonsters().Where(card => card.IsFaceup()).Select(card => card.Attack).DefaultIfEmpty(0).Max();
        }

        /// <summary>
        /// Worth of removing an enemy card: monsters by ATK, problem cards first.
        /// </summary>
        private int RemovalScore(ClientCard card)
        {
            if (card == null || card.Controller != 1)
                return -1;
            if (card.Location == CardLocation.MonsterZone)
            {
                if (IsToken(card))
                    return 40;
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

        private bool ShouldTossCoin()
        {
            List<ClientCard> enemy = Enemy.GetMonsters();
            if (enemy.Count == 0)
                return false;
            List<ClientCard> mine = Bot.GetMonsters().Where(card => !IsToken(card)).ToList();
            int enemyValue = enemy.Sum(card => card.IsFaceup() ? Math.Max(card.Attack, 1000) : 1200);
            if (mine.Count == 0)
                return enemy.Count >= 2 || enemyValue >= 1800;
            int myValue = mine.Sum(card => Math.Max(card.Attack, 1000));
            return myValue * 3 <= enemyValue;
        }

        /// <summary>
        /// Worth of a card offered by one of our searches.
        /// </summary>
        private int SearchScore(ClientCard card)
        {
            bool held = Bot.HasInHand(card.Id);
            switch (card.Id)
            {
                case CardId.ChimeraFusion:
                    return HasFusionSpell() ? 40 : 100;
                case CardId.Gazelle:
                    return held ? 45 : 90;
                case CardId.Berfomet:
                    return HasFusionSpell() ? 80 : 50;
                case CardId.Mirror:
                    if (held)
                        return 40;
                    return normalSummonUsed ? 70 : 95;
                case CardId.Apprentice:
                    if (held)
                        return 30;
                    return apprenticeSummonUsed ? 50 : 85;
                case CardId.Diabellze:
                    return 10;
                case CardId.BlackChaos:
                    return 100;
                case CardId.Alligator:
                    if (held || alligatorSummonUsed)
                        return 35;
                    return Bot.Hand.Any(MentionsWizard) ? 95 : 60;
                case CardId.Panther:
                    return held ? 30 : 55;
                case CardId.Jinzo:
                    return 15;
                case CardId.DarkTimeWizard:
                    return held ? 35 : 90;
                case CardId.Scapegoats:
                    return held || Bot.HasInSpellZone(CardId.Scapegoats) ? 30 : 85;
                case CardId.Graverobber:
                    return held || Bot.HasInSpellZone(CardId.Graverobber) ? 20 : 65;
                case CardId.ReversalBox:
                    return held || Bot.HasInSpellZone(CardId.ReversalBox) ? 10 : 55;
                case CardId.Dice:
                    return held ? 25 : 50;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// How readily a hand card is discarded: cards that pay back from the GY first.
        /// </summary>
        private int DiscardScore(ClientCard card)
        {
            bool duplicate = HandCount(card.Id) >= 2;
            switch (card.Id)
            {
                case CardId.SkullArchfiend:
                    return SkullTriggerLive() ? 100 : 40;
                case CardId.Dice:
                    return 70;
                case CardId.Diabellze:
                    return 65;
                case CardId.Graverobber:
                    return 55;
                case CardId.BlackChaos:
                    return Bot.HasInHand(CardId.Ritual) || Bot.HasInGraveyard(CardId.Ritual) || SkullTriggerLive() ? 15 : 50;
                case CardId.Ritual:
                    return Bot.HasInHand(CardId.BlackChaos) ? 5 : 35;
                case CardId.Jinzo:
                    return 45;
                case CardId.ReversalBox:
                    return 40;
                case CardId.SkullMeister:
                    return 35;
                case _CardId.BookOfMoon:
                    return 30;
                case CardId.Mirror:
                    // In the GY she is the negate; a second copy is next turn's starter.
                    return Bot.HasInGraveyard(CardId.Mirror) || Bot.HasInMonstersZone(CardId.Mirror) ? 28 : 45;
                case CardId.Gazelle:
                    return duplicate ? 50 : 20;
                case CardId.Berfomet:
                case CardId.Apprentice:
                    return duplicate ? 50 : 25;
                case CardId.Alligator:
                    return duplicate ? 50 : 15;
                case CardId.Panther:
                    return duplicate ? 50 : 30;
                case CardId.Scapegoats:
                    return duplicate ? 45 : 15;
                case CardId.ChimeraFusion:
                    return duplicate ? 35 : 5;
                case CardId.DarkTimeWizard:
                    return duplicate ? 30 : 5;
                default:
                    return 20;
            }
        }

        /// <summary>
        /// Worth of Special Summoning a monster from the hand, GY or banishment.
        /// </summary>
        private int ReviveScore(ClientCard card)
        {
            if (card.Controller == 1)
                return 40 + Math.Max(card.Data != null ? card.Data.Attack : card.Attack, 0) / 100;
            switch (card.Id)
            {
                case CardId.IllusionBeast:
                    return 95;
                case CardId.BlackChaos:
                    return 90;
                case CardId.Berfomet:
                    if (berfometSearchUsed)
                        return 30;
                    return Bot.GetCardCountInDeck(CardId.Gazelle) + Bot.GetCardCountInDeck(CardId.ChimeraFusion) > 0 ? 85 : 35;
                case CardId.Jinzo:
                    return 80;
                case CardId.Alligator:
                    return alligatorSearchUsed ? 35 : 80;
                case CardId.Gazelle:
                    return gazelleSearchUsed ? 25 : 75;
                case CardId.Mirror:
                    return !mirrorTributeUsed && Bot.GetCardCountInDeck(CardId.Berfomet) + Bot.GetCardCountInDeck(CardId.Gazelle) > 0 ? 70 : 45;
                case CardId.Apprentice:
                    return apprenticeSearchUsed ? 40 : 65;
                case CardId.MoaRegina:
                    return 66;
                case CardId.Diabellze:
                    return 62;
                case CardId.SkullArchfiend:
                    return 60;
                case CardId.SolErysichthon:
                case CardId.Magnum:
                case CardId.Dragostapelia:
                    return 55;
                case CardId.ChimeraKing:
                case CardId.BerfometKing:
                    return 50;
                case CardId.Panther:
                    return 45;
                case CardId.SkullMeister:
                    return 30;
                default:
                    return 20;
            }
        }

        /// <summary>
        /// How much we would rather keep a monster than spend it as Fusion Material.
        /// Negative when sending it to the GY as material pays back.
        /// </summary>
        private int MaterialCost(ClientCard card)
        {
            bool onField = card.Location == CardLocation.MonsterZone;
            if (onField && card.Controller == 0 && card.Owner == 1)
                return 5;
            if (IsToken(card))
                return 5;
            switch (card.Id)
            {
                case CardId.Gazelle:
                    if (!gazelleMaterialUsed)
                        return onField ? -15 : -10;
                    return onField ? 10 : 20;
                case CardId.Berfomet:
                    if (!berfometMaterialUsed)
                        return onField ? -10 : -5;
                    return onField ? 10 : 20;
                case CardId.Mirror:
                    if (onField)
                        return mirrorTributeUsed ? 15 : 40;
                    return normalSummonUsed && !Bot.HasInGraveyard(CardId.Mirror) && !Bot.HasInMonstersZone(CardId.Mirror) ? 5 : 30;
                case CardId.Apprentice:
                    if (onField)
                        return 20;
                    return apprenticeSummonUsed ? 15 : 35;
                case CardId.Diabellze:
                    return onField ? 60 : 0;
                case CardId.SkullArchfiend:
                    if (onField)
                        return 45;
                    return SkullTriggerLive() ? -20 : 20;
                case CardId.SkullMeister:
                    return onField ? 15 : 35;
                case CardId.Alligator:
                    return onField ? 20 : 40;
                case CardId.Panther:
                    if (onField)
                        return pantherUsed ? 25 : 45;
                    return 40;
                case CardId.Jinzo:
                    return onField ? 70 : 25;
                case CardId.ChimeraKing:
                case CardId.BerfometKing:
                    return 15;
                case CardId.SolErysichthon:
                    return 60;
                case CardId.MoaRegina:
                    return 70;
                case CardId.Magnum:
                case CardId.Dragostapelia:
                    return 80;
                case CardId.BlackChaos:
                    return onField ? 300 : 60;
                case CardId.IllusionBeast:
                case CardId.Restrict:
                case CardId.Exceed:
                    return 300;
                default:
                    return 30;
            }
        }

        private int FusionTargetValue(int target)
        {
            List<ClientCard> enemyCards = Enemy.GetMonsters().Concat(Enemy.GetSpells()).ToList();
            int bestRemoval = enemyCards.Select(card => RemovalScore(card)).DefaultIfEmpty(0).Max();
            bool needChimera = !HasChimeraAvailable();
            switch (target)
            {
                case CardId.IllusionBeast:
                    return 90;
                case CardId.BerfometKing:
                    return (berfometKingUsed ? 30 : 75) + (needChimera ? 20 : 0);
                case CardId.ChimeraKing:
                    return (chimeraKingUsed || Enemy.GetHandCount() == 0 ? 30 : 70) + (needChimera ? 20 : 0);
                case CardId.SolErysichthon:
                    if (bestRemoval >= 200 || (bestRemoval > 100 && EnemyBestAttack() > Util.GetBestAttack(Bot)))
                        return 100;
                    if (bestRemoval >= 75)
                        return 65;
                    return 20;
                case CardId.Restrict:
                    if (!Enemy.GetMonsters().Any(card => card.IsFaceup()))
                        return 0;
                    return EnemyBestAttack() >= 2500 ? 80 : 55;
                case CardId.MoaRegina:
                    return 50;
                case CardId.Dragostapelia:
                    return 45;
                case CardId.Magnum:
                    return 40;
                default:
                    return 0;
            }
        }

        private static bool FitsFusion(int target, ClientCard first, ClientCard second)
        {
            switch (target)
            {
                case CardId.IllusionBeast:
                    return OnFieldFaceup(first) && IsChimera(first) && IsIllusion(second) && !IsChimera(second);
                case CardId.BerfometKing:
                    return (IsBeast(first) || IsFiend(first) || IsIllusion(first))
                        && (IsBeast(second) || IsFiend(second) || IsIllusion(second))
                        && RaceOf(first) != RaceOf(second);
                case CardId.ChimeraKing:
                    return IsBeast(first) && IsFiend(second);
                case CardId.SolErysichthon:
                    return IsIllusion(first) && IsFiend(second);
                case CardId.Restrict:
                    return (IsFiend(first) || IsIllusion(first) || IsSpellcaster(first))
                        && (IsFiend(second) || IsIllusion(second) || IsSpellcaster(second))
                        && RaceOf(first) != RaceOf(second);
                case CardId.MoaRegina:
                    return IsIllusion(first) && IsFiend(second) && LevelOf(second) >= 6;
                case CardId.Dragostapelia:
                    return IsFusionMonster(first) && IsDark(second);
                case CardId.Magnum:
                    return OnFieldFaceup(first) && IsFusionMonster(first) && second.Location == CardLocation.Hand;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Best Chimera Fusion play with the monsters in hand and on the field: the target's
        /// worth minus what its two materials cost us. Chimera Fusion itself needs a Beast or
        /// Fiend among the materials.
        /// </summary>
        private FusionPlan BuildFusionPlan()
        {
            List<ClientCard> pool = Bot.GetMonsters().Where(card => card.IsFaceup())
                .Concat(Bot.Hand.Where(IsMonsterCard)).ToList();
            bool zoneFree = FreeMonsterZones() > 0;
            FusionPlan best = null;
            foreach (int target in FusionTargets)
            {
                if (!Bot.HasInExtra(target))
                    continue;
                int value = FusionTargetValue(target);
                if (value <= 0)
                    continue;
                foreach (ClientCard first in pool)
                {
                    foreach (ClientCard second in pool)
                    {
                        if (first == second || !FitsFusion(target, first, second))
                            continue;
                        if (!IsBeast(first) && !IsFiend(first) && !IsBeast(second) && !IsFiend(second))
                            continue;
                        if (!zoneFree && first.Location != CardLocation.MonsterZone && second.Location != CardLocation.MonsterZone)
                            continue;
                        int score = value - MaterialCost(first) - MaterialCost(second);
                        // Detonator answers the activation by destroying a material on the field.
                        if (DetonatorLive() && (OnFieldFaceup(first) || OnFieldFaceup(second)))
                            score -= 40;
                        if (best == null || score > best.Score)
                            best = new FusionPlan { TargetId = target, First = first, Second = second, Score = score };
                    }
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- chain responses

        private bool MeisterNegate()
        {
            return Card.Location == CardLocation.Hand && Duel.LastChainPlayer == 1;
        }

        // Against an Extra Deck engine the one negate is kept for the Extra Deck monsters,
        // whose effects are the wipes and the pops, instead of the first main-deck starter.
        private bool MirrorNegateWorthIt()
        {
            if (Duel.LastChainPlayer != 1)
                return false;
            if (Enemy.GetMonsters().Any(card => card.IsFaceup() && card.IsExtraCard()))
                enemyUsesExtraDeck = true;
            ClientCard source = Duel.GetCurrentChainCard();
            if (source == null || source.Controller != 1 || !enemyUsesExtraDeck)
                return true;
            return source.IsExtraCard() || Duel.Phase >= DuelPhase.BattleStart;
        }

        private bool MirrorNegateGrave()
        {
            return Card.Location == CardLocation.Grave && MirrorNegateWorthIt();
        }

        private bool MirrorNegateField()
        {
            return Card.Location == CardLocation.MonsterZone && ActivateDescription == Util.GetStringId(CardId.Mirror, 1)
                && MirrorNegateWorthIt();
        }

        private bool BoxEffect()
        {
            return Card.Location == CardLocation.SpellZone && Card.IsFaceup();
        }

        private bool BoxFlip()
        {
            return Card.Location == CardLocation.SpellZone && Card.IsFacedown() && Duel.LastChainPlayer != 0;
        }

        private bool MagnumDestroy()
        {
            if (ActivateDescription != Util.GetStringId(CardId.Magnum, 1) || Duel.LastChainPlayer != 1)
                return false;
            return Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any(card => RemovalScore(card) >= 75);
        }

        private bool DragostapeliaCounter()
        {
            if (Duel.LastChainPlayer != 1)
                return false;
            ClientCard source = Duel.GetCurrentChainCard();
            return source != null && source.Controller == 1 && source.Location == CardLocation.MonsterZone && source.IsFaceup();
        }

        private bool DiceGrave()
        {
            if (Card.Location != CardLocation.Grave)
                return false;
            bool strongSummon = Duel.LastSummonedCards.Any(card => card != null && card.Controller == 1 && card.IsFaceup()
                && (card.Attack >= 2000 || card.IsExtraCard()));
            return strongSummon || Util.GetTotalAttackingMonsterAttack(1) >= Bot.LifePoints;
        }

        private bool DiceDefence()
        {
            if (Card.Location == CardLocation.Grave || Duel.Player != 1 || diceUsed)
                return false;
            if (!Bot.UnderAttack || Enemy.BattlingMonster == null || !IsAttackWorthStopping(Enemy.BattlingMonster))
                return false;
            diceUsed = true;
            return true;
        }

        private bool BookOfMoonOpponentTurn()
        {
            return Duel.Player == 1 && DefaultBookOfMoon();
        }

        private bool ScapegoatsOpponentTurn()
        {
            if (!IsOpponentTurnWindow() || Duel.Phase < DuelPhase.Main1 || Duel.Phase >= DuelPhase.End)
                return false;
            int free = FreeMonsterZones();
            if (free <= 0)
                return false;
            bool panther = Enemy.GetMonsterCount() > 0 && Bot.GetCardCountInDeck(CardId.Panther) > 0 && free >= 2;
            bool battle = Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2;
            bool threatened = Bot.UnderAttack || (battle && Util.GetTotalAttackingMonsterAttack(1) >= Bot.LifePoints);
            // Tokens brought out in the Main Phase feed board wipes that count our cards, so
            // they wait for the Battle Phase unless Jinzo is wanted early against a backrow.
            bool early = Enemy.GetSpellCountWithoutField() >= 2 || Util.IsChainTarget(Card);
            if (!threatened && !(panther && (battle || early)))
                return false;
            AI.SelectNumber(panther ? Math.Min(free - 1, 3) : Math.Min(free, 4));
            return true;
        }

        private bool GraverobberTrap()
        {
            if (Card.Location != CardLocation.SpellZone || FreeMonsterZones() <= 0)
                return false;
            if (Duel.Player == 1)
                return Duel.LastChainPlayer != 0 && Duel.Phase >= DuelPhase.Main1;
            return IsMyMainPhaseIdle();
        }

        private bool WizardOpponentTurn()
        {
            if (!IsOpponentTurnWindow() || wizardCoinUsed || Card.Location != CardLocation.SpellZone)
                return false;
            if (Bot.GetMonsters().Any(card => !IsToken(card)) || !ShouldTossCoin())
                return false;
            if (Enemy.GetMonsterCount() < 2 && !Bot.UnderAttack)
                return false;
            wizardWantsCoin = true;
            wizardCoinUsed = true;
            return true;
        }

        private bool ReviveWorthIt(IEnumerable<ClientCard> candidates)
        {
            if (!IsOpponentTurnWindow() || FreeMonsterZones() <= 0)
                return false;
            return candidates.Any(card => card != Card && !card.IsFacedown()
                && (IsBeast(card) || IsFiend(card) || IsIllusion(card)) && ReviveScore(card) >= 50);
        }

        private bool ChimeraKingRevive()
        {
            if (Card.Location != CardLocation.Grave || !ReviveWorthIt(Bot.Graveyard))
                return false;
            // Chimera Fusion only returns from the GY while a "Chimera" is on the field or in the GY.
            bool lastChimera = !HasChimeraOnField() && Bot.Graveyard.Count(IsChimera) <= 1;
            if (lastChimera && Bot.HasInGraveyard(CardId.ChimeraFusion) && !Bot.UnderAttack)
                return false;
            return true;
        }

        private bool BerfometKingRevive()
        {
            return Card.Location == CardLocation.Grave && ReviveWorthIt(Bot.Banished);
        }

        private bool TokenReplace()
        {
            return ActivateDescription == AskDestroyReplace || ActivateDescription <= 0;
        }

        // ---------------------------------------------------------------- triggers

        private bool GazelleEffect()
        {
            if (Card.Location == CardLocation.Grave)
                gazelleMaterialUsed = true;
            else
                gazelleSearchUsed = true;
            return true;
        }

        private bool BerfometEffect()
        {
            if (Card.Location == CardLocation.Grave)
                berfometMaterialUsed = true;
            else
                berfometSearchUsed = true;
            return true;
        }

        private bool ApprenticeSearch()
        {
            apprenticeSearchUsed = true;
            return true;
        }

        private bool AlligatorSearch()
        {
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            alligatorSearchUsed = true;
            return true;
        }

        private bool SkullTrigger()
        {
            if (Card.Location != CardLocation.Grave || ActivateDescription == Util.GetStringId(CardId.SkullArchfiend, 0))
                return false;
            skullTriggerUsed = true;
            return true;
        }

        private bool BlackChaosRecover()
        {
            return ActivateDescription != Util.GetStringId(CardId.BlackChaos, 1);
        }

        private bool ChimeraKingDiscard()
        {
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            chimeraKingUsed = true;
            return true;
        }

        private bool BerfometKingSend()
        {
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            berfometKingUsed = true;
            return true;
        }

        // Her trigger destroys one of our cards with one of theirs: paid with a Token or a spent card.
        private int DiabellzeCost(ClientCard card)
        {
            if (card.Location == CardLocation.MonsterZone)
                return IsToken(card) ? 0 : 20 + MaterialCost(card);
            return card.IsFaceup() ? 10 : 60;
        }

        private bool DiabellzeEffect()
        {
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            int cost = Bot.GetMonsters().Concat(Bot.GetSpells()).Where(card => card != Card).Select(card => DiabellzeCost(card)).DefaultIfEmpty(999).Min();
            int gain = Enemy.GetMonsters().Concat(Enemy.GetSpells()).Select(card => RemovalScore(card)).DefaultIfEmpty(0).Max();
            return cost <= 30 && gain >= 100;
        }

        private bool SolEffect()
        {
            return Card.Location == CardLocation.MonsterZone && Enemy.GetFieldCount() > 0;
        }

        private bool RestrictEffect()
        {
            return Card.Location == CardLocation.MonsterZone && Enemy.GetMonsterCount() > 0;
        }

        // ---------------------------------------------------------------- main phase

        private bool WizardCoin()
        {
            if (!IsMyMainPhaseIdle() || wizardCoinUsed || !ShouldTossCoin())
                return false;
            wizardWantsCoin = true;
            wizardCoinUsed = true;
            return true;
        }

        private bool WizardSearch()
        {
            if (!IsMyMainPhaseIdle() || wizardSearchUsed)
                return false;
            int[] targets = WizardCards.Where(id => id != CardId.DarkTimeWizard).ToArray();
            if (Bot.GetCardCountInDeck(targets) == 0)
                return false;
            wizardWantsCoin = false;
            wizardSearchUsed = true;
            return true;
        }

        private bool ExceedSummon()
        {
            bool enemyTribute = Enemy.GetMonsters().Any(card => card.IsFaceup());
            ClientCard own = Bot.GetMonsters().Where(card => card.IsFaceup()).OrderBy(MaterialCost).FirstOrDefault();
            if (!enemyTribute && (own == null || MaterialCost(own) > 20))
                return false;
            exceedSummoning = true;
            tributedThisTurn = true;
            return true;
        }

        private bool AlligatorSummon()
        {
            if (Card.Location != CardLocation.Hand || !IsMyMainPhaseIdle() || FreeMonsterZones() <= 0)
                return false;
            alligatorSummonUsed = true;
            return true;
        }

        private bool ScapegoatsMain()
        {
            if (!IsMyMainPhaseIdle())
                return false;
            int free = FreeMonsterZones();
            if (free < 3)
                return false;
            bool panther = Enemy.GetMonsterCount() > 0 && Bot.GetCardCountInDeck(CardId.Panther) > 0 && !pantherUsed;
            // A Token is a Beast: it completes a Chimera Fusion when the hand has no other Beast.
            List<ClientCard> pool = Bot.GetMonsters().Where(card => card.IsFaceup()).Concat(Bot.Hand.Where(IsMonsterCard)).ToList();
            bool beastForFusion = HasFusionSpell() && !pool.Any(IsBeast) && pool.Any(card => IsFiend(card) || IsIllusion(card));
            if (!panther && !beastForFusion)
                return false;
            AI.SelectNumber(2);
            return true;
        }

        private bool SummonStarter()
        {
            if (StarterShouldWait())
                return false;
            // With the negate unanswered, Gazelle keeps his body when his search is negated;
            // Mirror Swordknight's Tribute would be lost.
            if (CrossLive() && Card.IsCode(CardId.Mirror))
                return false;
            normalSummonUsed = true;
            return true;
        }

        // Set, she cannot be destroyed by battle and takes no damage while the negate is unanswered.
        private bool SetMirrorWall()
        {
            if (!CrossLive() || StarterShouldWait() || Bot.HasInHand(CardId.Gazelle))
                return false;
            normalSummonUsed = true;
            return true;
        }

        private bool ApprenticeBait()
        {
            if (!IsMyMainPhaseIdle() || !CrossLive() || CanAlligatorBait() || !CanApprenticeBait())
                return false;
            if (!Bot.HasInHand(CardId.Mirror) && !Bot.HasInHand(CardId.Gazelle))
                return false;
            apprenticeSummonUsed = true;
            return true;
        }

        // Tributing a spent monster for Berfomet opens the same search as Mirror Swordknight's line.
        private bool SummonBerfomet()
        {
            if (berfometSearchUsed || Bot.GetCardCountInDeck(CardId.Gazelle) + Bot.GetCardCountInDeck(CardId.ChimeraFusion) == 0)
                return false;
            if (StarterShouldWait())
                return false;
            if (!Bot.GetMonsters().Any(card => TributeCost(card) <= 25))
                return false;
            normalSummonUsed = true;
            tributedThisTurn = true;
            return true;
        }

        private int TributeCost(ClientCard card)
        {
            if (IsToken(card))
                return 0;
            if (card.IsFacedown())
                return 30;
            switch (card.Id)
            {
                case CardId.SkullMeister:
                    return 10;
                case CardId.Alligator:
                    return alligatorSearchUsed || !alligatorSummonUsed ? 15 : 60;
                case CardId.Gazelle:
                    return 20;
                case CardId.Apprentice:
                    return 25;
                case CardId.Panther:
                    return pantherUsed ? 25 : 40;
                default:
                    return 100;
            }
        }

        public override IList<ClientCard> OnSelectTribute(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            return cards.OrderBy(TributeCost).Take(min).ToList();
        }

        private bool MirrorTribute()
        {
            if (Card.Location != CardLocation.MonsterZone || ActivateDescription != Util.GetStringId(CardId.Mirror, 0))
                return false;
            bool berfomet = Bot.GetCardCountInDeck(CardId.Berfomet) > 0 && !berfometSearchUsed;
            bool gazelle = Bot.GetCardCountInDeck(CardId.Gazelle) > 0 && !gazelleSearchUsed;
            if (!berfomet && !gazelle)
                return false;
            if (Duel.Player == 0 ? !IsMyMainPhaseIdle() : Duel.LastChainPlayer == 0)
                return false;
            // The Tribute is a cost: negated, Mirror is simply gone. As a body she at least blocks.
            if (CrossLive())
                return false;
            mirrorTributeUsed = true;
            tributedThisTurn = true;
            return true;
        }

        private bool ApprenticeSummon()
        {
            if (!IsMyMainPhaseIdle() || FreeMonsterZones() <= 0)
                return false;
            int best = Bot.Hand.Where(card => card != Card).Select(card => DiscardScore(card)).DefaultIfEmpty(0).Max();
            // With the Normal Summon open and no starter in hand, she fetches Mirror Swordknight.
            bool fetchesStarter = !apprenticeSearchUsed && !normalSummonUsed && Bot.GetCardCountInDeck(CardId.Mirror) > 0
                && !Bot.HasInHand(CardId.Mirror) && !Bot.HasInHand(CardId.Gazelle);
            int needed = apprenticeSearchUsed ? 60 : (fetchesStarter ? 5 : (Bot.GetMonsterCount() == 0 ? 20 : 40));
            if (best < needed)
                return false;
            apprenticeSummonUsed = true;
            return true;
        }

        /// <summary>
        /// How readily a monster is Tributed for Swiftwind Panther Warrior.
        /// </summary>
        private int PantherFodderCost(ClientCard card)
        {
            if (IsToken(card))
                return 0;
            if (card.Location == CardLocation.Hand)
            {
                switch (card.Id)
                {
                    case CardId.SkullArchfiend:
                        return SkullTriggerLive() ? -10 : 25;
                    case CardId.Diabellze:
                        return 5;
                    case CardId.Panther:
                        return 15;
                    case CardId.BlackChaos:
                        return Bot.HasInHand(CardId.Ritual) ? 60 : 25;
                    default:
                        return 40;
                }
            }
            if (card.IsFacedown())
                return 30;
            switch (card.Id)
            {
                case CardId.Gazelle:
                    return gazelleSearchUsed ? 20 : 50;
                case CardId.Berfomet:
                    return berfometSearchUsed ? 20 : 50;
                case CardId.Alligator:
                    return alligatorSearchUsed ? 25 : 60;
                default:
                    return 60;
            }
        }

        private ClientCard PantherFodder(ClientCard panther)
        {
            return Bot.GetMonsters().Concat(Bot.Hand.Where(IsMonsterCard))
                .Where(card => card != panther)
                .OrderBy(PantherFodderCost).FirstOrDefault();
        }

        private bool PantherHasTarget()
        {
            return Bot.GetCardCountInDeck(CardId.Alligator) + Bot.GetCardCountInDeck(CardId.Jinzo) > 0
                || Bot.HasInHand(CardId.Alligator) || Bot.HasInHand(CardId.Jinzo);
        }

        private bool SummonPanther()
        {
            if (pantherUsed || !PantherHasTarget())
                return false;
            ClientCard fodder = PantherFodder(Card);
            if (fodder == null || PantherFodderCost(fodder) > 25)
                return false;
            normalSummonUsed = true;
            return true;
        }

        private bool PantherEffect()
        {
            if (Card.Location != CardLocation.MonsterZone || !PantherHasTarget())
                return false;
            ClientCard fodder = PantherFodder(Card);
            if (fodder == null)
                return false;
            int cost = PantherFodderCost(fodder);
            if (Duel.Player == 0 ? (!IsMyMainPhaseIdle() || cost > 25) : (Duel.LastChainPlayer == 0 || cost > 5))
                return false;
            pantherUsed = true;
            tributedThisTurn = true;
            return true;
        }

        private bool FusionRecover()
        {
            if (Card.Location != CardLocation.Grave || !IsMyMainPhaseIdle())
                return false;
            fusionRecoverUsed = true;
            return true;
        }

        private bool FusionActivate()
        {
            if (Card.Location == CardLocation.Grave)
                return false;
            int needed;
            if (Duel.Player == 0)
            {
                if (!IsMyMainPhaseIdle())
                    return false;
                needed = 40;
            }
            else
            {
                if (Duel.LastChainPlayer == 0)
                    return false;
                needed = 75;
            }
            FusionPlan plan = BuildFusionPlan();
            if (plan == null || plan.Score < needed)
                return false;
            fusionPlanTarget = plan.TargetId;
            fusionPlanMaterials = new List<ClientCard> { plan.First, plan.Second };
            return true;
        }

        private bool RitualRecover()
        {
            if (Card.Location != CardLocation.Grave || !IsMyMainPhaseIdle())
                return false;
            bool summonNow = Bot.HasInHand(CardId.BlackChaos) && Bot.HasInGraveyard(CardId.SkullArchfiend);
            bool summonAgain = Bot.HasInGraveyard(CardId.BlackChaos) && !Bot.HasInHand(CardId.BlackChaos)
                && !Bot.HasInMonstersZone(CardId.BlackChaos);
            return summonNow || summonAgain;
        }

        private bool RitualActivate()
        {
            if (Card.Location == CardLocation.Grave || !IsMyMainPhaseIdle())
                return false;
            ClientCard blackChaos = Bot.Hand.FirstOrDefault(card => card.IsCode(CardId.BlackChaos));
            List<ClientCard> pool = Bot.Graveyard.Where(IsMonsterCard)
                .Concat(Bot.Hand.Where(card => card != blackChaos && IsMonsterCard(card)))
                .Concat(Bot.GetMonsters().Where(card => card.IsFaceup())).ToList();
            List<ClientCard> tributes = BestRitualTributes(pool);
            if (tributes == null || tributes.Sum(card => RitualCost(card)) > 70)
                return false;
            ritualPlan.Clear();
            return true;
        }

        /// <summary>
        /// Cheapest set of up to three monsters whose Levels reach Black Chaos's 8 with no
        /// card to spare, as the Ritual requires.
        /// </summary>
        private List<ClientCard> BestRitualTributes(IList<ClientCard> pool)
        {
            const int needed = 8;
            List<ClientCard> best = null;
            int bestCost = int.MaxValue;
            for (int size = 1; size <= 3; ++size)
            {
                foreach (IEnumerable<ClientCard> combination in pool.GetCombinations(size))
                {
                    List<ClientCard> set = combination.ToList();
                    int total = set.Sum(card => LevelOf(card));
                    if (total < needed || set.Any(card => total - LevelOf(card) >= needed))
                        continue;
                    int cost = set.Sum(card => RitualCost(card));
                    if (cost < bestCost)
                    {
                        best = set;
                        bestCost = cost;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// Worth of returning a card to the Deck for Skull Archfiend of Chaos. One of the
        /// three must mention "Light and Darkness Ritual", so those score highest.
        /// </summary>
        private int ReturnScore(ClientCard card)
        {
            if (card.Controller == 1)
                return card.Location == CardLocation.Grave ? 60 : 15;
            if (card.IsCode(CardId.Ritual))
                return 100;
            if (card.IsCode(CardId.BlackChaos))
                return 90;
            if (card.Location != CardLocation.Grave)
                return 45;
            switch (card.Id)
            {
                case CardId.DarkTimeWizard:
                    return wizardSearchUsed ? -50 : 40;
                case CardId.ChimeraFusion:
                    return fusionRecoverUsed ? 30 : -60;
                case CardId.ChimeraKing:
                case CardId.BerfometKing:
                case CardId.IllusionBeast:
                    return -70;
                case CardId.Mirror:
                    return -50;
                case CardId.Dice:
                    return -40;
                case CardId.Graverobber:
                    return -30;
                case CardId.Scapegoats:
                    return 40;
                case CardId.Gazelle:
                case CardId.Berfomet:
                case CardId.Apprentice:
                case CardId.Alligator:
                case CardId.Panther:
                    return 35;
                case CardId.Diabellze:
                    return 10;
                default:
                    return 25;
            }
        }

        private bool SkullSummon()
        {
            if (Card.Location != CardLocation.Hand && ActivateDescription != Util.GetStringId(CardId.SkullArchfiend, 0))
                return false;
            if (!IsMyMainPhaseIdle() || FreeMonsterZones() <= 0)
                return false;
            List<ClientCard> pool = Bot.Graveyard.Concat(Bot.Banished).Concat(Enemy.Graveyard).Concat(Enemy.Banished)
                .Where(card => card != null && card != Card && !card.IsFacedown()).ToList();
            return pool.Any(card => card.Controller == 0 && card.IsCode(CardId.Ritual, CardId.BlackChaos))
                && pool.Count(card => ReturnScore(card) >= 15) >= 3;
        }

        private bool BlackChaosBanish()
        {
            return ActivateDescription == Util.GetStringId(CardId.BlackChaos, 1) && Enemy.GetFieldCount() > 0;
        }

        private bool MoaRevive()
        {
            return ActivateDescription == Util.GetStringId(CardId.MoaRegina, 0);
        }

        private int SetFromGraveScore(ClientCard card)
        {
            if (card.Controller == 1)
                return 10;
            switch (card.Id)
            {
                case CardId.Scapegoats:
                    return 90;
                case CardId.DarkTimeWizard:
                    return wizardSearchUsed && Bot.Graveyard.Count(c => c.IsCode(CardId.DarkTimeWizard)) <= 1 ? 0 : 80;
                case CardId.ReversalBox:
                    return 60;
                case CardId.Dice:
                    return 50;
                default:
                    return 0;
            }
        }

        private bool GraverobberGrave()
        {
            if (Card.Location != CardLocation.Grave || !IsMyMainPhaseIdle() || Bot.GetSpellCountWithoutField() >= 4)
                return false;
            return Bot.Graveyard.Any(card => card != Card && SetFromGraveScore(card) >= 50);
        }

        // Face-down, Detonator cannot answer our plays and Exciton Knight cannot wipe in our Battle Phase.
        private bool BookOfMoonBreaker()
        {
            if (!IsMyMainPhaseIdle() || Enemy.HasInMonstersZone(CardId.StarliegePhotonBlastDragon, true, false, true))
                return false;
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsDisabled() && card.HasXyzMaterial()
                    && card.IsCode(CardId.RyzealDetonator, CardId.ExcitonKnight))
                .OrderByDescending(card => card.IsCode(CardId.RyzealDetonator) ? 1 : 0).FirstOrDefault();
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // Face-down, a monster we cannot beat in attack falls to its lower DEF.
        private bool BookOfMoonMain()
        {
            if (!IsMyMainPhaseIdle() || Duel.Phase != DuelPhase.Main1 || Duel.Turn <= 1)
                return false;
            int best = Bot.GetMonsters().Where(card => card.IsFaceup() && card.IsAttack()).Select(card => card.Attack).DefaultIfEmpty(0).Max();
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.HasType(CardType.Link) && !card.IsShouldNotBeTarget()
                    && card.GetDefensePower() >= best && card.Defense < best)
                .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        /// <summary>
        /// The roll averages 7, so 1400 ATK: used before battle when that swing turns a
        /// battle we cannot win into one we expect to win.
        /// </summary>
        private bool DiceMain()
        {
            if (Card.Location == CardLocation.Grave || !IsMyMainPhaseIdle() || diceUsed)
                return false;
            if (Duel.Phase != DuelPhase.Main1 || Duel.Turn <= 1)
                return false;
            List<ClientCard> attackers = Bot.GetMonsters().Where(card => card.IsFaceup() && card.IsAttack() && card.Attack > 0).ToList();
            List<ClientCard> blockers = Enemy.GetMonsters().Where(card => card.IsFaceup()).ToList();
            if (attackers.Count == 0 || blockers.Count == 0)
                return false;
            bool swing = blockers.Any(blocker => attackers.All(attacker => attacker.Attack <= blocker.GetDefensePower())
                && attackers.Any(attacker => attacker.Attack + (MentionsWizard(attacker) ? 1400 : 0) > blocker.GetDefensePower() - 1400));
            if (!swing)
                return false;
            diceUsed = true;
            return true;
        }

        private bool SummonBody()
        {
            if (Bot.GetMonsterCount() > 0 || EnemyBestAttack() >= Card.Attack)
                return false;
            normalSummonUsed = true;
            return true;
        }

        private bool SetBody()
        {
            if (Bot.GetMonsterCount() > 0)
                return false;
            normalSummonUsed = true;
            return true;
        }

        // In hand she answers GY effects; she is only set as a blocker when the board is about to kill us.
        private bool SetWall()
        {
            if (Bot.GetMonsterCount() > 0 || Util.GetTotalAttackingMonsterAttack(1) * 2 < Bot.LifePoints)
                return false;
            normalSummonUsed = true;
            return true;
        }

        private bool ReposCheck()
        {
            if (Card.IsFacedown() || IsToken(Card))
                return DefaultMonsterRepos();
            bool wall = Card.IsCode(CardId.Mirror, CardId.Apprentice);
            if (Duel.Turn == 1 || Duel.Phase == DuelPhase.Main2)
                return Card.IsAttack() && (wall || Card.Attack < 2000);
            if (Card.IsDefense())
            {
                if (Card.Attack < 1500 || (Card.IsCode(CardId.Panther) && !tributedThisTurn))
                    return false;
                if (Enemy.GetMonsterCount() == 0)
                    return true;
                return Enemy.GetMonsters().Any(enemy => enemy.IsFaceup() && Card.Attack > enemy.GetDefensePower())
                    && Card.Attack >= EnemyBestAttack();
            }
            return DefaultMonsterRepos();
        }

        private bool SpellSetCheck()
        {
            if (Bot.GetSpellCountWithoutField() >= 4)
                return false;
            switch (Card.Id)
            {
                case _CardId.BookOfMoon:
                case CardId.Graverobber:
                case CardId.Scapegoats:
                case CardId.Dice:
                    return true;
                case CardId.ReversalBox:
                    return !Bot.HasInSpellZone(CardId.ReversalBox);
                case CardId.DarkTimeWizard:
                    return wizardSearchUsed && Bot.GetMonsters().All(IsToken);
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- selections

        public override bool OnSelectYesNo(int desc)
        {
            if (desc == Util.GetStringId(CardId.Scapegoats, 2) || desc == Util.GetStringId(CardId.Graverobber, 2))
                return true;
            return base.OnSelectYesNo(desc);
        }

        public override int OnSelectOption(IList<int> options)
        {
            int wizardSearch = Util.GetStringId(CardId.DarkTimeWizard, 1);
            int wizardCoin = Util.GetStringId(CardId.DarkTimeWizard, 2);
            if (options.Contains(wizardSearch) || options.Contains(wizardCoin))
            {
                int index = options.IndexOf(wizardWantsCoin ? wizardCoin : wizardSearch);
                return index >= 0 ? index : 0;
            }
            int pantherSummon = options.IndexOf(Util.GetStringId(CardId.Panther, 2));
            if (pantherSummon >= 0)
                return pantherSummon;
            int fusionToHand = options.IndexOf(Util.GetStringId(CardId.ChimeraFusion, 2));
            if (fusionToHand >= 0)
                return fusionToHand;
            return base.OnSelectOption(options);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (AI.HaveSelectedPosition())
                return base.OnSelectPosition(cardId, positions);
            NamedCard data = NamedCard.Get(cardId);
            if (data == null || !positions.Contains(CardPosition.FaceUpDefence) || !positions.Contains(CardPosition.FaceUpAttack))
                return base.OnSelectPosition(cardId, positions);

            // Neither is destroyed by battle, so in defence they take no damage either.
            bool wall = cardId == CardId.Mirror || cardId == CardId.Apprentice;
            if (Duel.Player == 1)
                return wall || data.Attack < 2400 || data.Attack <= EnemyBestAttack() ? CardPosition.FaceUpDefence : CardPosition.FaceUpAttack;
            if (Duel.Turn == 1 || Duel.Phase == DuelPhase.Main2)
                return wall || data.Attack < 2000 ? CardPosition.FaceUpDefence : CardPosition.FaceUpAttack;

            if (data.Attack < 2000 && cardId != CardId.Alligator && Enemy.GetMonsters().Any(card => card.IsFaceup() && card.Attack >= data.Attack))
                return CardPosition.FaceUpDefence;
            CardPosition safer = SummonPosition.DefenceIfExposed(Duel, cardId, positions);
            return safer != 0 ? safer : CardPosition.FaceUpAttack;
        }

        public override IList<ClientCard> OnSelectRitualTribute(IList<ClientCard> cards, IList<ClientCard> mandatoryCards,
            int sum, int min, int max, bool exactEqual)
        {
            // Preference order only: the caller completes it into a legal Tribute set.
            return cards.OrderBy(RitualCost).ToList();
        }

        private int RitualCost(ClientCard card)
        {
            if (card.Location == CardLocation.Grave)
            {
                switch (card.Id)
                {
                    case CardId.ChimeraKing:
                    case CardId.BerfometKing:
                        return 60;
                    case CardId.Mirror:
                        return 40;
                    case CardId.SkullArchfiend:
                        return 30;
                    case CardId.IllusionBeast:
                        return 25;
                    case CardId.Jinzo:
                        return 15;
                    default:
                        return 5;
                }
            }
            if (card.Location == CardLocation.Hand)
            {
                switch (card.Id)
                {
                    case CardId.Diabellze:
                        return 6;
                    case CardId.SkullArchfiend:
                        return 12;
                    case CardId.Jinzo:
                        return 20;
                    default:
                        return 35;
                }
            }
            return IsToken(card) ? 45 : 50 + MaterialCost(card);
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
            int source = solving != null ? solving.ActivateId : (activating != null ? activating.Id : 0);

            switch (hint)
            {
                case HintMsg.FusionMaterial:
                    return PickFusionMaterial(cards, min);
                case HintMsg.SpSummon:
                    if (cards.All(card => card.Location == CardLocation.Extra))
                    {
                        if (cards.Any(card => card.IsCode(fusionPlanTarget)))
                            return PickTop(cards, min, 1, card => card.IsCode(fusionPlanTarget) ? 1 : 0);
                        fusionPlanMaterials.Clear();
                        return PickTop(cards, min, 1, card => FusionTargetValue(card.Id));
                    }
                    if (source == CardId.Mirror)
                        return PickTop(cards, min, 1, card => card.IsCode(CardId.Berfomet) == !berfometSearchUsed ? 2 : 1);
                    if (source == CardId.Panther || source == CardId.ReversalBox)
                        return PickTop(cards, min, 1, WizardMonsterScore);
                    if (cards.All(card => card.Location != CardLocation.Deck))
                        return PickTop(cards, min, 1, ReviveScore);
                    break;
                case HintMsg.AddToHand:
                    if (cards.All(card => card.Location == CardLocation.Deck))
                        return PickTop(cards, min, 1, SearchScore);
                    if (source == CardId.BlackChaos)
                        return PickTop(cards, min, 1, SpellRecoverScore);
                    if (source == CardId.Ritual)
                        return PickTop(cards, min, 1, card => card.IsCode(CardId.BlackChaos) ? 2 : (card.IsCode(CardId.SkullArchfiend) ? 1 : 0));
                    break;
                case HintMsg.Discard:
                    if (cards.All(card => card.Controller == 0 && card.Location == CardLocation.Hand))
                        return PickTop(cards, min, min, DiscardScore);
                    break;
                case HintMsg.Release:
                    if (source == CardId.Panther)
                        return PickTop(cards, min, min, card => -PantherFodderCost(card));
                    if (source == CardId.Ritual)
                        return PickRitualTribute(cards, min);
                    if (source == 0 && exceedSummoning)
                    {
                        exceedSummoning = false;
                        return PickTop(cards, min, 1, card => card.Controller == 1 ? 1000 + card.Attack : -MaterialCost(card));
                    }
                    break;
                case HintMsg.ToGrave:
                    if (source == CardId.BerfometKing)
                        return PickTop(cards, min, min, MillScore);
                    if (source == CardId.Graverobber)
                        return PickTop(cards, min, min, GraverobberSendScore);
                    if (source == CardId.SolErysichthon)
                        return PickTop(cards, min, min, RemovalScore);
                    if (source == CardId.Panther)
                        return PickTop(cards, min, min, card => card.IsCode(CardId.Dice) ? 2 : (card.IsCode(CardId.Graverobber) ? 1 : 0));
                    break;
                case HintMsg.ToDeck:
                    if (source == CardId.SkullArchfiend)
                        return PickTop(cards, min, Math.Max(min, 1), ReturnScore);
                    break;
                case HintMsg.Destroy:
                    if (source == CardId.Diabellze && cards.All(card => card.Controller == 0))
                        return PickTop(cards, min, min, card => -DiabellzeCost(card));
                    goto case HintMsg.Remove;
                case HintMsg.Remove:
                case HintMsg.Equip:
                case HintMsg.Faceup:
                    if (cards.Any(card => card.Controller == 1))
                    {
                        if (source == CardId.Dragostapelia && activating != null)
                        {
                            ClientCard answered = Duel.CurrentChain.LastOrDefault(card => card != null && card.Controller == 1 && cards.Contains(card));
                            if (answered != null)
                                return new List<ClientCard> { answered };
                        }
                        return PickTop(cards, min, Math.Max(min, 1), RemovalScore);
                    }
                    break;
                case HintMsg.Set:
                    if (source == CardId.Graverobber)
                        return PickTop(cards, min, min, SetFromGraveScore);
                    break;
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private int WizardMonsterScore(ClientCard card)
        {
            bool backrow = Enemy.GetSpells().Any(spell => spell.IsFacedown() || spell.IsTrap());
            switch (card.Id)
            {
                case CardId.Jinzo:
                    return backrow || alligatorSearchUsed ? 90 : 70;
                case CardId.Alligator:
                    return alligatorSearchUsed ? 40 : 80;
                default:
                    return 30;
            }
        }

        private int SpellRecoverScore(ClientCard card)
        {
            switch (card.Id)
            {
                case CardId.ChimeraFusion:
                    return fusionRecoverUsed || !HasChimeraAvailable() ? 90 : 55;
                case CardId.DarkTimeWizard:
                    return wizardSearchUsed ? 30 : 80;
                case CardId.Scapegoats:
                    return 70;
                case _CardId.BookOfMoon:
                    return 60;
                case CardId.Dice:
                    return 40;
                default:
                    return 10;
            }
        }

        private int MillScore(ClientCard card)
        {
            switch (card.Id)
            {
                case CardId.SkullArchfiend:
                    return SkullTriggerLive() ? 100 : 40;
                case CardId.Mirror:
                    return Bot.HasInGraveyard(CardId.Mirror) ? 35 : 80;
                case CardId.Gazelle:
                case CardId.Berfomet:
                    return 30;
                case CardId.Apprentice:
                    return 20;
                default:
                    return 5;
            }
        }

        private int GraverobberSendScore(ClientCard card)
        {
            bool backrow = Enemy.GetSpells().Any(spell => spell.IsFacedown() || spell.IsTrap());
            bool strongRevive = Bot.Graveyard.Concat(Enemy.Graveyard).Any(c => IsMonsterCard(c) && ReviveScore(c) >= 85);
            switch (card.Id)
            {
                case CardId.Jinzo:
                    return backrow ? 95 : 75;
                case CardId.Alligator:
                    return alligatorSearchUsed ? 30 : 80;
                case CardId.Dice:
                    return strongRevive ? 90 : 60;
                case CardId.Panther:
                    return 40;
                default:
                    return 20;
            }
        }

        private IList<ClientCard> PickRitualTribute(IList<ClientCard> cards, int min)
        {
            if (!ritualPlan.Any(cards.Contains))
            {
                if (min == 0)
                    return new List<ClientCard>();
                ritualPlan = BestRitualTributes(cards) ?? new List<ClientCard>();
            }
            ClientCard planned = ritualPlan.FirstOrDefault(cards.Contains);
            if (planned == null)
                return PickTop(cards, min, min, card => -RitualCost(card));
            ritualPlan.Remove(planned);
            return new List<ClientCard> { planned };
        }

        private IList<ClientCard> PickFusionMaterial(IList<ClientCard> cards, int min)
        {
            ClientCard planned = fusionPlanMaterials.FirstOrDefault(cards.Contains);
            if (planned != null)
            {
                fusionPlanMaterials.Remove(planned);
                return new List<ClientCard> { planned };
            }
            if (min == 0)
                return new List<ClientCard>();
            return PickTop(cards, min, min, card => -MaterialCost(card));
        }

        private static IList<ClientCard> PickTop(IList<ClientCard> cards, int min, int count, Func<ClientCard, int> score)
        {
            int take = Math.Max(min, Math.Min(Math.Max(count, 1), cards.Count));
            return cards.OrderByDescending(score).Take(take).ToList();
        }
    }
}
