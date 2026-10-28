using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Fiend beatdown/control bot for the Edison format (TCG April 2010 card
    // pool), built around the Archfiend engine. Pandemonium is the core card:
    // it removes every Archfiend's Standby Phase LP upkeep and replaces each
    // Archfiend destroyed by a card effect with a lower-Level one from the
    // deck. Archfiend General discards itself to fetch Pandemonium, Archfiend
    // Soldier / Infernalqueen / Vilepawn / Darkbishop open the gate for
    // Terrorking Archfiend (which can only be Normal Summoned while we control
    // an Archfiend), Desrook Archfiend revives a fallen Terrorking from the
    // hand, and Archfiend's Roar recycles the strongest Archfiend for a turn
    // (its End Phase self-destruction feeds Pandemonium's search). Caius the
    // Shadow Monarch, Dark Ruler Ha Des and Skull Archfiend of Lightning are
    // the tribute beaters, Snipe Hunter and Falling Down turn spare cards into
    // removal, Doomcaliber Knight and Fiendish Chain shut off monster effects,
    // and Dark Resonator gives a small Synchro toolbox (Black Rose Dragon,
    // Goyo Guardian, Brionac, Catastor, Dark End Dragon, Red Dragon Archfiend).
    // Battle Fader and Gorz punish direct attacks on an empty board.
    // Sangan, Brain Control, Goyo Guardian and Brionac use the pre-errata
    // passcodes required by the Edison whitelist; they are defined locally.
    // Deck: AI_HalloweenArchfiends.ydk (Edison-whitelist-validated).
    [Deck("HalloweenArchfiends", "AI_HalloweenArchfiends")]
    public class HalloweenArchfiendsExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Tribute beaters.
            public const int Caius = 9748752;
            public const int DarkRulerHaDes = 53982768;
            public const int SkullArchfiend = 61370518;

            // Archfiend package.
            public const int Terrorking = 35975813;
            public const int ArchfiendSoldier = 49881766;
            public const int Infernalqueen = 8581705;
            public const int Desrook = 72192100;
            public const int ArchfiendGeneral = 48675364;
            public const int Darkbishop = 35798491;
            public const int Vilepawn = 73219648;

            // Fiend support monsters.
            public const int DarkResonator = 97021916;
            public const int SnipeHunter = 84290642;
            public const int DoomcaliberKnight = 78700060;
            public const int Sangan = 511002631; // PRE-ERRATA passcode (Edison whitelist).
            public const int BattleFader = 19665973;
            public const int Gorz = 44330098;

            // Spells.
            public const int Pandemonium = 94585852;
            public const int BrainControl = 511002995; // PRE-ERRATA passcode (Edison whitelist).
            public const int FallingDown = 32919136;

            // Traps.
            public const int ArchfiendsRoar = 56246017;
            public const int MirrorForce = 44095762;
            public const int BottomlessTrapHole = 29401950;
            public const int DimensionalPrison = 70342110;
            public const int FiendishChain = 50078509;

            // Extra Deck.
            public const int BlackRoseDragon = 73580471;
            public const int GoyoGuardian = 511002994; // PRE-ERRATA passcode (Edison whitelist).
            public const int Brionac = 511002993; // PRE-ERRATA passcode (Edison whitelist).
            public const int Catastor = 26593852;
            public const int DarkEndDragon = 88643579;
            public const int RedDragonArchfiend = 70902743;
            public const int ThoughtRulerArchfiend = 70780151;
            public const int StardustDragon = 44508094;
        }

        private const int ArchfiendSetcode = 0x45;

        // Pandemonium search priority: the strongest Archfiend the Level cap
        // allows. The candidate list from the engine already respects the
        // "lower Level than the destroyed card" rule, so unavailable ids are
        // skipped by the selector.
        private static readonly int[] PandemoniumSearchPriority =
        {
            CardId.Terrorking, CardId.Infernalqueen, CardId.Desrook,
            CardId.Darkbishop, CardId.Vilepawn
        };

        // Sangan search priority (1500 ATK or less): removal first, then the
        // Tuner, then the Terrorking enabler, then the defensive fader.
        private static readonly int[] SanganSearchPriority =
        {
            CardId.SnipeHunter, CardId.DarkResonator, CardId.Infernalqueen,
            CardId.Desrook, CardId.BattleFader, CardId.Vilepawn, CardId.Darkbishop
        };

        public HalloweenArchfiendsExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Pandemonium's Standby Phase prompt ("skip the LP upkeep?") is
            // asked through every Archfiend we control; always skip paying.
            AddExecutor(ExecutorType.Activate, PandemoniumSkipUpkeep);

            // Pandemonium first: the whole deck plays around it.
            AddExecutor(ExecutorType.Activate, CardId.Pandemonium, PandemoniumEffect);
            AddExecutor(ExecutorType.Activate, CardId.ArchfiendGeneral, ArchfiendGeneralSearch);

            // Removal / disruption staples with DefaultExecutor's smart timing.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.DimensionalPrison, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, DefaultCallOfTheHaunted);
            AddExecutor(ExecutorType.Activate, CardId.FiendishChain, FiendishChainEffect);
            AddExecutor(ExecutorType.Activate, CardId.BrainControl, BrainControlEffect);
            AddExecutor(ExecutorType.Activate, CardId.FallingDown, FallingDownEffect);
            AddExecutor(ExecutorType.Activate, _CardId.AllureofDarkness, AllureOfDarknessEffect);

            // Monster effects.
            AddExecutor(ExecutorType.Activate, CardId.DoomcaliberKnight, DoomcaliberKnightEffect);
            AddExecutor(ExecutorType.Activate, CardId.Desrook);
            AddExecutor(ExecutorType.Activate, CardId.SnipeHunter, SnipeHunterEffect);
            AddExecutor(ExecutorType.Activate, CardId.Caius, CaiusEffect);
            AddExecutor(ExecutorType.Activate, CardId.Sangan, SanganEffect);
            AddExecutor(ExecutorType.Activate, CardId.BattleFader);
            AddExecutor(ExecutorType.Activate, CardId.Gorz);
            AddExecutor(ExecutorType.SpSummon, CardId.BattleFader);
            AddExecutor(ExecutorType.SpSummon, CardId.Gorz);

            // Synchro toolbox off Dark Resonator.
            AddExecutor(ExecutorType.SpSummon, CardId.DarkEndDragon, DarkEndDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.RedDragonArchfiend, RedDragonArchfiendSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.StardustDragon, DefaultStardustDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.ThoughtRulerArchfiend, ThoughtRulerArchfiendSummon);
            AddExecutor(ExecutorType.Activate, CardId.ThoughtRulerArchfiend, ThoughtRulerArchfiendEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.BlackRoseDragon, BlackRoseDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Brionac, BrionacSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.GoyoGuardian, GoyoGuardianSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Catastor, CatastorSummon);
            AddExecutor(ExecutorType.Activate, CardId.BlackRoseDragon, BlackRoseDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.DarkEndDragon, DarkEndDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.Brionac, BrionacEffect);
            AddExecutor(ExecutorType.Activate, CardId.GoyoGuardian);
            AddExecutor(ExecutorType.Activate, CardId.StardustDragon, DefaultStardustDragonEffect);

            // Archfiend's Roar: recycle the best Archfiend on our own turn.
            AddExecutor(ExecutorType.Activate, CardId.ArchfiendsRoar, ArchfiendsRoarEffect);

            // Summons, strongest first.
            AddExecutor(ExecutorType.Summon, CardId.Caius, CaiusSummon);
            AddExecutor(ExecutorType.Summon, CardId.DarkRulerHaDes, TributeBeaterSummon);
            AddExecutor(ExecutorType.Summon, CardId.SkullArchfiend, TributeBeaterSummon);
            AddExecutor(ExecutorType.Summon, CardId.Terrorking);
            AddExecutor(ExecutorType.Summon, CardId.ArchfiendGeneral, ArchfiendGeneralSummon);
            AddExecutor(ExecutorType.Summon, CardId.Infernalqueen, GateArchfiendSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.ArchfiendSoldier);
            AddExecutor(ExecutorType.Summon, CardId.SnipeHunter, SnipeHunterSummon);
            AddExecutor(ExecutorType.Summon, CardId.DarkResonator, DarkResonatorSummon);
            AddExecutor(ExecutorType.Summon, CardId.DoomcaliberKnight);
            AddExecutor(ExecutorType.Summon, CardId.Vilepawn, VilepawnSummon);
            AddExecutor(ExecutorType.Summon, CardId.Darkbishop, DarkbishopSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.Sangan);
            AddExecutor(ExecutorType.MonsterSet, CardId.DarkResonator);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Infernalqueen);
            AddExecutor(ExecutorType.MonsterSet, CardId.Vilepawn);

            // Fallback: whatever is left, set traps, reposition.
            AddExecutor(ExecutorType.SummonOrSet, FallbackSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        private static bool IsArchfiend(ClientCard card)
        {
            return card != null && card.HasSetcode(ArchfiendSetcode);
        }

        private bool ControlsFaceupArchfiend()
        {
            return Bot.GetMonsters().Any(card => card.IsFaceup() && IsArchfiend(card));
        }

        private bool PandemoniumActive()
        {
            ClientCard field = Bot.GetFieldSpellCard();
            return field != null && field.IsFaceup() && field.IsCode(CardId.Pandemonium);
        }

        // Every Archfiend asks "use Pandemonium instead of paying?" during our
        // Standby Phase; answering no would pay the upkeep for nothing.
        private bool PandemoniumSkipUpkeep()
        {
            return ActivateDescription == Util.GetStringId(CardId.Pandemonium, 1);
        }

        // Two jobs share this card: the search trigger (take the strongest
        // legal Archfiend) and the activation of the Field Spell itself
        // (only when no face-up Pandemonium is already running).
        private bool PandemoniumEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.Pandemonium, 0))
                return true;
            return !PandemoniumActive();
        }

        // Discard General to fetch Pandemonium only when no copy is already
        // in hand or on the field; otherwise keep it as a 2100 beater.
        private bool ArchfiendGeneralSearch()
        {
            if (Card.Location != CardLocation.Hand)
                return false;
            return !PandemoniumActive() && !Bot.HasInHand(CardId.Pandemonium)
                && !Bot.HasInSpellZone(CardId.Pandemonium);
        }

        // General destroys itself without Pandemonium on the field.
        private bool ArchfiendGeneralSummon()
        {
            return PandemoniumActive();
        }

        // Infernalqueen is the cheapest gate for Terrorking: summon it when a
        // Terrorking waits in hand or we already field another Archfiend to buff.
        private bool GateArchfiendSummon()
        {
            return Bot.HasInHand(CardId.Terrorking) || ControlsFaceupArchfiend();
        }

        // Vilepawn redirects every attack onto itself, so it only earns a
        // summon while it protects a stronger Archfiend.
        private bool VilepawnSummon()
        {
            return Bot.GetMonsters().Any(card => card.IsFaceup() && IsArchfiend(card) && card.Attack > 1200);
        }

        // Darkbishop shields our other Archfiends from targeted effects.
        private bool DarkbishopSummon()
        {
            return Bot.GetMonsters().Count(card => card.IsFaceup() && IsArchfiend(card)) >= 1;
        }

        // Snipe Hunter is only worth a body when there is something to shoot
        // and a spare card to discard for it.
        private bool SnipeHunterSummon()
        {
            return Enemy.GetFieldCount() > 0 && Bot.Hand.Count >= 2;
        }

        // Normal Summon the Tuner only when a Synchro is reachable this turn:
        // a Level 4 non-Tuner makes Black Rose Dragon, a Level 3 Goyo/Brionac,
        // a Level 2 Catastor.
        private bool DarkResonatorSummon()
        {
            return Bot.GetMonsters().Any(card => card.IsFaceup() && !card.IsTuner()
                && (card.Level == 2 || card.Level == 3 || card.Level == 4));
        }

        // Caius needs an enemy card to banish (its trigger is mandatory and
        // would hit our own field otherwise) and a cheap tribute.
        private bool CaiusSummon()
        {
            ClientCard target = GetCaiusTarget();
            if (target == null || !HasCheapTribute(2400))
                return false;
            AI.SelectCard(target);
            return true;
        }

        private bool CaiusEffect()
        {
            ClientCard target = GetCaiusTarget();
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // DARK monsters also burn 1000, so prefer a problematic DARK body.
        private ClientCard GetCaiusTarget()
        {
            ClientCard problem = Util.GetProblematicEnemyCard(0, true);
            if (problem != null)
                return problem;
            ClientCard dark = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && card.HasAttribute(CardAttribute.Dark) && !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (dark != null)
                return dark;
            return Util.GetBestEnemyCard(false, true);
        }

        private bool TributeBeaterSummon()
        {
            return HasCheapTribute(Card.Attack);
        }

        // A tribute is cheap when it is weaker than the incoming beater and
        // not one of our own Level 6+ threats.
        private bool HasCheapTribute(int incomingAttack)
        {
            return Bot.GetMonsters().Any(card => card.Level < 6 && card.GetDefensePower() < incomingAttack);
        }

        // Doomcaliber Knight negates and destroys an opponent's monster effect.
        private bool DoomcaliberKnightEffect()
        {
            return Duel.LastChainPlayer == 1;
        }

        // Discard the least useful card, then shoot the opponent's best card.
        // The roll destroys on 2-5, so only fire at a real target.
        private bool SnipeHunterEffect()
        {
            ClientCard target = Util.GetProblematicEnemyCard(0, true)
                ?? Util.GetBestEnemyMonster(false, true)
                ?? Util.GetBestEnemySpell();
            if (target == null)
                return false;
            ClientCard discard = GetSpareHandCard();
            if (discard == null)
                return false;
            AI.SelectCard(discard);
            AI.SelectNextCard(target);
            return true;
        }

        // Spare card for discard costs: a General once Pandemonium is live, a
        // redundant Pandemonium, then the weakest monster, then any spell.
        private ClientCard GetSpareHandCard()
        {
            if (PandemoniumActive())
            {
                ClientCard spare = Bot.Hand.FirstOrDefault(card => card.IsCode(CardId.Pandemonium, CardId.ArchfiendGeneral));
                if (spare != null)
                    return spare;
            }
            ClientCard monster = Bot.Hand
                .Where(card => card.IsMonster() && !card.IsCode(CardId.Desrook, CardId.BattleFader, CardId.Gorz) && card != Card)
                .OrderBy(card => card.Attack).FirstOrDefault();
            if (monster != null)
                return monster;
            return Bot.Hand.FirstOrDefault(card => card != Card && !card.IsCode(CardId.Pandemonium));
        }

        private bool SanganEffect()
        {
            AI.SelectCard(SanganSearchPriority);
            return true;
        }

        // Fiendish Chain: negate and pin the opponent's best effect monster,
        // either in response to its effect or when it attacks into us.
        private bool FiendishChainEffect()
        {
            if (DefaultTrapWillBeNegated())
                return false;
            ClientCard target = null;
            ClientCard last = Util.GetLastChainCard();
            if (Duel.LastChainPlayer == 1 && last != null && last.Controller == 1
                && last.Location == CardLocation.MonsterZone && last.IsFaceup())
                target = last;
            else if (Duel.Player == 1 && Enemy.BattlingMonster != null && Enemy.BattlingMonster.HasType(CardType.Effect))
                target = Enemy.BattlingMonster;
            else if (Duel.Player == 1 && Duel.Phase == DuelPhase.End)
                target = Enemy.GetMonsters()
                    .Where(card => card.IsFaceup() && card.HasType(CardType.Effect) && !card.IsDisabled())
                    .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (target == null || target.IsShouldNotBeTarget())
                return false;
            AI.SelectCard(target);
            return true;
        }

        // Pre-errata Brain Control: the stolen monster can attack and be
        // tributed, so it doubles as Caius / Ha Des fodder.
        private bool BrainControlEffect()
        {
            if (Bot.LifePoints <= 800)
                return false;
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (target == null || target.Attack < 1500)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // Falling Down is destroyed without an Archfiend card, so only cast it
        // while a face-up Archfiend holds the field, on a body worth stealing.
        private bool FallingDownEffect()
        {
            if (!ControlsFaceupArchfiend() && !PandemoniumActive())
                return false;
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (target == null || target.Attack < 1800)
                return false;
            AI.SelectCard(target);
            return true;
        }

        private bool AllureOfDarknessEffect()
        {
            List<ClientCard> darkInHand = Bot.Hand
                .Where(card => card != Card && card.HasAttribute(CardAttribute.Dark)).ToList();
            if (darkInHand.Count == 0)
                return false;
            ClientCard banish = darkInHand.FirstOrDefault(card => card.IsCode(CardId.ArchfiendGeneral) && PandemoniumActive())
                ?? darkInHand.Where(card => !card.IsCode(CardId.Desrook, CardId.Gorz, CardId.BattleFader))
                    .OrderBy(card => card.Attack).FirstOrDefault()
                ?? darkInHand.First();
            AI.SelectCard(banish);
            return true;
        }

        // Archfiend's Roar on our Main Phase 1: revive the strongest Archfiend
        // in the GY to open the Terrorking gate, to pair with Dark Resonator
        // for a Synchro, or simply to add an attacker. It dies in the End
        // Phase, which also fires Pandemonium's search.
        private bool ArchfiendsRoarEffect()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1)
                return false;
            if (Bot.LifePoints <= 500 || Bot.GetMonsterCount() >= 5)
                return false;
            List<ClientCard> archfiends = Bot.Graveyard.Where(card => IsArchfiend(card) && card.IsMonster() && card.IsCanRevive()).ToList();
            if (archfiends.Count == 0)
                return false;

            bool resonatorOnField = Bot.GetMonsters().Any(card => card.IsFaceup() && card.IsCode(CardId.DarkResonator));
            if (resonatorOnField)
            {
                ClientCard material = archfiends.FirstOrDefault(card => card.Level == 4)
                    ?? archfiends.FirstOrDefault(card => card.Level == 3);
                if (material != null)
                {
                    AI.SelectCard(material);
                    return true;
                }
            }

            bool openGate = Bot.HasInHand(CardId.Terrorking) && !ControlsFaceupArchfiend() && Duel.Turn > 1;
            ClientCard best = archfiends.OrderByDescending(card => card.Attack).First();
            if (!openGate && best.Attack <= Util.GetBestPower(Enemy) && Enemy.GetMonsterCount() > 0)
                return false;
            AI.SelectCard(best);
            return true;
        }

        // Synchro choices.
        private bool DarkEndDragonSummon()
        {
            return Enemy.GetMonsters().Any(card => !card.IsShouldNotBeTarget());
        }

        private bool RedDragonArchfiendSummon()
        {
            // Its End Phase sweep destroys our monsters that did not attack,
            // so only bring it out to crush a defensive wall or onto an
            // otherwise empty board.
            return Enemy.GetMonsters().Any(card => card.IsDefense()) || Bot.GetMonsterCount() <= 2;
        }

        // Thought Ruler Archfiend: pay 1000 LP to negate an opponent's effect
        // that targets it.
        private bool ThoughtRulerArchfiendEffect()
        {
            if (Bot.LifePoints <= 1000 || Duel.LastChainPlayer != 1)
                return false;
            return Util.IsChainTarget(Card);
        }

        private bool ThoughtRulerArchfiendSummon()
        {
            return true;
        }

        // Black Rose Dragon: a 2400 body against any board, and a field wipe
        // (see BlackRoseDragonEffect) only when we are clearly behind.
        private bool BlackRoseDragonSummon()
        {
            return IsBehindOnBoard() || Enemy.GetMonsterCount() > 0;
        }

        private bool BlackRoseDragonEffect()
        {
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            if (ActivateDescription == Util.GetStringId(CardId.BlackRoseDragon, 0))
                return IsBehindOnBoard();
            return false;
        }

        private bool IsBehindOnBoard()
        {
            int ours = Bot.GetFieldCount();
            int theirs = Enemy.GetFieldCount();
            return theirs >= ours + 2 || (Util.GetBestPower(Enemy) > 2400 && theirs >= ours);
        }

        private bool BrionacSummon()
        {
            return Util.GetProblematicEnemyCard() != null && Bot.Hand.Count > 0;
        }

        private bool GoyoGuardianSummon()
        {
            return Enemy.GetMonsterCount() > 0 && Util.GetBestPower(Enemy) < 2800;
        }

        private bool CatastorSummon()
        {
            return Enemy.GetMonsters().Any(card => card.IsFaceup() && !card.HasAttribute(CardAttribute.Dark));
        }

        // Dark End Dragon: send the opponent's best monster to the GY while it
        // still keeps a useful body (stop once it would drop under 1600 ATK).
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

        // Brionac: discard one spare card to bounce one problematic card.
        private bool BrionacEffect()
        {
            ClientCard target = Util.GetProblematicEnemyCard(0, true) ?? Util.GetBestEnemyCard(false, true);
            ClientCard discard = GetSpareHandCard();
            if (target == null || discard == null)
                return false;
            AI.SelectCard(discard);
            AI.SelectNextCard(target);
            return true;
        }

        private bool FallbackSummon()
        {
            if (Card.IsCode(CardId.ArchfiendGeneral) && !PandemoniumActive())
                return false;
            if (Card.IsCode(CardId.Caius) && GetCaiusTarget() == null)
                return false;
            return DefaultMonsterSummon();
        }

        // Forced triggers never reach an executor before their selection, so
        // their choices are made here based on the chain link resolving or
        // being activated: Infernalqueen's Standby Phase buff, Sangan's search
        // and Pandemonium's search.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            ClientCard source = Duel.GetCurrentSolvingChainCard() ?? Duel.CurrentChain.LastOrDefault();
            if (source != null && source.Controller == 0 && min == 1)
            {
                if (source.IsCode(CardId.Infernalqueen) && cards.All(card => card.Location == CardLocation.MonsterZone))
                {
                    ClientCard best = cards.Where(card => card.Controller == 0 && card.IsAttack())
                        .OrderByDescending(card => card.Attack).FirstOrDefault()
                        ?? cards.FirstOrDefault(card => card.Controller == 0);
                    if (best != null)
                        return new[] { best };
                }
                if (source.IsCode(CardId.Sangan) && cards.All(card => card.Location == CardLocation.Deck))
                {
                    ClientCard pick = PickByPriority(cards, SanganSearchPriority);
                    if (pick != null)
                        return new[] { pick };
                }
                if (source.IsCode(CardId.Pandemonium) && cards.All(card => card.Location == CardLocation.Deck))
                {
                    ClientCard pick = PickByPriority(cards, PandemoniumSearchPriority);
                    if (pick != null)
                        return new[] { pick };
                }
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private static ClientCard PickByPriority(IList<ClientCard> cards, int[] priority)
        {
            foreach (int id in priority)
            {
                ClientCard card = cards.FirstOrDefault(c => c.IsCode(id));
                if (card != null)
                    return card;
            }
            return null;
        }

        // Synchro materials: among the combinations that hit the exact Level,
        // spend the one with the lowest combined ATK so the real beaters stay.
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
