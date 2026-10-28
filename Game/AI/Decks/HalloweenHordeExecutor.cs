using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Entry-level Zombie beatdown bot for the Edison format (TCG April 2010
    // card pool), the first and easiest opponent of the Halloween horde.
    // The deck is mostly vanilla Zombies (Armored Zombie, Dragon Zombie, Clown
    // Zombie, Master Kyonshee) that simply swarm the field and attack. A few
    // effect Zombies add light value: Goblin Zombie searches another Zombie
    // when it leaves the field, Vampire Baby steals the monster it destroyed in
    // battle, Red-Eyes Zombie Dragon revives Zombies it destroys and can be
    // Tribute Summoned off a single Zombie, Ryu Kokki punishes Warriors and
    // Spellcasters, Return Zombie recurs itself when the hand is empty and
    // Poison Mummy is a burn wall. Book of Life and Call of the Haunted bring
    // the dead back; Fissure, Nobleman of Crossout, Sakuretsu Armor and Swords
    // of Revealing Light handle the opponent's threats.
    // Deck: AI_HalloweenHorde.ydk (Edison-whitelist-validated).
    [Deck("HalloweenHorde", "AI_HalloweenHorde")]
    public class HalloweenHordeExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Vanilla Zombies.
            public const int ArmoredZombie = 20277860;
            public const int DragonZombie = 66672569;
            public const int ClownZombie = 92667214;
            public const int MasterKyonshee = 24530661;
            public const int ThreeLeggedZombies = 33734439;
            public const int SkullServant = 32274490;

            // Effect Zombies.
            public const int ReturnZombie = 3072077;
            public const int PoisonMummy = 43716289;
            public const int VampireBaby = 56387350;
            public const int GoblinZombie = 63665875;
            public const int RedEyesZombieDragon = 5186893;
            public const int RyuKokki = 57281778;

            // Spells / traps not present in the shared _CardId class.
            public const int Fissure = 66788016;
            public const int NoblemanOfCrossout = 71044499;
            public const int BookOfLife = 2204140;
            public const int SakuretsuArmor = 56120475;
            public const int SwordsOfRevealingLight = 72302403;
        }

        // Goblin Zombie search priority: the best beater first, then bodies
        // that keep the horde growing.
        private static readonly int[] GoblinZombieSearchPriority =
        {
            CardId.MasterKyonshee,
            CardId.DragonZombie,
            CardId.ArmoredZombie,
            CardId.VampireBaby,
            CardId.GoblinZombie,
            CardId.ClownZombie,
            CardId.ThreeLeggedZombies,
            CardId.SkullServant
        };

        public HalloweenHordeExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // 1. Removal staples first, so the path is cleared before attacking.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);
            AddExecutor(ExecutorType.Activate, CardId.NoblemanOfCrossout, NoblemanOfCrossoutEffect);
            AddExecutor(ExecutorType.Activate, CardId.Fissure, FissureEffect);
            AddExecutor(ExecutorType.Activate, CardId.SwordsOfRevealingLight, SwordsOfRevealingLightEffect);

            // 2. Revival: Book of Life on our own turn, Call of the Haunted when
            // the opponent's board outclasses ours.
            AddExecutor(ExecutorType.Activate, CardId.BookOfLife, BookOfLifeEffect);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, DefaultCallOfTheHaunted);

            // 3. Battle trap: only on an attack that would actually hurt.
            AddExecutor(ExecutorType.Activate, CardId.SakuretsuArmor, SakuretsuArmorEffect);

            // 4. Monster effects.
            AddExecutor(ExecutorType.Activate, CardId.VampireBaby, VampireBabyEffect);
            AddExecutor(ExecutorType.Activate, CardId.RedEyesZombieDragon, RedEyesZombieDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.GoblinZombie, GoblinZombieEffect);
            AddExecutor(ExecutorType.Activate, CardId.ReturnZombie, ReturnZombieEffect);

            // 5. Tribute Summons: the big Zombies come down over our weakest body.
            AddExecutor(ExecutorType.Summon, CardId.RedEyesZombieDragon, TributeSummonOverWeakest);
            AddExecutor(ExecutorType.Summon, CardId.RyuKokki, TributeSummonOverWeakest);

            // 6. Poison Mummy is the wall of choice when the opponent outclasses
            // every beater we could summon this turn.
            AddExecutor(ExecutorType.MonsterSet, CardId.PoisonMummy, PoisonMummySet);

            // 7. Normal Summons, strongest beater first.
            AddExecutor(ExecutorType.SummonOrSet, CardId.MasterKyonshee);
            AddExecutor(ExecutorType.SummonOrSet, CardId.DragonZombie);
            AddExecutor(ExecutorType.SummonOrSet, CardId.ArmoredZombie);
            AddExecutor(ExecutorType.SummonOrSet, CardId.ClownZombie);
            AddExecutor(ExecutorType.SummonOrSet, CardId.GoblinZombie);
            AddExecutor(ExecutorType.SummonOrSet, CardId.ThreeLeggedZombies);
            AddExecutor(ExecutorType.Summon, CardId.VampireBaby, VampireBabySummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.ReturnZombie);

            // 8. Generic fallback play: summon what's left, set spells/traps, reposition.
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
            ClientCard target = GetFissureTarget();
            if (target == null)
                return false;

            if (Enemy.GetMonsterCount() == 1 && Bot.GetMonsterCount() > 0)
                return true;

            return target.GetDefensePower() >= Util.GetBestAttack(Bot);
        }

        private ClientCard GetFissureTarget()
        {
            return Enemy.GetMonsters()
                .Where(card => card.IsFaceup())
                .OrderBy(card => card.Attack)
                .FirstOrDefault();
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

        // Book of Life: on our own Main Phase, revive our best Zombie (only one
        // worth the card) and banish the opponent's best graveyard monster.
        private bool BookOfLifeEffect()
        {
            if (Duel.Player != 0 || (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2))
                return false;

            ClientCard revive = GetBestZombieInGraveyard(1500);
            ClientCard banish = Enemy.Graveyard.GetMatchingCards(card => card.IsMonster())
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            if (revive == null || banish == null)
                return false;

            AI.SelectCard(revive);
            AI.SelectNextCard(banish);
            return true;
        }

        private ClientCard GetBestZombieInGraveyard(int minAttack)
        {
            return Bot.Graveyard
                .GetMatchingCards(card => card.HasRace(CardRace.Zombie) && card.IsCanRevive() && card.Attack >= minAttack)
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
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

        // Vampire Baby and Red-Eyes Zombie Dragon both Special Summon the
        // monster they just destroyed in battle: a free body, always take it.
        private bool VampireBabyEffect()
        {
            return true;
        }

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

        // Return Zombie: pay 500 LP to get a body back while the hand is empty,
        // as long as the LP cost is not dangerous.
        private bool ReturnZombieEffect()
        {
            return Bot.LifePoints > 2000;
        }

        // Tribute Summon a Level 5+ Zombie only when the monster the engine will
        // tribute (always our lowest-ATK one) is weaker than the new monster.
        private bool TributeSummonOverWeakest()
        {
            ClientCard weakest = Bot.GetMonsters().OrderBy(card => card.Attack).FirstOrDefault();
            return weakest != null && weakest.Attack < Card.Attack;
        }

        // Set Poison Mummy (1800 DEF, flip burn) when the opponent has a monster
        // none of our Level 4 beaters in hand could beat.
        private bool PoisonMummySet()
        {
            if (Enemy.GetMonsterCount() == 0)
                return false;

            int bestBeater = Bot.Hand
                .Where(card => card.IsMonster() && card.Level <= 4 && !card.IsCode(CardId.PoisonMummy))
                .Select(card => card.Attack)
                .DefaultIfEmpty(0)
                .Max();
            return Util.GetBestPower(Enemy) >= bestBeater;
        }

        // Vampire Baby (700 ATK) only attacks profitably into a weaker monster.
        private bool VampireBabySummon()
        {
            return Enemy.GetMonsters().Any(card => card.IsFaceup() && card.GetDefensePower() < Card.Attack);
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

        // Ryu Kokki destroys a Warrior or Spellcaster it battled at the end of
        // the Damage Step, so it can attack a face-up defending one it cannot
        // beat in battle and still remove it (it survives, only taking damage).
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

            return base.OnSelectAttackTarget(attacker, defenders);
        }
    }
}
