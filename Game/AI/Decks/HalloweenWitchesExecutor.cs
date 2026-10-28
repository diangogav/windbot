using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Dark spellcaster bot for the Edison format (TCG April 2010 card pool),
    // built for the Halloween event. Dark Magician is the centrepiece: Skilled
    // Dark Magician collects Spell Counters from every Spell that resolves and
    // cashes them in for a free Dark Magician, Sage's Stone pulls one from the
    // Deck off a face-up Dark Magician Girl, and Magical Dimension swaps a spent
    // Spellcaster for a big one from hand while destroying a monster. With
    // Dark Magician on the field, Thousand Knives and Dark Magic Attack clear
    // the opponent's monsters and backrow. Breaker the Magical Warrior and Old
    // Vindictive Magician provide on-board removal, and the two Spellcaster
    // Tuners (Witch of the Black Rose, Night's End Sorcerer) turn small bodies
    // into Goyo Guardian, Explosive Magician, Stardust Dragon or Thought Ruler
    // Archfiend. A staple trap line protects the board.
    // Brain Control and Goyo Guardian use PRE-ERRATA passcodes required by the
    // Edison whitelist; they are defined locally in CardId below.
    // Deck: AI_HalloweenWitches.ydk (Edison-whitelist-validated).
    [Deck("HalloweenWitches", "AI_HalloweenWitches")]
    public class HalloweenWitchesExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Dark Magician package.
            public const int DarkMagician = 46986414;
            public const int DarkMagicianGirl = 38033121;
            public const int SkilledDarkMagician = 73752131;
            public const int SagesStone = 13604200;
            public const int ThousandKnives = 63391643;
            public const int DarkMagicAttack = 2314238;
            public const int MagicalDimension = 28553439;

            // Other Spellcasters.
            public const int Breaker = 71413901;
            public const int OldVindictiveMagician = 45141844;
            public const int WitchOfTheBlackRose = 17720747;
            public const int NightsEndSorcerer = 36107810;
            public const int ChaosCommandMagician = 72630549;
            public const int DarkElf = 21417692;

            // Staples not present in the shared _CardId class.
            public const int BrainControl = 511002995; // PRE-ERRATA passcode (Edison whitelist).
            public const int SwordsOfRevealingLight = 72302403;
            public const int MirrorForce = 44095762;
            public const int BottomlessTrapHole = 29401950;
            public const int DimensionalPrison = 70342110;
            public const int MagicCylinder = 62279055;

            // Extra Deck.
            public const int ExplosiveMagician = 33413279;
            public const int GoyoGuardian = 511002994; // PRE-ERRATA passcode (Edison whitelist).
            public const int StardustDragon = 44508094;
            public const int ThoughtRulerArchfiend = 70780151;
        }

        // Synchro material priority: spend the Tuners and the bodies whose job is
        // already done first, keep Skilled Dark Magician (counters) and Dark
        // Magician Girl on board whenever another combination exists.
        private static readonly int[] SynchroMaterials =
        {
            CardId.NightsEndSorcerer, CardId.WitchOfTheBlackRose, CardId.OldVindictiveMagician,
            CardId.Breaker, CardId.DarkElf, CardId.SkilledDarkMagician, CardId.DarkMagicianGirl
        };

        public HalloweenWitchesExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Skilled Dark Magician goes down first so every Spell we resolve this
            // turn feeds its counters.
            AddExecutor(ExecutorType.Summon, CardId.SkilledDarkMagician, SkilledDarkMagicianSummon);

            // Free Dark Magicians: Skilled's 3-counter tribute and Sage's Stone.
            AddExecutor(ExecutorType.Activate, CardId.SkilledDarkMagician, SkilledDarkMagicianEffect);
            AddExecutor(ExecutorType.Activate, CardId.SagesStone, SagesStoneEffect);

            // Dark Magician support removal.
            AddExecutor(ExecutorType.Activate, CardId.DarkMagicAttack, DarkMagicAttackEffect);
            AddExecutor(ExecutorType.Activate, CardId.ThousandKnives, ThousandKnivesEffect);
            AddExecutor(ExecutorType.Activate, CardId.MagicalDimension, MagicalDimensionEffect);

            // Removal and disruption staples with DefaultExecutor's smart timing.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, _CardId.BookOfMoon, DefaultBookOfMoon);
            AddExecutor(ExecutorType.Activate, CardId.BrainControl, BrainControlEffect);
            AddExecutor(ExecutorType.Activate, CardId.SwordsOfRevealingLight, SwordsOfRevealingLightEffect);
            AddExecutor(ExecutorType.Activate, _CardId.AllureofDarkness, AllureOfDarknessEffect);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, DefaultCallOfTheHaunted);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce, MirrorForceEffect);
            AddExecutor(ExecutorType.Activate, CardId.MagicCylinder, MagicCylinderEffect);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.DimensionalPrison, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);

            // Spellcaster monster effects.
            AddExecutor(ExecutorType.Activate, CardId.Breaker, BreakerEffect);
            AddExecutor(ExecutorType.Activate, CardId.OldVindictiveMagician, OldVindictiveMagicianEffect);
            AddExecutor(ExecutorType.Activate, CardId.NightsEndSorcerer, NightsEndSorcererEffect);

            // Synchro package.
            AddExecutor(ExecutorType.SpSummon, CardId.StardustDragon, SynchroSummon);
            AddExecutor(ExecutorType.Activate, CardId.StardustDragon, DefaultStardustDragonEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.ThoughtRulerArchfiend, SynchroSummon);
            AddExecutor(ExecutorType.Activate, CardId.ThoughtRulerArchfiend, ThoughtRulerArchfiendEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.GoyoGuardian, SynchroSummon);
            AddExecutor(ExecutorType.Activate, CardId.GoyoGuardian, GoyoGuardianEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.ExplosiveMagician, ExplosiveMagicianSummon);
            AddExecutor(ExecutorType.Activate, CardId.ExplosiveMagician, ExplosiveMagicianEffect);

            // Normal Summon priority: Tribute Summons of the big casters, then
            // Tuners that complete a Synchro, then the face-up beaters, then the
            // set flip removal.
            AddExecutor(ExecutorType.Summon, CardId.DarkMagician, DarkMagicianSummon);
            AddExecutor(ExecutorType.Summon, CardId.DarkMagicianGirl, OneTributeSummon);
            AddExecutor(ExecutorType.Summon, CardId.ChaosCommandMagician, OneTributeSummon);
            AddExecutor(ExecutorType.Summon, CardId.WitchOfTheBlackRose, WitchOfTheBlackRoseSummon);
            AddExecutor(ExecutorType.Summon, CardId.NightsEndSorcerer, NightsEndSorcererSummon);
            AddExecutor(ExecutorType.Summon, CardId.Breaker);
            AddExecutor(ExecutorType.Summon, CardId.DarkElf, DarkElfSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.OldVindictiveMagician, OldVindictiveMagicianSet);
            AddExecutor(ExecutorType.Repos, CardId.OldVindictiveMagician, OldVindictiveMagicianFlip);

            // Generic fallback play: summon what's left, set backrow, reposition.
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        private bool HasFaceupDarkMagician()
        {
            return Bot.HasInMonstersZone(CardId.DarkMagician, false, false, true);
        }

        // Dark Magician copies still reachable from hand, Deck or Graveyard.
        private bool DarkMagicianAvailable()
        {
            return Bot.HasInHand(CardId.DarkMagician) || Bot.HasInGraveyard(CardId.DarkMagician)
                || Bot.GetRemainingCount(CardId.DarkMagician, 3) > 0;
        }

        // Skilled Dark Magician is summoned face-up early, before the turn's
        // Spells, so they all add counters. One copy on the field is enough
        // while a Dark Magician is still reachable.
        private bool SkilledDarkMagicianSummon()
        {
            return !Bot.HasInMonstersZone(CardId.SkilledDarkMagician, false, false, true) && DarkMagicianAvailable();
        }

        // Skilled Dark Magician's ignition is only offered at 3 counters:
        // tribute it for a Dark Magician whenever one is still reachable.
        private bool SkilledDarkMagicianEffect()
        {
            if (!DarkMagicianAvailable())
                return false;

            AI.SelectCard(CardId.DarkMagician);
            return true;
        }

        // Sage's Stone: Special Summon Dark Magician from hand or Deck while
        // Dark Magician Girl is face-up.
        private bool SagesStoneEffect()
        {
            if (Bot.GetMonsterCount() >= 5)
                return false;
            return Bot.HasInHand(CardId.DarkMagician) || Bot.GetRemainingCount(CardId.DarkMagician, 3) > 0;
        }

        // Dark Magic Attack: wipe the opponent's Spells/Traps, worth it from 2
        // cards or from any set card before attacking.
        private bool DarkMagicAttackEffect()
        {
            int backrow = Enemy.GetSpellCount();
            if (backrow >= 2)
                return true;
            return backrow == 1 && Duel.Phase == DuelPhase.Main1;
        }

        // Thousand Knives: destroy the opponent's best monster.
        private bool ThousandKnivesEffect()
        {
            ClientCard target = Util.GetBestEnemyMonster(false, true);
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Magical Dimension: tribute our weakest monster, Special Summon the
        // strongest Spellcaster from hand and then destroy the opponent's best
        // monster; the three picks are queued in the order the card asks them. Only when the swap is an
        // upgrade and there is something to destroy.
        private bool MagicalDimensionEffect()
        {
            if (Enemy.GetMonsterCount() == 0)
                return false;

            ClientCard best = Bot.Hand
                .Where(card => card.IsMonster() && card.HasRace(CardRace.SpellCaster))
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            ClientCard worst = Util.GetWorstBotMonster();
            if (best == null || worst == null)
                return false;

            if (best.Attack <= worst.Attack)
                return false;

            AI.SelectCard(worst);
            AI.SelectNextCard(best);
            AI.SelectThirdCard(Util.GetBestEnemyMonster());
            return true;
        }

        // Pre-errata Brain Control: pay 800 LP to borrow the opponent's best
        // face-up monster for the turn, in Main Phase 1 so it can attack or
        // become Tribute fodder for Dark Magician.
        private bool BrainControlEffect()
        {
            if (Duel.Phase != DuelPhase.Main1 || Bot.LifePoints <= 800)
                return false;

            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            if (target == null || target.Attack < 1500)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Swords of Revealing Light: stall only when the opponent's board
        // outclasses ours right now.
        private bool SwordsOfRevealingLightEffect()
        {
            return Enemy.GetMonsterCount() > 0 && Util.IsOneEnemyBetter(true);
        }

        // Allure of Darkness: draw 2, then banish a DARK from hand. Keep a
        // Dark Magician in hand: only go when a cheaper DARK can be banished.
        private bool AllureOfDarknessEffect()
        {
            return Bot.Hand.Any(card => card != Card && card.HasAttribute(CardAttribute.Dark) && !card.IsCode(CardId.DarkMagician));
        }

        // Mirror Force: answer an attack that threatens the monster it hits (or
        // a direct attack).
        private bool MirrorForceEffect()
        {
            if (Duel.Player != 1 || Enemy.BattlingMonster == null)
                return false;

            ClientCard defender = Bot.BattlingMonster;
            return defender == null || Enemy.BattlingMonster.Attack >= defender.GetDefensePower();
        }

        // Magic Cylinder: bounce the attack back as burn when it is worth it.
        private bool MagicCylinderEffect()
        {
            return Duel.Player == 1 && Enemy.BattlingMonster != null && Enemy.BattlingMonster.Attack >= 1500;
        }

        // Breaker: remove its counter to destroy a Spell/Trap, only against the
        // opponent's backrow and before attacking.
        private bool BreakerEffect()
        {
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2)
                return false;

            ClientCard target = Util.GetBestEnemySpell();
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Old Vindictive Magician's FLIP destroy; the target (the opponent's
        // best monster) is chosen in OnSelectCard.
        private bool OldVindictiveMagicianEffect()
        {
            return true;
        }

        // Night's End Sorcerer: when Special Summoned, banish up to 2 cards from
        // the opponent's Graveyard; always free value.
        private bool NightsEndSorcererEffect()
        {
            return true;
        }

        // Synchro Summons: the engine only offers them with a legal Tuner +
        // non-Tuner pair on board; pick which bodies to spend.
        private bool SynchroSummon()
        {
            AI.SelectMaterials(SynchroMaterials);
            return true;
        }

        // Explosive Magician needs Spell Counters on our field to do anything;
        // Goyo Guardian is the default Level 6 otherwise.
        private bool ExplosiveMagicianSummon()
        {
            bool counterHolder = Bot.GetMonsters().Any(card => card.IsFaceup()
                && (card.IsCode(CardId.SkilledDarkMagician) || card.IsCode(CardId.Breaker)));
            if (!counterHolder && Bot.HasInExtra(CardId.GoyoGuardian))
                return false;
            return SynchroSummon();
        }

        // Explosive Magician: remove 2 Spell Counters from our field to destroy
        // an opponent's Spell/Trap (the engine only offers it with 2 counters
        // available); aim at the most dangerous one.
        private bool ExplosiveMagicianEffect()
        {
            ClientCard target = Util.GetBestEnemySpell();
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Goyo Guardian: take control of the monster it destroyed in battle.
        private bool GoyoGuardianEffect()
        {
            return true;
        }

        // Thought Ruler Archfiend: pay 1000 LP to negate an opponent's effect
        // that targets exactly 1 Psychic — which protects Thought Ruler itself.
        private bool ThoughtRulerArchfiendEffect()
        {
            if (Bot.LifePoints <= 1000 || Duel.LastChainPlayer != 1)
                return false;
            return Util.IsChainTarget(Card);
        }

        // Dark Magician: Tribute Summon with 2 bodies weaker than it, keeping
        // Skilled Dark Magician (which can summon it for free) out of it when
        // there is another option.
        private bool DarkMagicianSummon()
        {
            List<ClientCard> fodder = Bot.GetMonsters()
                .Where(card => card.GetDefensePower() < 2500 && !card.IsCode(CardId.DarkMagician))
                .ToList();
            if (fodder.Count < 2)
                return false;
            return fodder.Count(card => !card.IsCode(CardId.SkilledDarkMagician)) >= 2 || Enemy.GetMonsterCount() > 0;
        }

        // Dark Magician Girl / Chaos Command Magician: Tribute Summon over a
        // weaker body (never over Dark Magician).
        private bool OneTributeSummon()
        {
            ClientCard worst = Bot.GetMonsters()
                .Where(card => !card.IsCode(CardId.DarkMagician))
                .OrderBy(card => card.GetDefensePower())
                .FirstOrDefault();
            return worst != null && worst.GetDefensePower() < Card.Attack;
        }

        // Witch of the Black Rose: a Level 4 Tuner. Summon it next to a face-up
        // Level 4 (Level 8 Synchro) or Level 2 (Level 6 Synchro) non-Tuner, or
        // onto an empty field where its draw effect triggers.
        private bool WitchOfTheBlackRoseSummon()
        {
            if (Bot.GetFieldCount() == 0)
                return true;
            return Bot.GetMonsters().Any(card => card.IsFaceup() && !card.IsTuner() && (card.Level == 4 || card.Level == 2));
        }

        // Night's End Sorcerer: a Level 2 Tuner. Summon it next to a face-up
        // Level 4 (Level 6 Synchro) or Level 6 (Level 8 Synchro) non-Tuner.
        private bool NightsEndSorcererSummon()
        {
            return Bot.GetMonsters().Any(card => card.IsFaceup() && !card.IsTuner() && (card.Level == 4 || card.Level == 6));
        }

        // Dark Elf pays 1000 LP per attack: only summon it face-up while we can
        // afford that.
        private bool DarkElfSummon()
        {
            return Bot.LifePoints > 3000;
        }

        // Old Vindictive Magician is set for its FLIP destroy whenever the
        // opponent has a monster to hit.
        private bool OldVindictiveMagicianSet()
        {
            return Enemy.GetMonsterCount() > 0;
        }

        // Flip-summon it once a target is on the opponent's field.
        private bool OldVindictiveMagicianFlip()
        {
            return Card.IsFacedown() && Enemy.GetMonsterCount() > 0;
        }

        // How readily a card can be banished/discarded (higher = sooner). Dark
        // Magician is the deck's centrepiece and goes last.
        private int DiscardScore(ClientCard card)
        {
            if (card.IsCode(CardId.DarkMagician))
                return 0;
            if (card.IsCode(CardId.DarkMagicianGirl) || card.IsCode(CardId.SkilledDarkMagician))
                return 20;
            if (card.IsCode(CardId.NightsEndSorcerer) || card.IsCode(CardId.WitchOfTheBlackRose))
                return 60;
            if (card.IsCode(CardId.DarkElf))
                return 70;
            return 40;
        }

        // Magical Dimension's optional "destroy 1 monster" prompt: only when the
        // opponent still has one.
        public override bool OnSelectYesNo(int desc)
        {
            if (desc == Util.GetStringId(CardId.MagicalDimension, 0))
                return Enemy.GetMonsterCount() > 0;
            return base.OnSelectYesNo(desc);
        }

        // Card-selection smarts per hint; an explicit queued selection always
        // takes precedence.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (!AI.HaveSelectedCards())
            {
                // Magical Dimension / Skilled Dark Magician / Sage's Stone: bring
                // out the strongest Spellcaster offered.
                if (hint == HintMsg.SpSummon && cards.All(card => card.Controller == 0))
                {
                    return cards.OrderByDescending(card => card.Attack).Take(min).ToList();
                }

                // Night's End Sorcerer banishes from the opponent's Graveyard
                // (monsters first); Allure of Darkness banishes our cheapest DARK.
                if (hint == HintMsg.Remove)
                {
                    List<ClientCard> enemyGrave = cards
                        .Where(card => card.Controller == 1)
                        .OrderByDescending(card => card.IsMonster() ? 1 : 0)
                        .ToList();
                    if (enemyGrave.Count > 0)
                    {
                        IList<ClientCard> picked = Util.CheckSelectCount(enemyGrave.Take(max).ToList(), cards, min, max);
                        if (picked != null)
                            return picked;
                    }
                    else if (cards.All(card => card.Location == CardLocation.Hand))
                    {
                        return cards.OrderByDescending(DiscardScore).Take(min).ToList();
                    }
                }

                if (hint == HintMsg.Discard && cards.All(card => card.Location == CardLocation.Hand))
                {
                    return cards.OrderByDescending(DiscardScore).Take(min).ToList();
                }

                // Old Vindictive Magician, Magical Dimension's destroy and any
                // untargeted destroy: hit the opponent's strongest card first,
                // never our own.
                if (hint == HintMsg.Destroy)
                {
                    List<ClientCard> enemy = cards
                        .Where(card => card.Controller == 1)
                        .OrderByDescending(card => card.IsMonster() ? card.GetDefensePower() : 0)
                        .ToList();
                    IList<ClientCard> picked = Util.CheckSelectCount(enemy, cards, min, max);
                    if (enemy.Count > 0 && picked != null)
                        return picked;
                }
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }
    }
}
