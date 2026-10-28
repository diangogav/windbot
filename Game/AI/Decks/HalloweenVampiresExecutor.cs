using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Vampire Zombie midrange bot for the Edison format (TCG April 2010 card
    // pool), one step above the plain Zombie horde. Vampire Lady and Vampire
    // Lord mill a declared card type from the opponent's Deck every time they
    // connect, and Vampire Lord comes back on its own after being destroyed by
    // a card effect. Vampire Genesis is Special Summoned by banishing a Vampire
    // Lord and then trades a Zombie in hand for a smaller Zombie from the
    // graveyard each turn. Zombie Master revives Level 4 Zombies from either
    // graveyard (pitching a Zombie to revive that same card is a free Special
    // Summon), and reviving Bone Crusher that way pops a Spell/Trap. Pyramid
    // Turtle pulls Ryu Kokki or Red-Eyes Zombie Dragon from the Deck when it
    // dies in battle, Vampire's Curse returns stronger, and Goblin Zombie
    // searches. Book of Life, Call of the Haunted and a trap line (Mirror
    // Force, Magic Cylinder, Sakuretsu Armor, Bottomless Trap Hole, Torrential
    // Tribute) protect the board.
    // Deck: AI_HalloweenVampires.ydk (Edison-whitelist-validated).
    [Deck("HalloweenVampires", "AI_HalloweenVampires")]
    public class HalloweenVampiresExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Vampires.
            public const int VampireLord = 53839837;
            public const int VampireLady = 26495087;
            public const int VampireGenesis = 22056710;
            public const int VampiresCurse = 34294855;

            // Zombie engine.
            public const int ZombieMaster = 17259470;
            public const int BoneCrusher = 37675138;
            public const int PyramidTurtle = 77044671;
            public const int GoblinZombie = 63665875;
            public const int MasterKyonshee = 24530661;
            public const int RyuKokki = 57281778;
            public const int RedEyesZombieDragon = 5186893;

            // Spells / traps not present in the shared _CardId class.
            public const int BookOfLife = 2204140;
            public const int Fissure = 66788016;
            public const int NoblemanOfCrossout = 71044499;
            public const int SwordsOfRevealingLight = 72302403;
            public const int SakuretsuArmor = 56120475;
            public const int BottomlessTrapHole = 29401950;
            public const int MirrorForce = 44095762;
            public const int MagicCylinder = 62279055;
        }

        // Duel.AnnounceType offers these system strings, in this order.
        private const int DeclareMonster = 70;
        private const int DeclareSpell = 71;
        private const int DeclareTrap = 72;

        // Pyramid Turtle: biggest Zombie with 2000 or less DEF from the Deck.
        private static readonly int[] PyramidTurtleSummonPriority =
        {
            CardId.RyuKokki,
            CardId.RedEyesZombieDragon,
            CardId.VampireLord,
            CardId.VampiresCurse,
            CardId.MasterKyonshee,
            CardId.VampireLady,
            CardId.BoneCrusher,
            CardId.PyramidTurtle
        };

        // Goblin Zombie: Zombies with 1200 or less DEF.
        private static readonly int[] GoblinZombieSearchPriority =
        {
            CardId.MasterKyonshee,
            CardId.VampiresCurse,
            CardId.BoneCrusher,
            CardId.GoblinZombie
        };

        public HalloweenVampiresExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // 1. Removal staples first, so the path is cleared before attacking.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);
            AddExecutor(ExecutorType.Activate, CardId.NoblemanOfCrossout, NoblemanOfCrossoutEffect);
            AddExecutor(ExecutorType.Activate, CardId.Fissure, FissureEffect);
            AddExecutor(ExecutorType.Activate, CardId.SwordsOfRevealingLight, SwordsOfRevealingLightEffect);

            // 2. Vampire Genesis: banish a face-up Vampire Lord for a 3000 ATK body,
            // then use its once-per-turn graveyard swap.
            AddExecutor(ExecutorType.SpSummon, CardId.VampireGenesis, VampireGenesisSummon);
            AddExecutor(ExecutorType.Activate, CardId.VampireGenesis, VampireGenesisEffect);

            // 3. Zombie Master revival (Bone Crusher first when it can pop a card).
            AddExecutor(ExecutorType.Activate, CardId.ZombieMaster, ZombieMasterEffect);
            AddExecutor(ExecutorType.Activate, CardId.BoneCrusher, BoneCrusherEffect);

            // 4. Revival spells / traps.
            AddExecutor(ExecutorType.Activate, CardId.BookOfLife, BookOfLifeEffect);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, DefaultCallOfTheHaunted);

            // 5. Floating and recursion triggers.
            AddExecutor(ExecutorType.Activate, CardId.PyramidTurtle, PyramidTurtleEffect);
            AddExecutor(ExecutorType.Activate, CardId.VampiresCurse, VampiresCurseEffect);
            AddExecutor(ExecutorType.Activate, CardId.RedEyesZombieDragon, RedEyesZombieDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.GoblinZombie, GoblinZombieEffect);

            // 6. Trap line.
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce, MirrorForceEffect);
            AddExecutor(ExecutorType.Activate, CardId.MagicCylinder, MagicCylinderEffect);
            AddExecutor(ExecutorType.Activate, CardId.SakuretsuArmor, SakuretsuArmorEffect);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);

            // 7. Tribute Summons. Vampire Lord jumps the queue while Vampire
            // Genesis waits in hand for it.
            AddExecutor(ExecutorType.Summon, CardId.VampireLord, VampireLordForGenesisSummon);
            AddExecutor(ExecutorType.Summon, CardId.RedEyesZombieDragon, TributeSummonOverWeakest);
            AddExecutor(ExecutorType.Summon, CardId.RyuKokki, TributeSummonOverWeakest);
            AddExecutor(ExecutorType.Summon, CardId.VampireLord, TributeSummonOverWeakest);
            AddExecutor(ExecutorType.Summon, CardId.VampiresCurse, TributeSummonOverWeakest);

            // 8. Normal Summons. Zombie Master first when it has a cost to pay,
            // Vampire Lady first into an empty board (direct hit plus mill).
            AddExecutor(ExecutorType.Summon, CardId.ZombieMaster, ZombieMasterSummon);
            AddExecutor(ExecutorType.Summon, CardId.VampireLady, VampireLadySummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.PyramidTurtle, PyramidTurtleSet);
            AddExecutor(ExecutorType.SummonOrSet, CardId.MasterKyonshee);
            AddExecutor(ExecutorType.SummonOrSet, CardId.VampireLady);
            AddExecutor(ExecutorType.SummonOrSet, CardId.BoneCrusher, BoneCrusherSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.GoblinZombie);
            AddExecutor(ExecutorType.SummonOrSet, CardId.PyramidTurtle);

            // 9. Generic fallback play: summon what's left, set spells/traps, reposition.
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        // Nobleman of Crossout: banish a face-down monster before it can flip.
        private bool NoblemanOfCrossoutEffect()
        {
            ClientCard target = Enemy.GetMonsters().FirstOrDefault(card => card.IsFacedown());
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Fissure always hits the opponent's lowest-ATK face-up monster, so it
        // is only worth a card when that monster actually blocks us: either it
        // is the last monster between us and a direct attack, or none of our
        // monsters can beat it in battle.
        private bool FissureEffect()
        {
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup())
                .OrderBy(card => card.Attack)
                .FirstOrDefault();
            if (target == null)
                return false;

            if (Enemy.GetMonsterCount() == 1 && Bot.GetMonsterCount() > 0)
                return true;

            return target.GetDefensePower() >= Util.GetBestAttack(Bot);
        }

        // Swords of Revealing Light: buy three turns when the opponent has a
        // monster our board cannot handle, or flip a wall of set monsters.
        private bool SwordsOfRevealingLightEffect()
        {
            if (Enemy.GetMonsterCount() == 0 || !UniqueFaceupSpell())
                return false;

            if (Bot.GetMonsterCount() == 0)
                return true;

            return Util.IsOneEnemyBetter(true) || Enemy.GetMonsters().Count(card => card.IsFacedown()) >= 2;
        }

        // Vampire Genesis: banish a face-up Vampire Lord to bring out 3000 ATK.
        private bool VampireGenesisSummon()
        {
            ClientCard lord = Bot.GetMonsters().FirstOrDefault(card => card.IsCode(CardId.VampireLord) && card.IsFaceup());
            if (lord == null)
                return false;

            AI.SelectCard(lord);
            return true;
        }

        // Vampire Genesis: discard a Zombie from hand to revive a lower-Level
        // Zombie from our graveyard. Revive the strongest legal target and pay
        // with the cheapest Zombie whose Level is high enough.
        private bool VampireGenesisEffect()
        {
            List<ClientCard> discards = Bot.Hand
                .Where(card => card.IsMonster() && card.HasRace(CardRace.Zombie))
                .ToList();
            if (discards.Count == 0)
                return false;

            List<ClientCard> targets = Bot.Graveyard
                .GetMatchingCards(card => card.HasRace(CardRace.Zombie) && card.IsCanRevive()
                    && !card.IsCode(CardId.VampireGenesis) && card.Attack >= 1500)
                .OrderByDescending(card => card.Attack)
                .ToList();

            foreach (ClientCard target in targets)
            {
                ClientCard discard = discards
                    .Where(card => card.Level > target.Level)
                    .OrderBy(card => card.Attack)
                    .FirstOrDefault();
                if (discard == null)
                    continue;

                AI.SelectCard(discard);
                AI.SelectNextCard(target);
                return true;
            }

            return false;
        }

        // Zombie Master: send a monster from hand to the graveyard, then revive
        // a Level 4 or lower Zombie from either graveyard. The cost resolves
        // before the target is chosen, so a Zombie pitched from hand can be the
        // card it revives.
        //   1. Opponent has Spells/Traps: revive Bone Crusher (from our graveyard
        //      or pitched from hand) to destroy one of them.
        //   2. Otherwise revive the strongest eligible Zombie (1500+ ATK) from
        //      either graveyard or our hand.
        private bool ZombieMasterEffect()
        {
            List<ClientCard> handMonsters = Bot.Hand.Where(card => card.IsMonster()).ToList();
            if (handMonsters.Count == 0)
                return false;

            if (Enemy.GetSpellCount() > 0)
            {
                ClientCard handCrusher = handMonsters.FirstOrDefault(card => card.IsCode(CardId.BoneCrusher));
                if (Bot.HasInGraveyard(CardId.BoneCrusher))
                {
                    AI.SelectCard(GetZombieMasterCost(handMonsters));
                    AI.SelectNextCard(Bot.Graveyard.GetFirstMatchingCard(card => card.IsCode(CardId.BoneCrusher)));
                    return true;
                }
                if (handCrusher != null)
                {
                    AI.SelectCard(handCrusher);
                    AI.SelectNextCard(CardId.BoneCrusher);
                    return true;
                }
            }

            ClientCard graveTarget = Bot.Graveyard.Concat(Enemy.Graveyard)
                .Where(card => IsZombieMasterTarget(card))
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            ClientCard handTarget = handMonsters
                .Where(card => IsZombieMasterTarget(card))
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();

            if (handTarget != null && (graveTarget == null || handTarget.Attack > graveTarget.Attack))
            {
                AI.SelectCard(handTarget);
                AI.SelectNextCard(handTarget.Id);
                return true;
            }

            if (graveTarget == null)
                return false;

            AI.SelectCard(GetZombieMasterCost(handMonsters));
            AI.SelectNextCard(graveTarget);
            return true;
        }

        private static bool IsZombieMasterTarget(ClientCard card)
        {
            return card.IsMonster() && card.HasRace(CardRace.Zombie) && card.Level <= 4
                && card.Attack >= 1500 && card.IsCanRevive();
        }

        // Cheapest monster to pitch: never Vampire Genesis while another option
        // exists, otherwise the lowest ATK.
        private static ClientCard GetZombieMasterCost(List<ClientCard> handMonsters)
        {
            return handMonsters
                .OrderBy(card => card.IsCode(CardId.VampireGenesis) ? 1 : 0)
                .ThenBy(card => card.Attack)
                .FirstOrDefault();
        }

        // Bone Crusher (revived from our graveyard by a Zombie effect): destroy
        // the most dangerous Spell/Trap, set cards before plain face-up spells.
        private bool BoneCrusherEffect()
        {
            ClientCard target = Util.GetProblematicEnemySpell()
                ?? Enemy.GetSpells().FirstOrDefault(card => card.IsFaceup()
                    && (card.HasType(CardType.Continuous) || card.HasType(CardType.Field) || card.HasType(CardType.Equip)))
                ?? Enemy.GetSpells().FirstOrDefault(card => card.IsFacedown())
                ?? Enemy.GetSpells().FirstOrDefault();
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Book of Life: on our own Main Phase, revive our best Zombie (only one
        // worth the card) and banish the opponent's best graveyard monster.
        private bool BookOfLifeEffect()
        {
            if (Duel.Player != 0 || (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2))
                return false;

            ClientCard revive = Bot.Graveyard
                .GetMatchingCards(card => card.HasRace(CardRace.Zombie) && card.IsCanRevive()
                    && !card.IsCode(CardId.VampireGenesis) && card.Attack >= 1500)
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            ClientCard banish = Enemy.Graveyard.GetMatchingCards(card => card.IsMonster())
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            if (revive == null || banish == null)
                return false;

            AI.SelectCard(revive);
            AI.SelectNextCard(banish);
            return true;
        }

        // Pyramid Turtle: Special Summon the biggest Zombie left in the Deck.
        private bool PyramidTurtleEffect()
        {
            AI.SelectCard(PyramidTurtleSummonPriority);
            return true;
        }

        // Vampire's Curse: pay 500 LP to return with +500 ATK, unless the LP
        // cost puts us in burn range.
        private bool VampiresCurseEffect()
        {
            return Bot.LifePoints > 1500;
        }

        // Red-Eyes Zombie Dragon: the Zombie it destroyed joins our side.
        private bool RedEyesZombieDragonEffect()
        {
            return true;
        }

        // Goblin Zombie's two triggers are mandatory (mill on battle damage,
        // search when it leaves the field). Only the search needs a choice:
        // queue the preferred target for it, never for the mill, so nothing
        // leaks into an unrelated selection.
        private bool GoblinZombieEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.GoblinZombie, 1))
                AI.SelectCard(GoblinZombieSearchPriority);
            return true;
        }

        // Mirror Force: wait for an attack worth answering, ideally into a board
        // with several attack-position monsters.
        private bool MirrorForceEffect()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            if (attacker == null)
                return false;

            int attackers = Enemy.GetMonsters().Count(card => card.IsAttack());
            return attackers >= 2 || attacker.Attack >= 1800 || attacker.Attack >= Bot.LifePoints
                || (Bot.BattlingMonster != null && attacker.Attack >= Bot.BattlingMonster.GetDefensePower());
        }

        // Magic Cylinder: bounce a big hit, or any hit that is lethal either way.
        private bool MagicCylinderEffect()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            if (attacker == null)
                return false;

            return attacker.Attack >= 1800 || attacker.Attack >= Bot.LifePoints || attacker.Attack >= Enemy.LifePoints;
        }

        // Sakuretsu Armor destroys the attacker; keep it for attacks that would
        // destroy our monster or deal real direct damage.
        private bool SakuretsuArmorEffect()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            if (attacker == null)
                return false;

            ClientCard defender = Bot.BattlingMonster;
            if (defender == null)
                return attacker.Attack >= 1000;

            return attacker.Attack >= defender.GetDefensePower();
        }

        // Tribute Summon a Level 5+ Zombie only when the monster the engine will
        // tribute (always our lowest-ATK one) is weaker than the new monster.
        private bool TributeSummonOverWeakest()
        {
            ClientCard weakest = Bot.GetMonsters().OrderBy(card => card.Attack).FirstOrDefault();
            return weakest != null && weakest.Attack < Card.Attack && !weakest.IsCode(CardId.VampireGenesis);
        }

        private bool VampireLordForGenesisSummon()
        {
            return Bot.HasInHand(CardId.VampireGenesis)
                && !Bot.HasInMonstersZone(CardId.VampireLord, true)
                && TributeSummonOverWeakest();
        }

        // Zombie Master only earns its Normal Summon when there is a monster in
        // hand to pay its cost with.
        private bool ZombieMasterSummon()
        {
            return Bot.Hand.Any(card => card != Card && card.IsMonster());
        }

        // Vampire Lady face-up into an empty board: direct hit plus a mill.
        private bool VampireLadySummon()
        {
            return Enemy.GetMonsterCount() == 0;
        }

        // Keep Bone Crusher in hand as Zombie Master fodder while a Master is
        // on the field or in hand and the opponent has Spells/Traps to pop.
        private bool BoneCrusherSummon()
        {
            bool masterAvailable = Bot.HasInMonstersZone(CardId.ZombieMaster, true) || Bot.HasInHand(CardId.ZombieMaster);
            return !(masterAvailable && Enemy.GetSpellCount() > 0);
        }

        // Set Pyramid Turtle as bait when the opponent outclasses every beater we
        // could summon: if it is destroyed in battle it pulls a 2400 ATK Zombie.
        private bool PyramidTurtleSet()
        {
            if (Enemy.GetMonsterCount() == 0)
                return false;

            int bestBeater = Bot.Hand
                .Where(card => card.IsMonster() && card.Level <= 4 && !card.IsCode(CardId.PyramidTurtle))
                .Select(card => card.Attack)
                .DefaultIfEmpty(0)
                .Max();
            return Util.GetBestPower(Enemy) >= bestBeater;
        }

        // Vampire Lady / Vampire Lord: declare the card type the opponent seems
        // to run most of. Spells and traps are judged from every card of theirs
        // we have seen (graveyard, banished, face-up on the field); a monster is
        // never declared, since milling monsters usually helps the opponent.
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

        // Goblin Zombie's mandatory search arrives as a plain deck selection.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (!AI.HaveSelectedCards() && hint == HintMsg.AddToHand && cards.All(card => card.Location == CardLocation.Deck))
            {
                IList<ClientCard> preferred = Util.SelectPreferredCards(GoblinZombieSearchPriority, cards, min, max);
                return Util.CheckSelectCount(preferred, cards, min, max);
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        // Beatdown attack policy: trade into an equal-ATK monster instead of only
        // trading on the last attacker. Ryu Kokki destroys a Warrior or
        // Spellcaster it battled at the end of the Damage Step, so it may attack
        // a face-up defending one it cannot beat and still remove it.
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (attacker.IsCode(CardId.RyuKokki) && !attacker.IsDisabled())
            {
                foreach (ClientCard defender in defenders)
                {
                    if (!defender.IsFaceup() || !defender.IsDefense())
                        continue;
                    if (!defender.HasRace(CardRace.Warrior) && !defender.HasRace(CardRace.SpellCaster))
                        continue;
                    if (Bot.LifePoints > defender.Defense - attacker.Attack + 1000)
                        return AI.Attack(attacker, defender);
                }
            }

            foreach (ClientCard defender in defenders)
            {
                attacker.RealPower = attacker.Attack;
                defender.RealPower = defender.GetDefensePower();
                if (!OnPreBattleBetween(attacker, defender))
                    continue;

                bool canKill = attacker.RealPower > defender.RealPower;
                bool evenTrade = attacker.RealPower >= defender.RealPower && defender.IsAttack();
                if (canKill || evenTrade)
                    return AI.Attack(attacker, defender);
            }

            if (attacker.CanDirectAttack)
                return AI.Attack(attacker, null);

            return null;
        }
    }
}
