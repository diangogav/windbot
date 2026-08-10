using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Lightsworn mill/beatdown bot for the Edison format (TCG April 2010 card
    // pool). Mills itself toward Judgment Dragon with Solar Recharge and Charge
    // of the Light Brigade, floats Wulf and Card Trooper as free/cheap beaters,
    // uses Ryko and Super-Nimble Mega Hamster as set flip-removal, and closes
    // games with Judgment Dragon's free field wipe or a Synchro push (Goyo
    // Guardian / Brionac / Stardust / Black Rose) off Plaguespreader Zombie.
    // Several card IDs below are PRE-ERRATA passcodes required by the Edison
    // whitelist and do NOT match the cards' modern/official passcodes: Ryko,
    // Honest, Armory Arm, Brionac and Goyo Guardian. Those are defined locally
    // and must never be looked up through DefaultExecutor's _CardId.
    // Deck: AI_EdisonLightsworn.ydk (Edison-whitelist-legal Lightsworn mill).
    [Deck("EdisonLightsworn", "AI_EdisonLightsworn")]
    public class EdisonLightswornExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Win condition.
            public const int JudgmentDragon = 57774843;

            // Lightsworn monsters.
            public const int Ryko = 511003007; // PRE-ERRATA passcode (Edison whitelist).
            public const int Wulf = 58996430;
            public const int Lyla = 22624373;
            public const int Jain = 96235275;
            public const int Celestia = 94381039;
            public const int Lumina = 95503687;
            public const int Ehren = 44178886;
            public const int Garoth = 59019082;

            // Support monsters.
            public const int SuperNimbleMegaHamster = 5220687;
            public const int CardTrooper = 85087012;
            public const int PlaguespreaderZombie = 33420078;
            public const int NecroGardna = 4906301;
            public const int DDWarriorLady = 7572887;
            public const int Honest = 910003001; // PRE-ERRATA passcode (Edison whitelist).

            // Spells.
            public const int ChargeOfTheLightBrigade = 94886282;
            public const int SolarRecharge = 691925;
            public const int FoolishBurial = 81439173;
            public const int GiantTrunade = 42703248;
            public const int ReinforcementOfTheArmy = 32807846;
            public const int ColdWave = 60682203;
            public const int GoldSarcophagus = 75500286;

            // Traps.
            public const int MirrorForce = 44095762;
            public const int TrapDustshoot = 64697231;

            // Extra Deck.
            public const int ArmoryArm = 910003009; // PRE-ERRATA passcode (Edison whitelist).
            public const int AllyOfJusticeCatastor = 26593852;
            public const int MagicalAndroid = 43385557;
            public const int Brionac = 511002993; // PRE-ERRATA passcode (Edison whitelist).
            public const int FlamvellUruquizas = 53714009;
            public const int GoyoGuardian = 511002994; // PRE-ERRATA passcode (Edison whitelist).
            public const int TempestMagician = 63101919;
            public const int BlackRoseDragon = 73580471;
            public const int ColossalFighter = 23693634;
            public const int DarkEndDragon = 88643579;
            public const int RedDragonArchfiend = 70902743;
            public const int StardustDragon = 44508094;
            public const int ThoughtRulerArchfiend = 70780151;
            public const int ChimeratechFortressDragon = 79229522;
        }

        // Synchro material priority lists, grouped by target Level. The engine
        // only offers a SpSummon slot for an Extra Deck monster once a legal
        // Tuner + non-Tuner combination is actually on the field, so our job is
        // just picking WHICH bodies to spend, in order (cheap/expendable first,
        // keep the strongest beaters on board whenever another combo is available).
        private static readonly int[] Level4SynchroMaterials =
        {
            CardId.PlaguespreaderZombie, CardId.Ryko
        };

        private static readonly int[] Level6SynchroMaterials =
        {
            CardId.PlaguespreaderZombie, CardId.NecroGardna, CardId.CardTrooper, CardId.Ryko,
            CardId.Lumina, CardId.Ehren, CardId.Garoth, CardId.Jain, CardId.Lyla, CardId.Wulf,
            CardId.DDWarriorLady, CardId.SuperNimbleMegaHamster, CardId.Celestia
        };

        private static readonly int[] Level7SynchroMaterials =
        {
            CardId.CardTrooper, CardId.Ehren, CardId.Garoth, CardId.Jain, CardId.Lyla,
            CardId.Wulf, CardId.DDWarriorLady, CardId.SuperNimbleMegaHamster
        };

        private static readonly int[] Level8SynchroMaterials =
        {
            CardId.NecroGardna, CardId.Ehren, CardId.Garoth, CardId.Jain, CardId.Lyla,
            CardId.Wulf, CardId.DDWarriorLady, CardId.SuperNimbleMegaHamster,
            CardId.PlaguespreaderZombie, CardId.Celestia
        };

        // Tracks whether this turn's single live Honest has already been
        // reserved by an earlier attacker in OnSelectAttackTarget/OnPreBattleBetween.
        private int _honestReservedTurn = -1;
        private bool _honestReserved;

        public EdisonLightswornExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Judgment Dragon: our win condition. The engine only offers this
            // SpSummon slot once 4+ differently-named Lightsworn monsters sit in
            // the Graveyard, so an unconditional rule is safe — always cash it in,
            // including a second copy for a lethal double push.
            AddExecutor(ExecutorType.SpSummon, CardId.JudgmentDragon);
            // Field ignition: pay 1000 LP, destroy every OTHER card on the field
            // (both players'). Only pull the trigger on a clearly profitable wipe.
            AddExecutor(ExecutorType.Activate, CardId.JudgmentDragon, JudgmentDragonNukeEffect);

            // Removal / disruption staples with DefaultExecutor's smart timing.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.TrapDustshoot, TrapDustshootEffect);
            AddExecutor(ExecutorType.Activate, CardId.GiantTrunade, GiantTrunadeEffect);
            AddExecutor(ExecutorType.Activate, CardId.ColdWave, ColdWaveEffect);

            // Card advantage / mill engine toward Judgment Dragon.
            AddExecutor(ExecutorType.Activate, CardId.SolarRecharge);
            AddExecutor(ExecutorType.Activate, CardId.ChargeOfTheLightBrigade, ChargeOfTheLightBrigadeEffect);
            AddExecutor(ExecutorType.Activate, CardId.ReinforcementOfTheArmy, ReinforcementOfTheArmyEffect);
            AddExecutor(ExecutorType.Activate, CardId.GoldSarcophagus, GoldSarcophagusEffect);
            AddExecutor(ExecutorType.Activate, CardId.FoolishBurial, FoolishBurialEffect);

            // Honest: LIGHT combat trick, reuse DefaultExecutor's proven timing.
            AddExecutor(ExecutorType.Activate, CardId.Honest, DefaultHonestEffect);

            // Synchro package. Plaguespreader Zombie can be Normal Summoned like
            // any other Tuner, or paid back from the Graveyard via its ignition
            // effect — both gated on PlaguespreaderZombieSummon() so the Normal
            // Summon isn't wasted on it when no partner is on board (that slot
            // is needed by Card Trooper / Lumina / Lyla / Ryko below instead).
            AddExecutor(ExecutorType.Summon, CardId.PlaguespreaderZombie, PlaguespreaderZombieSummon);
            AddExecutor(ExecutorType.Activate, CardId.PlaguespreaderZombie, PlaguespreaderZombieRevive);

            AddExecutor(ExecutorType.SpSummon, CardId.GoyoGuardian, GoyoGuardianSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Brionac, BrionacSummon);
            AddExecutor(ExecutorType.Activate, CardId.Brionac, BrionacEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.AllyOfJusticeCatastor, AllyOfJusticeCatastorSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.MagicalAndroid, MagicalAndroidSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.TempestMagician, TempestMagicianSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.BlackRoseDragon, BlackRoseDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.StardustDragon, StardustDragonSummon);
            AddExecutor(ExecutorType.Activate, CardId.StardustDragon, StardustDragonEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.RedDragonArchfiend, RedDragonArchfiendSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.ThoughtRulerArchfiend, ThoughtRulerArchfiendSummon);
            AddExecutor(ExecutorType.Activate, CardId.ThoughtRulerArchfiend, ThoughtRulerArchfiendEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.DarkEndDragon, DarkEndDragonSummon);
            AddExecutor(ExecutorType.Activate, CardId.DarkEndDragon, DarkEndDragonEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.ColossalFighter, ColossalFighterSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.FlamvellUruquizas, FlamvellUruquizasSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.ArmoryArm, ArmoryArmSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.ChimeratechFortressDragon);

            // Card Trooper: mill 3 for the max ATK pump, then swing.
            AddExecutor(ExecutorType.Summon, CardId.CardTrooper);
            AddExecutor(ExecutorType.Activate, CardId.CardTrooper, CardTrooperEffect);

            // Lumina: always Summon face-up so her Summon-trigger revival fires;
            // only 1 copy in the deck, so never waste the revival on nothing.
            AddExecutor(ExecutorType.Summon, CardId.Lumina);
            AddExecutor(ExecutorType.Activate, CardId.Lumina, LuminaEffect);

            // Celestia: Tribute (Advance) Summoned from hand by releasing a
            // Lightsworn — not Special Summoned. Her mill-4+destroy trigger is
            // an optional Summon-Success trigger with its own Activate executor
            // below, not folded into the Summon decision.
            AddExecutor(ExecutorType.Summon, CardId.Celestia, CelestiaSummon);
            AddExecutor(ExecutorType.Activate, CardId.Celestia, CelestiaEffect);

            // Lyla: pop a set backrow card; otherwise just a 1900 beater.
            AddExecutor(ExecutorType.Summon, CardId.Lyla);
            AddExecutor(ExecutorType.Activate, CardId.Lyla, LylaEffect);

            // Ryko / Super-Nimble Mega Hamster: always Set for flip value —
            // never Normal Summoned face-up.
            AddExecutor(ExecutorType.MonsterSet, CardId.Ryko);
            AddExecutor(ExecutorType.Activate, CardId.Ryko);
            AddExecutor(ExecutorType.MonsterSet, CardId.SuperNimbleMegaHamster);
            AddExecutor(ExecutorType.Activate, CardId.SuperNimbleMegaHamster, SuperNimbleMegaHamsterEffect);

            // Necro Gardna: Summon it face-up as a body when useful; its real
            // value (banish from the GY to negate an attack) is automatic and
            // needs no gating beyond legality.
            AddExecutor(ExecutorType.Summon, CardId.NecroGardna);
            AddExecutor(ExecutorType.Activate, CardId.NecroGardna);

            // D.D. Warrior Lady: banish it together with a real threat it battles.
            AddExecutor(ExecutorType.Summon, CardId.DDWarriorLady);
            AddExecutor(ExecutorType.Activate, CardId.DDWarriorLady, DDWarriorLadyEffect);

            // Plain beaters: always Normal Summon face-up, never set.
            AddExecutor(ExecutorType.Summon, CardId.Wulf);
            AddExecutor(ExecutorType.Summon, CardId.Jain);
            AddExecutor(ExecutorType.Summon, CardId.Garoth);
            AddExecutor(ExecutorType.Summon, CardId.Ehren);

            // Generic play: summon/set whatever is left, set backrow, reposition.
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        // Field ignition: pay 1000 LP, destroy every OTHER card on the field.
        // Only worth it when it clears more of the opponent's board than our own,
        // we can afford the LP, and we aren't about to blow away a second copy
        // of our own win condition sitting alongside this one.
        private bool JudgmentDragonNukeEffect()
        {
            if (Bot.LifePoints <= 1000)
                return false;

            int enemyCards = Enemy.GetMonsterCount() + Enemy.GetSpellCount();
            if (enemyCards < 2)
                return false; // not enough on the opponent's side to justify it.

            int ourOtherCards = (Bot.GetMonsterCount() - 1) + Bot.GetSpellCount(); // this JD survives.
            if (ourOtherCards > enemyCards)
                return false; // we would lose more than the opponent gains us.

            if (Bot.GetMonsters().Count(m => m != Card && m.IsCode(CardId.JudgmentDragon)) > 0)
                return false; // never blow away our own second Judgment Dragon.

            return true;
        }

        // Trap Dustshoot: activate only during the opponent's Standby Phase to
        // strip a card from their hand back to the top of the Deck before they
        // can act on it that turn.
        private bool TrapDustshootEffect()
        {
            return Duel.Phase == DuelPhase.Standby && Duel.Player == 1;
        }

        // Giant Trunade bounces ALL Spells/Traps on both sides back to hand —
        // only worth it when the opponent has more backrow to strip than we do,
        // so we aren't just undoing our own setup.
        private bool GiantTrunadeEffect()
        {
            int enemyBackrow = Enemy.GetSpellCount();
            if (enemyBackrow == 0)
                return false;

            int ourBackrow = Bot.GetSpellCountWithoutField();
            return enemyBackrow >= ourBackrow;
        }

        // Cold Wave locks BOTH players' Spells/Traps until our next turn — a
        // defensive/offensive lock, not free value. Only worth it right before a
        // big push (Judgment Dragon live, or lethal-ish board already assembled),
        // and only in Main Phase 1 before we've committed the rest of our turn.
        private bool ColdWaveEffect()
        {
            if (Duel.Phase != DuelPhase.Main1)
                return false;
            if (Bot.HasInMonstersZone(CardId.JudgmentDragon, false, false, true))
                return true;

            int boardAttack = Bot.GetMonsters().Where(m => m.IsAttack()).Sum(m => m.Attack);
            return boardAttack >= Enemy.LifePoints;
        }

        // Charge of the Light Brigade: mill 3, then add back the best support
        // piece — Lumina first (more revival value later), then Ryko (removal),
        // then Lyla (more removal). Celestia is Level 5 and can never be found
        // by this card (its search filter is Level <= 4 Lightsworn only). The
        // engine skips unavailable candidates.
        private bool ChargeOfTheLightBrigadeEffect()
        {
            AI.SelectCard(CardId.Lumina, CardId.Ryko, CardId.Lyla);
            return true;
        }

        // Reinforcement of the Army: search a Warrior-Type Lightsworn we don't
        // already have in hand.
        private bool ReinforcementOfTheArmyEffect()
        {
            AI.SelectCard(CardId.DDWarriorLady, CardId.Garoth, CardId.Jain);
            return true;
        }

        // Gold Sarcophagus: banish now, add to hand in 2 turns. Prioritize
        // finding Judgment Dragon; otherwise restock the draw/removal engine.
        private bool GoldSarcophagusEffect()
        {
            if (!Bot.HasInHand(CardId.JudgmentDragon))
                AI.SelectCard(CardId.JudgmentDragon);
            else
                AI.SelectCard(CardId.SolarRecharge, _CardId.HeavyStorm);
            return true;
        }

        // Foolish Burial: send a monster from the Deck to the GY. Wulf treats
        // being milled this way as a trigger for its own free Special Summon, so
        // it is always the first choice; otherwise just build the Judgment
        // Dragon count with a name we don't have in the GY yet.
        private bool FoolishBurialEffect()
        {
            if (!Bot.HasInGraveyard(CardId.Wulf))
            {
                AI.SelectCard(CardId.Wulf);
                return true;
            }
            AI.SelectCard(CardId.Garoth, CardId.Jain, CardId.Celestia, CardId.Lyla, CardId.Ehren);
            return true;
        }

        // Shared gate for getting Plaguespreader Zombie onto the field: only
        // spend the Normal Summon on it, or pay its Graveyard ignition's hand
        // cost to revive it, when a Level 4 or 6 partner is already on board so
        // a Synchro Summon actually follows — a Level 4 non-Tuner turns it into
        // a Level 6 (Goyo Guardian / Brionac), a Level 6 (Celestia) turns it into
        // a Level 8 (Stardust / Red Dragon Archfiend). Without a real partner on
        // board it isn't worth spending the turn's one Normal Summon (or
        // reviving a 400 ATK body from the GY) on it.
        private bool PlaguespreaderZombieSummon()
        {
            return Bot.GetMonsters().Any(m => m.IsFaceup() && (m.Level == 4 || m.Level == 6));
        }

        // Plaguespreader Zombie's Graveyard ignition effect: put a card from
        // hand on top of the Deck as a cost, then Special Summon it back. This
        // is an EFFECT_TYPE_IGNITION effect (shows up as an activatable card),
        // not a Special Summon proc, so it must be registered as Activate, not
        // SpSummon. Gated by the same board check as the Normal Summon above;
        // the ToDeck cost selection (worst hand card, never Judgment
        // Dragon/Honest) is resolved in OnSelectCard.
        private bool PlaguespreaderZombieRevive()
        {
            return PlaguespreaderZombieSummon();
        }

        private bool SynchroSummonEffect(int[] materialPriority)
        {
            AI.SelectMaterials(materialPriority);
            return true;
        }

        private bool GoyoGuardianSummon()
        {
            return SynchroSummonEffect(Level6SynchroMaterials);
        }

        private bool BrionacSummon()
        {
            return SynchroSummonEffect(Level6SynchroMaterials);
        }

        // Brionac's on-field ignition: discard 1 card, bounce up to 2 cards. Only
        // worth the discard when the opponent actually has something worth
        // bouncing back to their hand.
        private bool BrionacEffect()
        {
            if (Enemy.GetMonsterCount() == 0 && Enemy.GetSpellCount() == 0)
                return false;

            // The discard cost is exactly 1 card; selecting by ID would grab
            // EVERY matching card in hand up to the prompt's max (which can be 2
            // when both bounce slots are live), overpaying the cost. Pick a
            // single ClientCard instead, reusing the same worst-first scoring as
            // the Discard-hint handler below.
            ClientCard discard = Bot.Hand.OrderByDescending(DiscardScore).FirstOrDefault();
            if (discard == null)
                return false;

            AI.SelectCard(discard);
            return true;
        }

        private bool AllyOfJusticeCatastorSummon()
        {
            return SynchroSummonEffect(Level6SynchroMaterials);
        }

        private bool MagicalAndroidSummon()
        {
            return SynchroSummonEffect(Level6SynchroMaterials);
        }

        private bool TempestMagicianSummon()
        {
            return SynchroSummonEffect(Level6SynchroMaterials);
        }

        // Black Rose Dragon wipes every OTHER card on the field on Summon — only
        // worth committing the materials when the opponent actually has a board.
        private bool BlackRoseDragonSummon()
        {
            if (Enemy.GetMonsterCount() == 0)
                return false;
            return SynchroSummonEffect(Level7SynchroMaterials);
        }

        private bool StardustDragonSummon()
        {
            return SynchroSummonEffect(Level8SynchroMaterials);
        }

        // Stardust Dragon: Tribute itself to negate a destruction effect, then
        // comes back at the End Phase. DefaultStardustDragonEffect() only says
        // yes when the destruction is the OPPONENT'S (or Stardust is already in
        // the GY reviving for free) — an unconditional true would negate our own
        // Judgment Dragon nuke, Black Rose Dragon, Ryko or Torrential Tribute.
        private bool StardustDragonEffect()
        {
            return DefaultStardustDragonEffect();
        }

        private bool RedDragonArchfiendSummon()
        {
            return SynchroSummonEffect(Level8SynchroMaterials);
        }

        private bool ThoughtRulerArchfiendSummon()
        {
            return SynchroSummonEffect(Level8SynchroMaterials);
        }

        // Thought Ruler Archfiend's real effects are a mandatory LP-recovery
        // trigger when it destroys a monster by battle, and a Quick Effect that
        // negates an activated effect — but only one targeting exactly 1
        // Psychic-Type monster, which this deck never has to deal with. Neither
        // matches an ATK-discard trick, so this executor never actually fires;
        // kept registered as a harmless no-op.
        private bool ThoughtRulerArchfiendEffect()
        {
            if (Bot.BattlingMonster == null || Enemy.BattlingMonster == null)
                return false;
            return Bot.BattlingMonster.Attack < Enemy.BattlingMonster.Attack;
        }

        private bool DarkEndDragonSummon()
        {
            return SynchroSummonEffect(Level8SynchroMaterials);
        }

        // Dark End Dragon: ignition effect that targets and sends an opponent's
        // monster (500+ ATK/DEF) straight to the Graveyard, at the cost of
        // Dark End Dragon itself taking a permanent -500/-500. Always worth it
        // when the opponent has a real target; queue their best monster instead
        // of leaving the choice to the default resolver.
        private bool DarkEndDragonEffect()
        {
            ClientCard target = Util.GetBestEnemyMonster(false, true) ?? Enemy.GetMonsters().FirstOrDefault();
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Colossal Fighter is Level 8.
        private bool ColossalFighterSummon()
        {
            return SynchroSummonEffect(Level8SynchroMaterials);
        }

        // Flamvell Uruquizas is Level 6.
        private bool FlamvellUruquizasSummon()
        {
            return SynchroSummonEffect(Level6SynchroMaterials);
        }

        // Armory Arm is Level 4 (e.g. Plaguespreader Zombie's Level 2 Tuner +
        // Ryko's Level 2 non-Tuner already covers it). The engine only offers
        // this SpSummon slot once a legal Tuner + non-Tuner combination summing
        // to Level 4 is actually on the field.
        private bool ArmoryArmSummon()
        {
            return SynchroSummonEffect(Level4SynchroMaterials);
        }

        // Card Trooper: always mill the maximum (3) for the biggest ATK pump.
        private bool CardTrooperEffect()
        {
            AI.SelectNumber(3);
            return true;
        }

        // Lumina: discard cost to Special Summon a Level <= 4 Lightsworn from
        // the GY — the script accepts any of them, not just Wulf/Lyla/Jain.
        // Necro Gardna is free value as the discard (it wants to be in the GY
        // anyway); revival priority is Wulf (best body) > Lyla (removal) > Jain
        // (beater) > Ryko / Garoth / Ehren when the preferred ones are absent.
        private bool LuminaEffect()
        {
            if (!Bot.HasInGraveyard(CardId.Wulf) && !Bot.HasInGraveyard(CardId.Lyla) && !Bot.HasInGraveyard(CardId.Jain)
                && !Bot.HasInGraveyard(CardId.Ryko) && !Bot.HasInGraveyard(CardId.Garoth) && !Bot.HasInGraveyard(CardId.Ehren))
                return false;

            AI.SelectCard(CardId.NecroGardna, CardId.Ryko, CardId.Wulf, CardId.Honest, CardId.Ehren, CardId.Garoth);
            AI.SelectNextCard(CardId.Wulf, CardId.Lyla, CardId.Jain, CardId.Ryko, CardId.Garoth, CardId.Ehren);
            return true;
        }

        // Celestia: Tribute (Advance) Summon by releasing 1 monster, preferring
        // a spent/weak Lightsworn over our best beater (Wulf). Only worth the
        // tribute when there is a real 2-for-1 (or better) to make; her
        // mill-4+destroy trigger itself lives in CelestiaEffect below.
        private bool CelestiaSummon()
        {
            if (Enemy.GetMonsterCount() + Enemy.GetSpellCount() < 2)
                return false;

            AI.SelectCard(CardId.Jain, CardId.Lumina, CardId.Lyla, CardId.Ehren, CardId.Garoth, CardId.Ryko, CardId.Wulf);
            return true;
        }

        // Celestia's Summon-trigger: mill 4 (cost), then destroy 1-2 of the
        // opponent's cards. Optional, but CelestiaSummon() above already
        // confirmed a Lightsworn was tributed and the opponent has 2+ cards, so
        // it's safe to always accept; targets are resolved by the shared
        // Destroy-hint handler in OnSelectCard below.
        private bool CelestiaEffect()
        {
            return true;
        }

        // Lyla: pop a Spell/Trap Card. Only worth using when the opponent
        // actually has one set; otherwise she just stays a 1900 beater.
        private bool LylaEffect()
        {
            return Enemy.GetSpellCount() > 0;
        }

        // Ryko: FLIP - mill 3, then optionally destroy a card. Always take the
        // free mill toward Judgment Dragon; the destroy target (if any) is
        // resolved through the shared Destroy-hint handler in OnSelectCard,
        // which falls back to our own worst card (never cards[0]) if the
        // opponent's field is empty when the "destroy?" prompt is forced.
        // (No custom logic needed: registered with a null func above.)

        // Super-Nimble Mega Hamster: FLIP - Special Summon Ryko face-down from
        // the Deck, refilling our set flip-removal. Deck-side ClientCards are
        // placeholders (id 0) until revealed, so Bot.Deck.Any(IsCode(...)) can
        // never match anything — use the house "remaining copies" idiom instead
        // (this deck runs 3x Ryko).
        private bool SuperNimbleMegaHamsterEffect()
        {
            if (Bot.GetRemainingCount(CardId.Ryko, 3) <= 0)
                return false;

            AI.SelectCard(CardId.Ryko);
            return true;
        }

        // D.D. Warrior Lady: banish herself together with the monster she
        // battles — only worth it against a real threat, not a trade for nothing.
        private bool DDWarriorLadyEffect()
        {
            return Bot.BattlingMonster != null && Enemy.BattlingMonster != null
                && Enemy.BattlingMonster.Attack >= 1600;
        }

        // How readily we can part with a card when forced to discard (HIGHER =
        // pitch sooner). Necro Gardna is free value (it wants to be in the GY to
        // negate an attack), extra copies of our 3-ofs are cheap fodder, a second
        // Honest can go since the first stays as a combat trick, and Judgment
        // Dragon / our only Celestia are never touched voluntarily.
        private int DiscardScore(ClientCard card)
        {
            if (card.IsCode(CardId.NecroGardna))
                return 100;
            if (card.IsCode(CardId.Wulf) || card.IsCode(CardId.Ryko))
                return 80;
            if (card.IsCode(CardId.Honest))
                return Bot.Hand.Count(c => c.IsCode(CardId.Honest)) >= 2 ? 70 : 5;
            if (card.IsCode(CardId.JudgmentDragon))
                return 0;
            if (card.IsCode(CardId.Celestia))
                return Bot.Hand.Count(c => c.IsCode(CardId.Celestia)) >= 2 ? 60 : 1;
            return 40;
        }

        // Centralised card-selection smarts, handled per hint so it's independent
        // of the order the engine asks. An explicit queued selection
        // (AI.SelectCard / AI.SelectNextCard) always takes precedence.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (!AI.HaveSelectedCards())
            {
                // Discard (generic hand-discard costs) and ToDeck (Plaguespreader
                // Zombie's Graveyard revival cost) both want the same thing: give
                // up the card we can most afford to lose first — never Judgment
                // Dragon, almost never Honest.
                if ((hint == HintMsg.Discard || hint == HintMsg.ToDeck) && cards.Count >= min)
                {
                    List<ClientCard> ordered = new List<ClientCard>(cards);
                    ordered.Sort((a, b) => DiscardScore(b).CompareTo(DiscardScore(a))); // discard/return-first first.
                    return ordered.Take(min).ToList();
                }

                // Destroy (Ryko flip, Celestia, Lyla), ReturnToHand (Brionac) and
                // Control (Goyo Guardian) all want the same thing: hit the
                // opponent's biggest monster/spell first, never our own.
                if (hint == HintMsg.Destroy || hint == HintMsg.ReturnToHand || hint == HintMsg.Control)
                {
                    List<ClientCard> enemyCards = Enemy.GetMonsters().Concat(Enemy.GetSpells()).ToList();
                    List<ClientCard> targets = cards
                        .Where(card => enemyCards.Contains(card))
                        .OrderByDescending(card => card.HasType(CardType.Monster) ? card.Attack : 0)
                        .Take(max)
                        .ToList();

                    if (targets.Count > 0)
                    {
                        // .Take(max) alone can leave us short of min for a fixed-count
                        // prompt (e.g. Brionac bouncing exactly 2 with only 1 real
                        // enemy target available) — pad up to a legal selection
                        // instead of returning a too-short list.
                        IList<ClientCard> padded = Util.CheckSelectCount(targets, cards, min, max);
                        if (padded != null)
                            return padded;
                    }
                    else if (min > 0)
                    {
                        // No legitimate enemy target at all (e.g. Ryko's flip firing
                        // with an empty opponent field). If the engine still forces a
                        // selection, give up our own least valuable card instead of
                        // falling through to the base fallback, which blindly takes
                        // cards[0] — possibly Judgment Dragon.
                        ClientCard ownWorst = cards
                            .Where(card => card.Controller == 0)
                            .OrderBy(card => card.GetDefensePower())
                            .FirstOrDefault();
                        if (ownWorst != null)
                        {
                            IList<ClientCard> fallback = Util.CheckSelectCount(new List<ClientCard> { ownWorst }, cards, min, max);
                            if (fallback != null)
                                return fallback;
                        }
                    }
                }

                if (hint == HintMsg.ToGrave && cards.Count >= min)
                {
                    return cards.Take(min).ToList(); // Solar Recharge / Card Trooper mill: no real choice, keep top order.
                }
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        // Honest gives a LIGHT attacker ATK equal to its opponent's monster,
        // during damage calculation — factor it into our attack-target planning
        // so a fight that looks unfavorable on paper is still won. Only 1 copy
        // can ever be live at once, so once an attacker actually needs the boost
        // to win or trade, treat it as spent for the rest of the Battle Phase
        // instead of letting every other LIGHT attacker assume the same single
        // card saves them too.
        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (Duel.Turn != _honestReservedTurn)
            {
                _honestReservedTurn = Duel.Turn;
                _honestReserved = false;
            }

            if (!defender.IsMonsterHasPreventActivationEffectInBattle())
            {
                if (!_honestReserved && attacker.HasAttribute(CardAttribute.Light) && Bot.HasInHand(CardId.Honest))
                    attacker.RealPower = attacker.RealPower + defender.Attack;
            }
            return base.OnPreBattleBetween(attacker, defender);
        }

        // Aggressive attack policy: Ehren always hits a face-down Defense
        // Position monster (her effect neutralizes it without a flip trigger),
        // and Honest (factored in via OnPreBattleBetween) can turn a losing fight
        // into a win, so trade into even fights instead of holding back.
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (attacker.IsCode(CardId.Ehren))
            {
                ClientCard facedown = defenders.FirstOrDefault(d => d.IsFacedown());
                if (facedown != null)
                    return AI.Attack(attacker, facedown);
            }

            foreach (ClientCard defender in defenders)
            {
                attacker.RealPower = attacker.Attack;
                defender.RealPower = defender.GetDefensePower();
                int attackBeforeHonest = attacker.RealPower;
                if (!OnPreBattleBetween(attacker, defender))
                    continue;

                bool canKill = attacker.RealPower > defender.RealPower;
                bool evenTrade = attacker.RealPower >= defender.RealPower && defender.IsAttack();
                if (canKill || evenTrade)
                {
                    if (attacker.RealPower > attackBeforeHonest)
                        _honestReserved = true; // this win/trade needed the Honest boost.
                    return AI.Attack(attacker, defender);
                }
            }

            if (attacker.CanDirectAttack)
                return AI.Attack(attacker, null);

            return null;
        }
    }
}
