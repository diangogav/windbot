using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Zombie Synchro boss bot for the Edison format (TCG April 2010 card
    // pool). The deck climbs into Synchros off two Tuners: Plaguespreader
    // Zombie (Level 2, revives itself from the GY by top-decking a card) and
    // Dark Resonator (Level 3). Plaguespreader + a Level 4 Zombie makes
    // Doomkaiser Dragon (steals a Zombie from the opponent's GY) or Revived
    // King Ha Des; + Vampire Lord makes Black Rose Dragon; + Il Blud makes a
    // Level 8 finisher. Every Zombie is recursion: Zombie Master discards a
    // monster to revive a Level 4 or lower Zombie from either GY (usually
    // Plaguespreader), Mezuki banishes itself from the GY to revive any
    // Zombie, Book of Life revives a Zombie while banishing the opponent's
    // GY, Il Blud summons Zombies from hand or either GY once Gemini
    // Summoned, Pyramid Turtle and Goblin Zombie pull bodies from the deck,
    // and Burial from a Different Dimension recycles a banished
    // Plaguespreader or Mezuki. The 2010 staple trap line (Mirror Force,
    // Torrential Tribute, Bottomless, Dimensional Prison, Sakuretsu Armor,
    // Solemn Judgment) protects the board between climbs.
    // Brain Control, Goyo Guardian and Brionac use the pre-errata passcodes
    // required by the Edison whitelist; they are defined locally.
    // Deck: AI_HalloweenLich.ydk (Edison-whitelist-validated).
    [Deck("HalloweenLich", "AI_HalloweenLich")]
    public class HalloweenLichExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Zombie engine.
            public const int ZombieMaster = 17259470;
            public const int GoblinZombie = 63665875;
            public const int Mezuki = 92826944;
            public const int PlaguespreaderZombie = 33420078;
            public const int PyramidTurtle = 77044671;
            public const int IlBlud = 70595331;
            public const int SpiritReaper = 23205979;
            public const int VampireLord = 53839837;
            public const int DarkResonator = 97021916;

            // Spells.
            public const int BookOfLife = 2204140;
            public const int FoolishBurial = 81439173;
            public const int BurialFromADifferentDimension = 48976825;
            public const int BrainControl = 511002995; // PRE-ERRATA passcode (Edison whitelist).

            // Traps.
            public const int MirrorForce = 44095762;
            public const int BottomlessTrapHole = 29401950;
            public const int DimensionalPrison = 70342110;
            public const int SakuretsuArmor = 56120475;

            // Extra Deck.
            public const int DoomkaiserDragon = 6021033;
            public const int RevivedKingHaDes = 5309481;
            public const int GoyoGuardian = 511002994; // PRE-ERRATA passcode (Edison whitelist).
            public const int Brionac = 511002993; // PRE-ERRATA passcode (Edison whitelist).
            public const int StardustDragon = 44508094;
            public const int RedDragonArchfiend = 70902743;
            public const int BlackRoseDragon = 73580471;
            public const int ThoughtRulerArchfiend = 70780151;
            public const int ColossalFighter = 23693634;
            public const int Catastor = 26593852;
            public const int MagicalAndroid = 43385557;
            public const int DarkEndDragon = 88643579;
            public const int MistWurm = 27315304;
        }

        // Duel.AnnounceType offers these system strings, in this order.
        private const int DeclareMonster = 70;
        private const int DeclareSpell = 71;
        private const int DeclareTrap = 72;

        // Goblin Zombie's forced search (Zombie with 1200 or less DEF).
        private static readonly int[] GoblinZombieSearchPriority =
        {
            CardId.ZombieMaster, CardId.PlaguespreaderZombie, CardId.Mezuki,
            CardId.SpiritReaper, CardId.IlBlud, CardId.GoblinZombie
        };

        // Revival priority for Mezuki / Book of Life / Call of the Haunted:
        // Synchro Zombies first (Doomkaiser re-triggers its steal), then the
        // biggest main-deck bodies.
        private static readonly int[] ReviveBodyPriority =
        {
            CardId.DoomkaiserDragon, CardId.RevivedKingHaDes, CardId.IlBlud,
            CardId.VampireLord, CardId.ZombieMaster, CardId.Mezuki,
            CardId.GoblinZombie, CardId.PyramidTurtle
        };

        public HalloweenLichExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Removal / disruption staples with DefaultExecutor's smart timing.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, _CardId.BookOfMoon, DefaultBookOfMoon);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.DimensionalPrison, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.SakuretsuArmor, SakuretsuArmorEffect);
            AddExecutor(ExecutorType.Activate, CardId.BrainControl, BrainControlEffect);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, CallOfTheHauntedEffect);

            // Synchro climbs, biggest payoff first. The engine only offers an
            // Extra Deck summon once a legal Tuner + non-Tuner set is on the
            // field; OnSelectSynchroMaterial spends the cheapest bodies.
            AddExecutor(ExecutorType.SpSummon, CardId.MistWurm, MistWurmSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.DarkEndDragon, DarkEndDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.RedDragonArchfiend, RedDragonArchfiendSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.StardustDragon);
            AddExecutor(ExecutorType.SpSummon, CardId.ThoughtRulerArchfiend);
            AddExecutor(ExecutorType.SpSummon, CardId.ColossalFighter);
            AddExecutor(ExecutorType.Activate, CardId.ThoughtRulerArchfiend, ThoughtRulerArchfiendEffect);
            AddExecutor(ExecutorType.Activate, CardId.ColossalFighter);
            AddExecutor(ExecutorType.SpSummon, CardId.DoomkaiserDragon, DoomkaiserStealSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Brionac, BrionacSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.GoyoGuardian, GoyoGuardianSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.RevivedKingHaDes, RevivedKingHaDesSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.DoomkaiserDragon);
            AddExecutor(ExecutorType.SpSummon, CardId.BlackRoseDragon, BlackRoseDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Catastor, CatastorSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.MagicalAndroid, MagicalAndroidSummon);

            // Synchro effects.
            AddExecutor(ExecutorType.Activate, CardId.MistWurm, MistWurmEffect);
            AddExecutor(ExecutorType.Activate, CardId.DoomkaiserDragon, DoomkaiserDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.BlackRoseDragon, BlackRoseDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.DarkEndDragon, DarkEndDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.Brionac, BrionacEffect);
            AddExecutor(ExecutorType.Activate, CardId.GoyoGuardian);
            AddExecutor(ExecutorType.Activate, CardId.StardustDragon, DefaultStardustDragonEffect);

            // Graveyard setup and recursion.
            AddExecutor(ExecutorType.Activate, CardId.FoolishBurial, FoolishBurialEffect);
            AddExecutor(ExecutorType.Activate, CardId.BurialFromADifferentDimension, BurialFromADifferentDimensionEffect);
            AddExecutor(ExecutorType.Activate, _CardId.AllureofDarkness, AllureOfDarknessEffect);
            AddExecutor(ExecutorType.Activate, CardId.PlaguespreaderZombie, PlaguespreaderZombieEffect);
            AddExecutor(ExecutorType.Activate, CardId.ZombieMaster, ZombieMasterEffect);
            AddExecutor(ExecutorType.Activate, CardId.IlBlud, IlBludEffect);
            AddExecutor(ExecutorType.Activate, CardId.Mezuki, MezukiEffect);
            AddExecutor(ExecutorType.Activate, CardId.BookOfLife, BookOfLifeEffect);
            AddExecutor(ExecutorType.Activate, CardId.PyramidTurtle, PyramidTurtleEffect);
            AddExecutor(ExecutorType.Activate, CardId.GoblinZombie, GoblinZombieEffect);

            // Summons: the engine pieces first, then the tribute beaters.
            AddExecutor(ExecutorType.Summon, CardId.ZombieMaster, ZombieMasterSummon);
            AddExecutor(ExecutorType.Summon, CardId.IlBlud, IlBludSummon);
            AddExecutor(ExecutorType.Summon, CardId.VampireLord, TributeBeaterSummon);
            AddExecutor(ExecutorType.Summon, CardId.DarkResonator, TunerSummon);
            AddExecutor(ExecutorType.Summon, CardId.PlaguespreaderZombie, TunerSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Mezuki);
            AddExecutor(ExecutorType.SummonOrSet, CardId.GoblinZombie);
            AddExecutor(ExecutorType.MonsterSet, CardId.PyramidTurtle, PyramidTurtleSet);
            AddExecutor(ExecutorType.Summon, CardId.PyramidTurtle);
            AddExecutor(ExecutorType.MonsterSet, CardId.SpiritReaper);
            AddExecutor(ExecutorType.MonsterSet, CardId.DarkResonator);
            AddExecutor(ExecutorType.SummonOrSet, CardId.ZombieMaster);

            // Fallback: whatever is left, set traps, reposition.
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        private static bool IsZombie(ClientCard card)
        {
            return card != null && card.IsMonster() && card.HasRace(CardRace.Zombie);
        }

        private bool IsMainPhaseOfOurs()
        {
            return Duel.Player == 0 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2);
        }

        private bool HasFreeMonsterZone()
        {
            return Bot.GetMonsterCount() < 5;
        }

        // Synchro reachability: a face-up non-Tuner on our field whose Level
        // plus the Tuner's Level matches a Synchro still in the Extra Deck.
        // Doomkaiser / Revived King Ha Des also need the non-Tuner to be a
        // Zombie and the Tuner to be Plaguespreader.
        private bool CanSynchroWithTuner(int tunerId, int tunerLevel)
        {
            foreach (ClientCard material in Bot.GetMonsters())
            {
                if (!material.IsFaceup() || material.IsTuner())
                    continue;
                int level = material.Level + tunerLevel;
                bool reachable = Bot.ExtraDeck.Any(card => card.Level == level
                    && (!card.IsCode(CardId.DoomkaiserDragon, CardId.RevivedKingHaDes)
                        || (tunerId == CardId.PlaguespreaderZombie && IsZombie(material))));
                if (reachable)
                    return true;
            }
            return false;
        }

        private bool PlaguespreaderClimbReady()
        {
            return CanSynchroWithTuner(CardId.PlaguespreaderZombie, 2);
        }

        // ---- Summons ----------------------------------------------------

        // Zombie Master is the engine: summon it whenever it has fuel (a
        // monster to discard) or there is no better play.
        private bool ZombieMasterSummon()
        {
            return Bot.Hand.Count(card => card.IsMonster() && card != Card) > 0
                || !Bot.GetMonsters().Any(card => card.IsFaceup());
        }

        // Il Blud: tribute summon it as a 2100 body, then Normal Summon it a
        // second time (Gemini) when its revive effect has a worthwhile target.
        private bool IlBludSummon()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return GetIlBludTarget() != null;
            return HasCheapTribute(Card.Attack);
        }

        private bool TributeBeaterSummon()
        {
            return HasCheapTribute(Card.Attack);
        }

        private bool HasCheapTribute(int incomingAttack)
        {
            return Bot.GetMonsters().Any(card => card.Level <= 4 && !card.IsExtraCard()
                && card.GetDefensePower() < incomingAttack && !card.IsCode(CardId.ZombieMaster))
                || Bot.GetMonsters().Any(card => card.IsFacedown() && card.IsCode(CardId.PyramidTurtle, CardId.SpiritReaper));
        }

        // Tuners only hit the field from the hand when a Synchro is ready.
        private bool TunerSummon()
        {
            if (Card.IsCode(CardId.PlaguespreaderZombie))
                return PlaguespreaderClimbReady();
            return CanSynchroWithTuner(CardId.DarkResonator, 3);
        }

        // Pyramid Turtle wants to be attacked: set it so a battle loss
        // pulls a Zombie from the deck, unless the opponent's field is empty.
        private bool PyramidTurtleSet()
        {
            return Enemy.GetMonsterCount() > 0;
        }

        // ---- Synchro choices ---------------------------------------------

        private bool MistWurmSummon()
        {
            return Enemy.GetFieldCount() >= 2;
        }

        private bool DarkEndDragonSummon()
        {
            return Enemy.GetMonsters().Any(card => !card.IsShouldNotBeTarget());
        }

        // Its End Phase sweep destroys our monsters that did not attack, so
        // only bring it out to crush defensive walls or onto a thin board.
        private bool RedDragonArchfiendSummon()
        {
            return Enemy.GetMonsters().Any(card => card.IsDefense()) || Bot.GetMonsterCount() <= 2;
        }

        private bool DoomkaiserStealSummon()
        {
            return Enemy.Graveyard.Any(card => IsZombie(card) && card.IsCanRevive());
        }

        private bool BrionacSummon()
        {
            return Util.GetProblematicEnemyCard() != null && GetSpareHandCard(null) != null;
        }

        private bool GoyoGuardianSummon()
        {
            return Enemy.GetMonsterCount() > 0 && Util.GetBestPower(Enemy) < 2800;
        }

        private bool RevivedKingHaDesSummon()
        {
            return Enemy.GetMonsters().Any(card => card.HasType(CardType.Effect));
        }

        private bool BlackRoseDragonSummon()
        {
            return IsBehindOnBoard() || Enemy.GetMonsterCount() > 0;
        }

        private bool CatastorSummon()
        {
            return Enemy.GetMonsters().Any(card => card.IsFaceup() && !card.HasAttribute(CardAttribute.Dark));
        }

        private bool MagicalAndroidSummon()
        {
            return Duel.Phase == DuelPhase.Main1 && Enemy.GetMonsterCount() == 0;
        }

        private bool IsBehindOnBoard()
        {
            int ours = Bot.GetFieldCount();
            int theirs = Enemy.GetFieldCount();
            return theirs >= ours + 2 || (Util.GetBestPower(Enemy) > 2500 && theirs >= ours);
        }

        // ---- Synchro effects ----------------------------------------------

        // Mist Wurm: bounce up to 3 of the opponent's best cards.
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

        // Doomkaiser: steal the strongest Zombie in the opponent's GY.
        private bool DoomkaiserDragonEffect()
        {
            ClientCard target = Enemy.Graveyard.Where(card => IsZombie(card) && card.IsCanRevive())
                .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (target == null)
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

        // Dark End Dragon: send the opponent's best monster to the GY while
        // it still keeps a useful body.
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

        // Brionac: discard one spare card to bounce the opponent's worst problem.
        private bool BrionacEffect()
        {
            ClientCard target = Util.GetProblematicEnemyCard(0, true) ?? Util.GetBestEnemyCard(false, true);
            ClientCard discard = GetSpareHandCard(null);
            if (target == null || discard == null)
                return false;
            AI.SelectCard(discard);
            AI.SelectNextCard(target);
            return true;
        }

        // ---- Graveyard engine ---------------------------------------------

        // Foolish Burial: Plaguespreader first (it revives itself), then
        // Mezuki (its effect works from the GY), then a Book of Life target.
        private bool FoolishBurialEffect()
        {
            List<int> priority = new List<int>();
            if (!Bot.HasInGraveyard(CardId.PlaguespreaderZombie) && !Bot.HasInHand(CardId.PlaguespreaderZombie))
                priority.Add(CardId.PlaguespreaderZombie);
            if (!Bot.HasInGraveyard(CardId.Mezuki))
                priority.Add(CardId.Mezuki);
            priority.Add(CardId.VampireLord);
            priority.Add(CardId.IlBlud);
            AI.SelectCard(priority);
            return true;
        }

        // Return our banished recursion pieces to the GY.
        private bool BurialFromADifferentDimensionEffect()
        {
            List<ClientCard> targets = Bot.Banished
                .Where(card => card.IsCode(CardId.PlaguespreaderZombie, CardId.Mezuki) || (IsZombie(card) && card.Attack >= 2000))
                .OrderByDescending(card => card.IsCode(CardId.PlaguespreaderZombie) ? 3 : card.IsCode(CardId.Mezuki) ? 2 : 1)
                .Take(3).ToList();
            if (!targets.Any(card => card.IsCode(CardId.PlaguespreaderZombie, CardId.Mezuki)))
                return false;
            AI.SelectCard(targets);
            return true;
        }

        private bool AllureOfDarknessEffect()
        {
            List<ClientCard> darkInHand = Bot.Hand
                .Where(card => card != Card && card.HasAttribute(CardAttribute.Dark) && card.IsMonster()).ToList();
            if (darkInHand.Count == 0)
                return false;
            ClientCard banish = darkInHand
                .Where(card => !card.IsCode(CardId.PlaguespreaderZombie, CardId.ZombieMaster))
                .OrderBy(card => Bot.Hand.Count(other => other.IsCode(card.Id)) > 1 ? 0 : 1)
                .ThenBy(card => card.Attack).FirstOrDefault()
                ?? darkInHand.First();
            AI.SelectCard(banish);
            return true;
        }

        // Plaguespreader from the GY: top-deck a spare card to bring it back
        // only when it completes a Synchro this turn.
        private bool PlaguespreaderZombieEffect()
        {
            if (Card.Location != CardLocation.Grave)
                return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2)
                return false;
            if (!HasFreeMonsterZone() || !PlaguespreaderClimbReady())
                return false;
            ClientCard spare = GetSpareHandCard(null);
            if (spare == null)
                return false;
            AI.SelectCard(spare);
            return true;
        }

        // Zombie Master: discard a monster, revive a Level 4 or lower Zombie
        // from either GY. Plaguespreader is the ideal target (Zombie Master
        // itself is the Level 4 Zombie for Doomkaiser / Ha Des).
        private bool ZombieMasterEffect()
        {
            if (Card.Location != CardLocation.MonsterZone || !HasFreeMonsterZone())
                return false;
            List<ClientCard> discards = Bot.Hand.Where(card => card.IsMonster()).ToList();
            if (discards.Count == 0)
                return false;

            ClientCard discard = discards.FirstOrDefault(card => card.IsCode(CardId.PlaguespreaderZombie))
                ?? discards.Where(card => !card.IsCode(CardId.ZombieMaster, CardId.DarkResonator))
                    .OrderByDescending(card => card.IsCode(CardId.IlBlud, CardId.VampireLord) ? 1 : 0)
                    .ThenBy(card => card.Attack).FirstOrDefault()
                ?? discards.First();

            List<ClientCard> pool = Bot.Graveyard.Concat(Enemy.Graveyard)
                .Where(card => IsZombie(card) && card.Level <= 4 && card.IsCanRevive()).ToList();
            if (discard.IsCode(CardId.PlaguespreaderZombie) || IsZombie(discard) && discard.Level <= 4)
                pool.Add(discard);

            ClientCard target = null;
            bool plagueClimb = Bot.ExtraDeck.Any(card => card.IsCode(CardId.DoomkaiserDragon, CardId.RevivedKingHaDes));
            if (plagueClimb)
                target = pool.FirstOrDefault(card => card.IsCode(CardId.PlaguespreaderZombie) && card.Controller == 0);
            if (target == null)
                target = pool.Where(card => !card.IsCode(CardId.Mezuki, CardId.PlaguespreaderZombie))
                    .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (target == null)
                return false;

            AI.SelectCard(discard);
            AI.SelectNextCard(target);
            return true;
        }

        // Il Blud's revive: Plaguespreader when it turns Il Blud into a
        // Level 8 Synchro, otherwise the strongest Zombie in hand or either GY.
        private bool IlBludEffect()
        {
            if (!HasFreeMonsterZone())
                return false;
            ClientCard target = GetIlBludTarget();
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        private ClientCard GetIlBludTarget()
        {
            List<ClientCard> pool = Bot.Hand.Concat(Bot.Graveyard).Concat(Enemy.Graveyard)
                .Where(card => IsZombie(card) && card.IsCanRevive() && !card.IsExtraCard() && !card.IsCode(CardId.IlBlud))
                .ToList();
            if (Bot.ExtraDeck.Any(card => card.Level == 8))
            {
                ClientCard plague = pool.FirstOrDefault(card => card.IsCode(CardId.PlaguespreaderZombie) && card.Controller == 0);
                if (plague != null)
                    return plague;
            }
            ClientCard best = pool.Where(card => !card.IsCode(CardId.Mezuki, CardId.PlaguespreaderZombie))
                .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (best == null || best.Attack < 1500)
                return null;
            return best;
        }

        // Mezuki from the GY: revive the best Zombie on our Main Phase 1, or
        // Plaguespreader when it completes a Synchro.
        private bool MezukiEffect()
        {
            if (Card.Location != CardLocation.Grave || !IsMainPhaseOfOurs() || !HasFreeMonsterZone())
                return false;
            ClientCard target = GetReviveTarget(Bot.Graveyard.Where(card => card != Card));
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // Book of Life: revive our best Zombie and banish the opponent's most
        // valuable GY monster. Used on our Main Phase or at the end of the
        // opponent's turn.
        private bool BookOfLifeEffect()
        {
            if (!HasFreeMonsterZone())
                return false;
            bool ourMain = IsMainPhaseOfOurs();
            bool theirEnd = Duel.Player == 1 && Duel.Phase == DuelPhase.End;
            if (!ourMain && !theirEnd)
                return false;
            ClientCard ours = GetReviveTarget(Bot.Graveyard);
            ClientCard theirs = Enemy.Graveyard.Where(card => card.IsMonster())
                .OrderByDescending(card => IsZombie(card) ? 0 : 1)
                .ThenByDescending(card => card.Attack).FirstOrDefault();
            if (ours == null || theirs == null)
                return false;
            AI.SelectCard(ours);
            AI.SelectNextCard(theirs);
            return true;
        }

        private ClientCard GetReviveTarget(IEnumerable<ClientCard> graveyard)
        {
            List<ClientCard> pool = graveyard.Where(card => IsZombie(card) && card.IsCanRevive()).ToList();
            if (PlaguespreaderClimbReady())
            {
                ClientCard plague = pool.FirstOrDefault(card => card.IsCode(CardId.PlaguespreaderZombie));
                if (plague != null)
                    return plague;
            }
            foreach (int id in ReviveBodyPriority)
            {
                ClientCard card = pool.FirstOrDefault(c => c.IsCode(id));
                if (card != null)
                    return card;
            }
            return null;
        }

        // Pyramid Turtle's float: on our turn, Plaguespreader to set up a
        // Synchro with a Level 4 Zombie already on the field; otherwise the
        // biggest body the DEF cap allows.
        private bool PyramidTurtleEffect()
        {
            List<int> priority = new List<int>();
            bool level4Zombie = Bot.GetMonsters().Any(card => card.IsFaceup() && IsZombie(card) && card.Level == 4 && !card.IsTuner());
            if (Duel.Player == 0 && level4Zombie)
                priority.Add(CardId.PlaguespreaderZombie);
            priority.AddRange(new[] { CardId.VampireLord, CardId.IlBlud, CardId.ZombieMaster, CardId.Mezuki, CardId.GoblinZombie, CardId.PyramidTurtle, CardId.SpiritReaper });
            AI.SelectCard(priority);
            return true;
        }

        private bool GoblinZombieEffect()
        {
            AI.SelectCard(GetGoblinZombiePriority());
            return true;
        }

        private int[] GetGoblinZombiePriority()
        {
            List<int> priority = new List<int>();
            if (!Bot.HasInHand(CardId.ZombieMaster))
                priority.Add(CardId.ZombieMaster);
            if (!Bot.HasInHandOrInGraveyard(CardId.PlaguespreaderZombie))
                priority.Add(CardId.PlaguespreaderZombie);
            priority.AddRange(GoblinZombieSearchPriority);
            return priority.ToArray();
        }

        // Spare card for costs (Plaguespreader's top-deck, Brionac's discard):
        // a trap or spell we cannot use right now, then a redundant monster.
        private ClientCard GetSpareHandCard(ClientCard exclude)
        {
            IEnumerable<ClientCard> hand = Bot.Hand.Where(card => card != exclude && card != Card);
            return hand.Where(card => card.IsTrap()).FirstOrDefault()
                ?? hand.Where(card => card.IsMonster() && !card.IsCode(CardId.ZombieMaster, CardId.PlaguespreaderZombie))
                    .OrderBy(card => card.Attack).FirstOrDefault()
                ?? hand.FirstOrDefault(card => !card.IsCode(CardId.BookOfLife));
        }

        // ---- Traps / spells -------------------------------------------------

        // Call of the Haunted: the default "revive our best when behind", plus
        // a proactive Plaguespreader revive on our turn to finish a Synchro.
        private bool CallOfTheHauntedEffect()
        {
            if (IsMainPhaseOfOurs() && PlaguespreaderClimbReady() && Bot.HasInGraveyard(CardId.PlaguespreaderZombie) && HasFreeMonsterZone())
            {
                AI.SelectCard(CardId.PlaguespreaderZombie);
                return true;
            }
            return DefaultCallOfTheHaunted();
        }

        // Sakuretsu Armor: kill an attacker that would win its battle or is
        // attacking directly.
        private bool SakuretsuArmorEffect()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            if (attacker == null || DefaultTrapWillBeNegated())
                return false;
            ClientCard defender = Bot.BattlingMonster;
            if (defender == null)
                return true;
            if (defender.IsCode(CardId.SpiritReaper, CardId.PyramidTurtle) && defender.IsDefense())
                return attacker.Attack >= 2000;
            return attacker.Attack >= defender.GetDefensePower();
        }

        // Pre-errata Brain Control: steal a body that can attack and be used
        // as Synchro material this turn.
        private bool BrainControlEffect()
        {
            if (Bot.LifePoints <= 800 || !HasFreeMonsterZone())
                return false;
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (target == null || target.Attack < 1500)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // Goblin Zombie's search is a forced trigger, so its choice is made
        // here based on the chain link resolving.
        // Thought Ruler Archfiend: pay 1000 LP to negate an opponent's effect
        // that targets it.
        private bool ThoughtRulerArchfiendEffect()
        {
            if (Bot.LifePoints <= 1000 || Duel.LastChainPlayer != 1)
                return false;
            return Util.IsChainTarget(Card);
        }

        // Vampire Lord: declare Spell or Trap, whichever the opponent has shown
        // more of; declaring Monster would only feed their graveyard.
        public override int OnSelectOption(IList<int> options)
        {
            if (options.Count == 3 && options[0] == DeclareMonster && options[1] == DeclareSpell && options[2] == DeclareTrap)
            {
                IEnumerable<ClientCard> seen = Enemy.Graveyard
                    .Concat(Enemy.Banished)
                    .Concat(Enemy.GetSpells().Where(card => card.IsFaceup()));
                int spells = seen.Count(card => card.IsSpell());
                int traps = seen.Count(card => card.IsTrap());
                return traps > spells ? 2 : 1;
            }

            return base.OnSelectOption(options);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            ClientCard source = Duel.GetCurrentSolvingChainCard();
            if (source != null && source.Controller == 0 && source.IsCode(CardId.GoblinZombie)
                && min == 1 && cards.All(card => card.Location == CardLocation.Deck))
            {
                foreach (int id in GetGoblinZombiePriority())
                {
                    ClientCard pick = cards.FirstOrDefault(card => card.IsCode(id));
                    if (pick != null)
                        return new[] { pick };
                }
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
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
