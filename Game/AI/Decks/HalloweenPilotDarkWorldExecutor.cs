using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Dark World player-deck pilot for the Edison format (TCG April 2010 card
    // pool). It plays the deck the way a strong human does.
    //
    // Discard engine. Dark World monsters trigger when they are discarded
    // from the hand by a card effect (Dark World Dealings, Dark World
    // Lightning, Card Destruction). Our own discards go, in order:
    //   1. Goldd / Sillva (Level 5, revive themselves as 2300 beaters),
    //   2. Gren while the opponent has a Spell/Trap (destroys one),
    //   3. Beiige (Level 4, revives itself), 4. Broww (draws 1),
    //   5. Zure / Scarr / a spare Dark Resonator as plain fodder.
    // When the opponent forces the discard Goldd (destroy 2), Sillva (2 cards
    // from their hand to the deck bottom), Reign-Beaux (field wipe) and Broww
    // (draw 2) are upgraded. A revive needs a free Monster Zone; without one
    // Broww is discarded instead. Hand Destruction sends rather than
    // discards, so it only fuels the GY; Brionac's discard is a cost and does
    // not trigger either.
    //
    // Dark Armed Dragon. It drops the moment the GY holds exactly 3 DARK
    // monsters. While it waits in the hand, discards are steered: at 3 DARK
    // only self-reviving Dark World monsters or Spells/Traps are thrown; at 2
    // DARK a non-reviving DARK (Broww first) is thrown to reach 3. Each of its
    // pops banishes the least useful DARK (spare Resonator, Zure, Gren, Scarr,
    // Broww) and keeps Goldd / Sillva / Reign-Beaux as Gateway targets.
    //
    // Synchro lines off Dark Resonator (Level 3 Tuner), normal summoned only
    // when a face-up non-Tuner completes a Synchro:
    //   3 + 5 (Goldd/Sillva) = 8: Dark End Dragon vs. a monster to remove, Red
    //     Dragon Archfiend vs. Defense Position walls or an empty board,
    //     Stardust Dragon vs. a heavy backrow, else Colossal Fighter.
    //   3 + 4 (Beiige/Zure) = 7: Black Rose Dragon (wipe only when behind).
    //   3 + 3 (Broww) = 6: Brionac with a spare card to bounce, Goyo Guardian
    //     vs. a beatable monster, else Gaia Knight.
    //   3 + 2 (Scarr/Gren) = 5: Catastor vs. non-DARK monsters, else Magical
    //     Android.
    // Ancient Sacred Wyvern (LIGHT Tuner) and Drill Warrior (Drill Synchron)
    // cannot be made with this main deck.
    //
    // Turn order: spell/trap removal (Heavy Storm, Dark World Lightning on a
    // face-down card), draw power (Allure, Dealings, Card Destruction after
    // setting traps), Dark Armed Dragon, Synchro climbs, the Normal Summon,
    // then set traps. Gateway to Dark World is set and fired in the
    // opponent's End Phase (or as an emergency blocker) because it locks our
    // own summons for the turn; it revives Goldd / Sillva when a Resonator is
    // waiting, otherwise the biggest Dark World monster. Scarr's battle
    // search takes Gren against backrow, otherwise Broww, then Beiige.
    // Goyo Guardian and Brionac use the pre-errata passcodes required by the
    // Edison whitelist.
    // Deck: AI_PilotDarkWorld.ydk.
    [Deck("PilotDarkWorld", "AI_PilotDarkWorld")]
    public class HalloweenPilotDarkWorldExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Dark World.
            public const int Goldd = 78004197;
            public const int Sillva = 32619583;
            public const int Broww = 79126789;
            public const int Scarr = 5498296;
            public const int Beiige = 33731070;
            public const int Zure = 7459013;
            public const int Gren = 51232472;
            public const int ReignBeaux = 99458769;

            // Other monsters.
            public const int DarkArmedDragon = 65192027;
            public const int DarkResonator = 97021916;

            // Spells.
            public const int DarkWorldDealings = 74117290;
            public const int CardDestruction = 72892473;
            public const int MorphingJar = 33508719;
            public const int ForcesOfDarkness = 29826127;
            public const int GatewayToDarkWorld = 93431518;
            public const int DarkWorldLightning = 93554166;

            // Traps.
            public const int MirrorForce = 44095762;
            public const int BottomlessTrapHole = 29401950;
            public const int DimensionalPrison = 70342110;

            // Extra Deck.
            public const int RedDragonArchfiend = 70902743;
            public const int StardustDragon = 44508094;
            public const int ThoughtRulerArchfiend = 70780151;
            public const int ColossalFighter = 23693634;
            public const int DarkEndDragon = 88643579;
            public const int BlackRoseDragon = 73580471;
            public const int AncientFairyDragon = 25862681;
            public const int GoyoGuardian = 511002994; // PRE-ERRATA passcode (Edison whitelist).
            public const int Brionac = 511002993; // PRE-ERRATA passcode (Edison whitelist).
            public const int MistWurm = 27315304;
            public const int GaiaKnight = 97204936;
            public const int IronChainDragon = 19974580;
            public const int Catastor = 26593852;
            public const int MagicalAndroid = 43385557;
        }

        private static readonly int[] DarkWorldIds =
        {
            CardId.Goldd, CardId.Sillva, CardId.Broww, CardId.Scarr,
            CardId.Beiige, CardId.Zure, CardId.Gren, CardId.ReignBeaux
        };

        // Synchros this main deck can never make (wrong Tuner).
        private static readonly int[] UnreachableSynchros = { };

        // Dark Armed Dragon's banish cost, least useful first.
        private static readonly int[] DarkArmedCostPriority =
        {
            CardId.DarkResonator, CardId.Zure, CardId.Gren, CardId.Scarr,
            CardId.Broww, CardId.Beiige, CardId.DarkArmedDragon,
            CardId.Sillva, CardId.Goldd, CardId.ReignBeaux
        };

        public HalloweenPilotDarkWorldExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Spell/Trap removal before anything is committed.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);
            AddExecutor(ExecutorType.Activate, CardId.DarkWorldLightning, DarkWorldLightningEffect);

            // Draw power and the discard engine.
            AddExecutor(ExecutorType.Activate, _CardId.AllureofDarkness, AllureOfDarknessEffect);
            AddExecutor(ExecutorType.Activate, CardId.DarkWorldDealings, DarkWorldDealingsEffect);
            AddExecutor(ExecutorType.SpellSet, TrapSetBeforeCardDestruction);
            AddExecutor(ExecutorType.Activate, CardId.CardDestruction, CardDestructionEffect);
            AddExecutor(ExecutorType.Activate, CardId.ForcesOfDarkness, ForcesOfDarknessEffect);

            // Dark Armed Dragon.
            AddExecutor(ExecutorType.SpSummon, CardId.DarkArmedDragon);
            AddExecutor(ExecutorType.Activate, CardId.DarkArmedDragon, DarkArmedDragonEffect);

            // Synchro climbs, biggest Level first; the engine only offers the
            // ones whose materials are on the field.
            AddExecutor(ExecutorType.SpSummon, CardId.DarkEndDragon, DarkEndDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.RedDragonArchfiend, RedDragonArchfiendSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.StardustDragon, StardustDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.ColossalFighter);
            AddExecutor(ExecutorType.SpSummon, CardId.ThoughtRulerArchfiend);
            AddExecutor(ExecutorType.SpSummon, CardId.StardustDragon);
            AddExecutor(ExecutorType.SpSummon, CardId.RedDragonArchfiend);
            AddExecutor(ExecutorType.SpSummon, CardId.DarkEndDragon);
            AddExecutor(ExecutorType.SpSummon, CardId.BlackRoseDragon);
            AddExecutor(ExecutorType.SpSummon, CardId.MistWurm, MistWurmSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.AncientFairyDragon);
            AddExecutor(ExecutorType.Activate, CardId.MistWurm);
            AddExecutor(ExecutorType.SpSummon, CardId.Brionac, BrionacSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.GoyoGuardian, GoyoGuardianSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.GaiaKnight);
            AddExecutor(ExecutorType.SpSummon, CardId.IronChainDragon);
            AddExecutor(ExecutorType.SpSummon, CardId.Catastor, CatastorSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.MagicalAndroid);
            AddExecutor(ExecutorType.SpSummon, CardId.Catastor);

            // Synchro effects.
            AddExecutor(ExecutorType.Activate, CardId.DarkEndDragon, DarkEndDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.BlackRoseDragon, BlackRoseDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.Brionac, BrionacEffect);
            AddExecutor(ExecutorType.Activate, CardId.GoyoGuardian);
            AddExecutor(ExecutorType.Activate, CardId.StardustDragon, DefaultStardustDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.ThoughtRulerArchfiend, ThoughtRulerArchfiendEffect);
            AddExecutor(ExecutorType.Activate, CardId.ColossalFighter);

            // Traps and recursion.
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.DimensionalPrison, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, CallOfTheHauntedEffect);
            AddExecutor(ExecutorType.Activate, CardId.GatewayToDarkWorld, GatewayToDarkWorldEffect);

            // The Normal Summon.
            AddExecutor(ExecutorType.Summon, CardId.DarkResonator, DarkResonatorSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Zure);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Beiige, FodderSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Broww, FodderSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.Scarr, ScarrSet);
            AddExecutor(ExecutorType.Summon, CardId.Goldd, TributeBeaterSummon);
            AddExecutor(ExecutorType.Summon, CardId.Sillva, TributeBeaterSummon);
            AddExecutor(ExecutorType.Summon, CardId.ReignBeaux, TributeBeaterSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.MorphingJar, MorphingJarSet);
            AddExecutor(ExecutorType.MonsterSet, CardId.DarkResonator, EmptyFieldSet);
            AddExecutor(ExecutorType.MonsterSet, CardId.Gren, EmptyFieldSet);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Beiige, EmptyFieldSet);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Broww, EmptyFieldSet);

            // Backrow and positions.
            AddExecutor(ExecutorType.SpellSet, SpellSetEffect);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        // ---- Board helpers ------------------------------------------------

        private static bool IsDarkWorld(ClientCard card)
        {
            return card != null && card.IsCode(DarkWorldIds);
        }

        private static bool IsDarkMonster(ClientCard card)
        {
            return card != null && card.IsMonster() && card.HasAttribute(CardAttribute.Dark);
        }

        private bool IsMainPhaseOfOurs()
        {
            return Duel.Player == 0 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2);
        }

        private bool IsBattlePhase()
        {
            return Duel.Phase == DuelPhase.BattleStart || Duel.Phase == DuelPhase.BattleStep || Duel.Phase == DuelPhase.Battle;
        }

        private bool HasFreeMonsterZone()
        {
            return Bot.GetMonsterCount() < 5;
        }

        private int DarkCountInGraveyard()
        {
            return Bot.Graveyard.Count(IsDarkMonster);
        }

        private bool DarkArmedWaiting()
        {
            return Bot.HasInHand(CardId.DarkArmedDragon) && HasFreeMonsterZone();
        }

        private bool IsBehindOnBoard()
        {
            int ours = Bot.GetFieldCount();
            int theirs = Enemy.GetFieldCount();
            return theirs >= ours + 2 || (Util.GetBestPower(Enemy) > 2500 && theirs >= ours);
        }

        private bool CanSynchroWithResonator()
        {
            return Bot.GetMonsters().Any(material => material.IsFaceup() && !material.IsTuner()
                && Bot.ExtraDeck.Any(card => card.Level == material.Level + 3 && !card.IsCode(UnreachableSynchros)));
        }

        private bool HasDiscardOutletInHand()
        {
            return Bot.Hand.Any(card => card.IsCode(CardId.DarkWorldDealings, CardId.CardDestruction))
                || Bot.HasInHand(CardId.DarkWorldLightning) && HasFaceDownEnemyCard();
        }

        private bool HasFaceDownEnemyCard()
        {
            return Enemy.GetSpells().Any(card => card.IsFacedown()) || Enemy.GetMonsters().Any(card => card.IsFacedown());
        }

        // How badly a card on the opponent's field needs to go.
        private int ThreatScore(ClientCard card)
        {
            if (card.IsFloodgate())
                return 200;
            if (card.IsMonster())
                return card.IsFaceup() ? 50 + card.Attack / 100 : 40;
            if (card.IsFacedown())
                return 45;
            if (card.HasType(CardType.Continuous) || card.HasType(CardType.Equip) || card.HasType(CardType.Field))
                return 35;
            return 10;
        }

        private ClientCard GetBestEnemyTarget()
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Where(card => !card.IsShouldNotBeTarget())
                .OrderByDescending(ThreatScore).FirstOrDefault();
        }

        // ---- Hand evaluation -----------------------------------------------

        // A Spell/Trap in hand that has nothing to do right now.
        private bool IsDeadCard(ClientCard card)
        {
            if (card.IsCode(CardId.GatewayToDarkWorld))
                return !Bot.Graveyard.Any(IsDarkWorld);
            if (card.IsCode(CardId.DarkWorldLightning))
                return !HasFaceDownEnemyCard();
            if (card.IsCode(_CardId.HeavyStorm))
                return Enemy.GetSpellCount() == 0;
            if (card.IsCode(_CardId.CallOfTheHaunted))
                return !Bot.Graveyard.Any(IsReviveCandidate);
            return false;
        }

        // Higher means "throw this first". `triggers` is true when the card
        // leaves the hand as an effect discard (Dark World effects apply);
        // `opponentCaused` upgrades the Dark World bonus effects.
        private int DiscardScore(ClientCard card, bool triggers, bool opponentCaused)
        {
            bool zone = HasFreeMonsterZone();
            bool revives = triggers && zone
                && (card.IsCode(CardId.Goldd, CardId.Sillva, CardId.Beiige) || opponentCaused && card.IsCode(CardId.ReignBeaux));
            int score;

            if (card.IsCode(CardId.Goldd))
                score = !triggers ? 15 : opponentCaused ? 100 : zone ? 90 : 25;
            else if (card.IsCode(CardId.Sillva))
                score = !triggers ? 15 : opponentCaused ? (Enemy.GetHandCount() > 1 ? 96 : 88) : zone ? 89 : 25;
            else if (card.IsCode(CardId.ReignBeaux))
                score = !triggers ? 45 : opponentCaused && zone ? 94 : 40;
            else if (card.IsCode(CardId.Broww))
                score = !triggers ? 30 : opponentCaused ? 92 : 70;
            else if (card.IsCode(CardId.Beiige))
                score = !triggers ? 35 : zone ? 75 : 35;
            else if (card.IsCode(CardId.Gren))
                score = !triggers ? 50 : Enemy.GetSpellCount() > 0 ? 85 : Bot.GetSpellCount() > 0 ? 5 : 45;
            else if (card.IsCode(CardId.Zure))
                score = 55;
            else if (card.IsCode(CardId.Scarr))
                score = 50;
            else if (card.IsCode(CardId.DarkResonator))
                score = Bot.Hand.Count(other => other.IsCode(CardId.DarkResonator)) > 1 ? 48 : 30;
            else if (card.IsCode(CardId.DarkArmedDragon))
                score = DarkCountInGraveyard() > 4 ? 40 : 0;
            else
                score = IsDeadCard(card) ? 58 : 10;

            // Steer the GY towards exactly 3 DARK while Dark Armed Dragon waits.
            if (IsDarkMonster(card) && !revives && !card.IsCode(CardId.DarkArmedDragon) && DarkArmedWaiting())
            {
                int dark = DarkCountInGraveyard();
                if (dark == 3)
                    score -= 60;
                else if (dark == 2)
                    score += 30;
            }
            return score;
        }

        // Allure of Darkness banish: lose the least useful DARK monster.
        private int BanishScore(ClientCard card)
        {
            if (card.IsCode(CardId.Zure))
                return 60;
            if (card.IsCode(CardId.Scarr))
                return 55;
            if (card.IsCode(CardId.DarkResonator))
                return Bot.Hand.Count(other => other.IsCode(CardId.DarkResonator)) > 1 ? 50 : 20;
            if (card.IsCode(CardId.Gren))
                return Enemy.GetSpellCount() > 0 ? 35 : 50;
            if (card.IsCode(CardId.ReignBeaux))
                return 45;
            if (card.IsCode(CardId.DarkArmedDragon))
                return DarkCountInGraveyard() > 4 ? 48 : 5;
            if (card.IsCode(CardId.Broww))
                return 30;
            if (card.IsCode(CardId.Beiige))
                return 25;
            return 10;
        }

        private int BestDiscardScore(bool triggers)
        {
            List<ClientCard> others = Bot.Hand.Where(card => card != Card).ToList();
            return others.Count == 0 ? -1 : others.Max(card => DiscardScore(card, triggers, false));
        }

        private static IList<ClientCard> TakeBest(IList<ClientCard> cards, int min, int max, Func<ClientCard, int> score)
        {
            int count = Math.Min(Math.Max(min, 1), Math.Min(max, cards.Count));
            return cards.OrderByDescending(score).Take(count).ToList();
        }

        // ---- Spells ----------------------------------------------------------

        // Dark World Lightning: destroy a face-down card of the opponent's
        // (backrow first), then discard into the engine.
        private bool DarkWorldLightningEffect()
        {
            ClientCard target = Enemy.GetSpells().Where(card => card.IsFacedown() && !card.IsShouldNotBeTarget())
                    .OrderByDescending(ThreatScore).FirstOrDefault()
                ?? Enemy.GetMonsters().FirstOrDefault(card => card.IsFacedown() && !card.IsShouldNotBeTarget());
            if (target == null || BestDiscardScore(true) < 0)
                return false;
            AI.SelectCard(target);
            return true;
        }

        private bool AllureOfDarknessEffect()
        {
            return Bot.Hand.Any(card => card != Card && IsDarkMonster(card));
        }

        // Dealings: draw 1 then discard 1; worth it whenever the hand has a
        // discard that does something.
        private bool DarkWorldDealingsEffect()
        {
            return BestDiscardScore(true) >= 45 || Bot.Hand.Count >= 5;
        }

        // Set traps before Card Destruction throws the hand away.
        private bool TrapSetBeforeCardDestruction()
        {
            return Card.IsTrap() && Bot.HasInHand(CardId.CardDestruction) && Bot.GetSpellCountWithoutField() < 4;
        }

        private bool CardDestructionEffect()
        {
            List<ClientCard> others = Bot.Hand.Where(card => card != Card).ToList();
            if (others.Count == 0 || Bot.Deck.Count <= others.Count + 1)
                return false;
            int engine = others.Count(card => IsDarkWorld(card) && DiscardScore(card, true, false) >= 70);
            int valuable = others.Count(card => !card.IsMonster() && !IsDeadCard(card));
            return engine >= 2 && valuable <= 1 || engine >= 1 && others.Count <= 2;
        }

        // The Forces of Darkness: return 2 Dark World monsters to hand so the
        // discard outlets can throw them again; self-reviving lords first.
        private bool ForcesOfDarknessEffect()
        {
            if (!IsMainPhaseOfOurs() || Bot.Graveyard.Count(IsDarkWorld) < 2)
                return false;
            AI.SelectCard(CardId.Goldd, CardId.Sillva, CardId.Broww, CardId.Scarr,
                CardId.ReignBeaux, CardId.Beiige, CardId.Zure, CardId.Gren);
            return true;
        }

        // Morphing Jar's flip makes both players discard their hands by card
        // effect, which triggers every Dark World caught in ours.
        private bool MorphingJarSet()
        {
            return Bot.Hand.Count(IsDarkWorld) >= 1;
        }

        private bool MistWurmSummon()
        {
            return Enemy.GetFieldCount() >= 3;
        }

        // ---- Dark Armed Dragon ---------------------------------------------

        private bool DarkArmedDragonEffect()
        {
            ClientCard target = GetBestEnemyTarget();
            if (target == null)
                return false;
            ClientCard cost = null;
            foreach (int id in DarkArmedCostPriority)
            {
                cost = Bot.Graveyard.FirstOrDefault(card => card.IsCode(id));
                if (cost != null)
                    break;
            }
            if (cost == null)
                cost = Bot.Graveyard.FirstOrDefault(IsDarkMonster);
            if (cost == null)
                return false;
            AI.SelectCard(cost);
            AI.SelectNextCard(target);
            return true;
        }

        // ---- Summons -----------------------------------------------------------

        private bool DarkResonatorSummon()
        {
            return CanSynchroWithResonator();
        }

        // Beiige / Broww are discard fodder; they only take the Normal Summon
        // when there is no outlet to discard them into.
        private bool FodderSummon()
        {
            return !HasDiscardOutletInHand() && Bot.GetMonsterCount() == 0;
        }

        // Scarr wants to be destroyed in battle to search.
        private bool ScarrSet()
        {
            return Enemy.GetMonsterCount() > 0 && !HasDiscardOutletInHand();
        }

        private bool TributeBeaterSummon()
        {
            if (HasDiscardOutletInHand())
                return false;
            return DefaultMonsterSummon();
        }

        private bool EmptyFieldSet()
        {
            return Bot.GetMonsterCount() == 0;
        }

        // ---- Synchro choices -----------------------------------------------

        private bool DarkEndDragonSummon()
        {
            return Enemy.GetMonsters().Any(card => !card.IsShouldNotBeTarget()
                && (card.IsFacedown() || card.Attack >= 1500 || card.IsFloodgate()));
        }

        // Its End Phase sweep destroys our monsters that did not attack.
        private bool RedDragonArchfiendSummon()
        {
            return Enemy.GetMonsters().Any(card => card.IsDefense())
                || Enemy.GetMonsterCount() == 0 && Bot.GetMonsterCount() <= 2;
        }

        private bool StardustDragonSummon()
        {
            return Enemy.GetSpells().Count(card => card.IsFacedown()) >= 2;
        }

        private bool BrionacSummon()
        {
            return GetBestEnemyTarget() != null && GetBrionacDiscard() != null;
        }

        private bool GoyoGuardianSummon()
        {
            return Enemy.GetMonsters().Any(card => card.GetDefensePower() < 2800);
        }

        private bool CatastorSummon()
        {
            return Enemy.GetMonsters().Any(card => card.IsFaceup() && !card.HasAttribute(CardAttribute.Dark));
        }

        // ---- Synchro effects ---------------------------------------------

        private bool DarkEndDragonEffect()
        {
            ClientCard target = Enemy.GetMonsters().Where(card => !card.IsShouldNotBeTarget())
                .OrderByDescending(ThreatScore).FirstOrDefault();
            if (target == null)
                return false;
            if (Card.Attack < 2100 && !target.IsFloodgate() && target.Attack < Card.Attack)
                return false;
            AI.SelectCard(target);
            return true;
        }

        private bool BlackRoseDragonEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.BlackRoseDragon, 0))
                return DefaultBlackRoseDragonWipe();
            return false;
        }

        private ClientCard GetBrionacDiscard()
        {
            return Bot.Hand.Where(card => DiscardScore(card, false, false) >= 45)
                .OrderByDescending(card => DiscardScore(card, false, false)).FirstOrDefault();
        }

        // Brionac: discard a spare card to bounce the biggest threat.
        private bool BrionacEffect()
        {
            ClientCard target = GetBestEnemyTarget();
            ClientCard discard = GetBrionacDiscard();
            if (target == null || discard == null)
                return false;
            AI.SelectCard(discard);
            AI.SelectNextCard(target);
            return true;
        }

        private bool ThoughtRulerArchfiendEffect()
        {
            if (Duel.LastChainPlayer == 1)
                return Bot.LifePoints > 1000 && Util.IsChainTarget(Card);
            return true;
        }

        // ---- Traps / recursion -------------------------------------------

        private static bool IsReviveCandidate(ClientCard card)
        {
            return card.IsMonster() && card.IsCanRevive() && !card.IsCode(CardId.DarkArmedDragon);
        }

        private ClientCard GetResonatorPartnerInGraveyard()
        {
            return Bot.Graveyard.Where(card => IsReviveCandidate(card) && !card.IsTuner()
                    && Bot.ExtraDeck.Any(extra => extra.Level == card.Level + 3 && !extra.IsCode(UnreachableSynchros)))
                .OrderByDescending(card => card.Level).FirstOrDefault();
        }

        // Call of the Haunted: on our turn, revive a Synchro partner for a
        // Resonator in hand; on theirs, a blocker when the field is empty or
        // every enemy monster is better.
        private bool CallOfTheHauntedEffect()
        {
            if (!HasFreeMonsterZone() || DefaultTrapWillBeNegated())
                return false;
            if (IsMainPhaseOfOurs() && Bot.HasInHand(CardId.DarkResonator))
            {
                ClientCard partner = GetResonatorPartnerInGraveyard();
                if (partner != null)
                {
                    AI.SelectCard(partner);
                    return true;
                }
            }
            bool needBody = Duel.Player == 1 && (Duel.Phase == DuelPhase.End || IsBattlePhase())
                && Bot.GetMonsterCount() == 0;
            if (!needBody && !Util.IsAllEnemyBetter(true))
                return false;
            ClientCard target = Bot.Graveyard.Where(IsReviveCandidate).OrderByDescending(card => card.Attack).FirstOrDefault();
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // Gateway locks our own summons for the turn, so it is fired in the
        // opponent's turn (End Phase, or as a blocker against an empty field)
        // or on ours only when there is nothing else to summon.
        private bool GatewayToDarkWorldEffect()
        {
            if (!HasFreeMonsterZone())
                return false;
            bool timing;
            if (Duel.Player == 1)
                timing = Duel.Phase == DuelPhase.End
                    || IsBattlePhase() && Bot.GetMonsterCount() == 0;
            else
                timing = Duel.Phase == DuelPhase.Main1 && Bot.GetMonsterCount() == 0
                    && !Bot.Hand.Any(card => card.IsMonster() && card.Level <= 4)
                    && !HasDiscardOutletInHand() && !Bot.HasInHand(CardId.DarkArmedDragon);
            if (!timing)
                return false;
            ClientCard target = GetGatewayTarget();
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        private ClientCard GetGatewayTarget()
        {
            List<ClientCard> pool = Bot.Graveyard.Where(card => IsDarkWorld(card) && card.IsCanRevive()).ToList();
            if (Bot.HasInHand(CardId.DarkResonator) || Bot.GetMonsters().Any(card => card.IsCode(CardId.DarkResonator)))
            {
                ClientCard partner = pool.FirstOrDefault(card => card.IsCode(CardId.Goldd, CardId.Sillva));
                if (partner != null)
                    return partner;
            }
            return pool.OrderByDescending(card => card.Attack).FirstOrDefault();
        }

        private bool SpellSetEffect()
        {
            if (Bot.GetSpellCountWithoutField() >= 4)
                return false;
            if (Card.IsTrap())
                return true;
            return Card.IsCode(CardId.GatewayToDarkWorld, _CardId.MysticalSpaceTyphoon);
        }

        // ---- Prompts ---------------------------------------------------------

        // Reign-Beaux (opponent-caused discard): destroy the side that hurts more.
        public override int OnSelectOption(IList<int> options)
        {
            int monsters = options.IndexOf(Util.GetStringId(CardId.ReignBeaux, 2));
            int spells = options.IndexOf(Util.GetStringId(CardId.ReignBeaux, 3));
            if (monsters >= 0 && spells >= 0)
            {
                bool hitMonsters = Enemy.GetMonsterCount() >= Enemy.GetSpellCount() || Util.GetBestPower(Enemy) >= 2000;
                return hitMonsters ? monsters : spells;
            }
            return base.OnSelectOption(options);
        }

        // Discards, sends and the mandatory Dark World triggers are chosen
        // here, keyed on the effect being resolved or activated.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (AI.HaveSelectedCards() || cards.Count == 0)
                return base.OnSelectCard(cards, min, max, hint, cancelable);

            ClientCard source = Duel.GetCurrentSolvingChainCard() ?? Duel.CurrentChain.LastOrDefault();
            bool ourHand = cards.All(card => card.Controller == 0 && card.Location == CardLocation.Hand);

            if (ourHand)
            {
                if (source != null && source.Controller == 0 && source.IsCode(_CardId.AllureofDarkness))
                    return TakeBest(cards, min, max, BanishScore);
                if (hint == HintMsg.Discard)
                {
                    bool opponentCaused = source != null && source.Controller == 1;
                    bool isCost = source != null && source.Controller == 0 && source.IsCode(CardId.Brionac);
                    return TakeBest(cards, min, max, card => DiscardScore(card, !isCost, opponentCaused));
                }
                if (hint == HintMsg.ToGrave)
                    return TakeBest(cards, min, max, card => DiscardScore(card, false, false));
            }

            if (source != null && source.Controller == 0)
            {
                if (source.IsCode(CardId.Gren))
                    return new[] { GetGrenTarget(cards) };
                if (source.IsCode(CardId.Goldd) && cards.All(card => card.Controller == 1))
                    return cards.OrderByDescending(ThreatScore).Take(Math.Max(1, max)).ToList();
                if (source.IsCode(CardId.Scarr) && cards.All(card => card.Location == CardLocation.Deck))
                    return new[] { GetScarrSearch(cards) };
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        // Gren must target a Spell/Trap: the opponent's worst one, else our
        // least useful (face-up first).
        private ClientCard GetGrenTarget(IList<ClientCard> cards)
        {
            return cards.Where(card => card.Controller == 1).OrderByDescending(ThreatScore).FirstOrDefault()
                ?? cards.OrderByDescending(card => card.IsFaceup() ? 1 : 0).First();
        }

        private ClientCard GetScarrSearch(IList<ClientCard> cards)
        {
            List<int> priority = new List<int>();
            if (Enemy.GetSpellCount() > 0 && !Bot.HasInHand(CardId.Gren))
                priority.Add(CardId.Gren);
            if (!Bot.HasInHand(CardId.Broww))
                priority.Add(CardId.Broww);
            priority.AddRange(new[] { CardId.Beiige, CardId.Broww, CardId.Gren, CardId.Zure, CardId.Scarr });
            foreach (int id in priority)
            {
                ClientCard pick = cards.FirstOrDefault(card => card.IsCode(id));
                if (pick != null)
                    return pick;
            }
            return cards.First();
        }

        // Synchro materials: among the combinations that hit the exact Level,
        // spend the one with the lowest combined ATK.
        public override IList<ClientCard> OnSelectSynchroMaterial(IList<ClientCard> cards, int sum, int min, int max)
        {
            if (sum <= 0)
                return null;
            List<ClientCard> best = null;
            int bestCost = int.MaxValue;
            int upper = Math.Min(max, cards.Count);
            for (int k = 1; k <= upper; ++k)
            {
                foreach (IEnumerable<ClientCard> combo in cards.GetCombinations(k))
                {
                    List<ClientCard> list = combo.ToList();
                    int s1 = list.Sum(card => card.OpParam1);
                    int s2 = list.Sum(card => card.OpParam2 != 0 ? card.OpParam2 : card.OpParam1);
                    if (s1 != sum && s2 != sum)
                        continue;
                    int cost = list.Sum(card => card.Attack);
                    if (cost < bestCost)
                    {
                        best = list;
                        bestCost = cost;
                    }
                }
            }
            return best;
        }
    }
}
