using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Skull-and-bones beatdown bot for the Edison format. Summoned Skull and
    // Skull Archfiend of Lightning are the 2500 ATK beaters, Tribute Summoned
    // by releasing Skull Servants so the graveyard keeps feeding King of the
    // Skull Servants (+1000 ATK per Skull Servant / King in the graveyard;
    // The Lady in Wight counts as a Skull Servant there). Pyramid Turtle
    // floats into the King or another Turtle, Mezuki raises a Zombie from the
    // graveyard (Bone Crusher revived this way pops a Spell/Trap), Skull
    // Conductor drops Zombies from the hand, and Skull Lair turns the
    // graveyard into targeted removal. A full trap line (Bottomless,
    // Dimensional Prison, Sakuretsu, Mirror Force, Solemn) protects the push.
    // Deck: AI_HalloweenSkeletons.ydk (Edison-whitelist-validated).
    [Deck("HalloweenSkeletons", "AI_HalloweenSkeletons")]
    public class HalloweenSkeletonsExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Skull beaters.
            public const int SummonedSkull = 70781052;
            public const int SkullArchfiendOfLightning = 61370518;

            // Skull Servant engine.
            public const int KingOfTheSkullServants = 36021814;
            public const int SkullServant = 32274490;
            public const int TheLadyInWight = 40991587;

            // Zombie support.
            public const int PyramidTurtle = 77044671;
            public const int Mezuki = 92826944;
            public const int SkullConductor = 62782218;
            public const int SoulAbsorbingBoneTower = 63012333;
            public const int BoneCrusher = 37675138;

            // Spells not present in the shared _CardId class.
            public const int FoolishBurial = 81439173;
            public const int SkullLair = 6733059;

            // Traps not present in the shared _CardId class.
            public const int MirrorForce = 44095762;
            public const int BottomlessTrapHole = 29401950;
            public const int DimensionalPrison = 70342110;
            public const int SakuretsuArmor = 56120475;
            public const int MagicCylinder = 62279055;
        }

        // Skull Lair banish order: spend the least useful graveyard monsters
        // first and keep the King's fuel (and Mezuki's revival) for last.
        private static readonly int[] SkullLairBanishOrder =
        {
            CardId.SoulAbsorbingBoneTower,
            CardId.SkullConductor,
            CardId.BoneCrusher,
            CardId.PyramidTurtle,
            CardId.SkullArchfiendOfLightning,
            CardId.SummonedSkull,
            CardId.Mezuki,
            CardId.TheLadyInWight,
            CardId.SkullServant,
            CardId.KingOfTheSkullServants
        };

        public HalloweenSkeletonsExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // 1. Backrow removal first, so the beaters swing into a clean field.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);

            // 2. Negation and reactive traps.
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.MagicCylinder, MagicCylinderEffect);
            AddExecutor(ExecutorType.Activate, CardId.SakuretsuArmor, SakuretsuArmorEffect);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.DimensionalPrison, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, CallOfTheHauntedEffect);

            // 3. Removal spells.
            AddExecutor(ExecutorType.Activate, CardId.SkullLair, SkullLairEffect);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, _CardId.BookOfMoon, DefaultBookOfMoon);

            // 4. Graveyard setup and graveyard/hand monster effects.
            AddExecutor(ExecutorType.Activate, CardId.FoolishBurial, FoolishBurialEffect);
            AddExecutor(ExecutorType.Activate, CardId.Mezuki, MezukiEffect);
            AddExecutor(ExecutorType.Activate, CardId.SkullConductor, SkullConductorEffect);
            AddExecutor(ExecutorType.Activate, CardId.KingOfTheSkullServants, KingOfTheSkullServantsEffect);
            AddExecutor(ExecutorType.Activate, CardId.PyramidTurtle, PyramidTurtleEffect);
            AddExecutor(ExecutorType.Activate, CardId.BoneCrusher, BoneCrusherEffect);

            // 5. Normal Summon priority: a big King, the 2500 skulls, then bodies.
            AddExecutor(ExecutorType.Summon, CardId.KingOfTheSkullServants, KingOfTheSkullServantsSummon);
            AddExecutor(ExecutorType.Summon, CardId.SummonedSkull, SkullTributeSummon);
            AddExecutor(ExecutorType.Summon, CardId.SkullArchfiendOfLightning, SkullArchfiendSummon);
            AddExecutor(ExecutorType.Summon, CardId.SkullConductor, SkullConductorSummon);
            AddExecutor(ExecutorType.Summon, CardId.TheLadyInWight, TheLadyInWightSummon);
            AddExecutor(ExecutorType.Summon, CardId.SoulAbsorbingBoneTower, SoulAbsorbingBoneTowerSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Mezuki, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.BoneCrusher, DefaultMonsterSummon);

            // 6. Walls and tribute fodder are Set.
            AddExecutor(ExecutorType.MonsterSet, CardId.PyramidTurtle);
            AddExecutor(ExecutorType.MonsterSet, CardId.TheLadyInWight);
            AddExecutor(ExecutorType.MonsterSet, CardId.SoulAbsorbingBoneTower);
            AddExecutor(ExecutorType.MonsterSet, CardId.SkullServant, SkullServantSet);

            // 7. Backrow and reposition.
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, CardId.KingOfTheSkullServants, KingOfTheSkullServantsRepos);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        // Skull Servants, Kings and The Lady in Wight (named "Skull Servant"
        // in the graveyard) all count toward King of the Skull Servants' ATK.
        private int SkullServantGraveCount()
        {
            return Bot.Graveyard.Count(IsSkullServantName);
        }

        private static bool IsSkullServantName(ClientCard card)
        {
            return card.IsCode(CardId.SkullServant, CardId.KingOfTheSkullServants, CardId.TheLadyInWight);
        }

        private bool MagicCylinderEffect()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            return attacker != null && attacker.Attack >= 1500;
        }

        // Sakuretsu Armor: destroy an attacker that would win its battle, or
        // any sizeable direct attacker.
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

        // Call of the Haunted: revive the strongest graveyard monster when the
        // opponent's board is ahead. A King coming back counts its fuel
        // without itself.
        private bool CallOfTheHauntedEffect()
        {
            if (!Util.IsAllEnemyBetter(true))
                return false;

            ClientCard target = Bot.Graveyard
                .Where(card => card.IsMonster() && card.IsCanRevive())
                .OrderByDescending(RevivedAttack)
                .FirstOrDefault();
            if (target == null || RevivedAttack(target) < 1500)
                return false;

            AI.SelectCard(target);
            return true;
        }

        private int RevivedAttack(ClientCard card)
        {
            if (card.IsCode(CardId.KingOfTheSkullServants))
                return (SkullServantGraveCount() - 1) * 1000;
            return card.Attack;
        }

        // Skull Lair: first activation only puts it face-up, and only once a
        // target exists; the Quick Effect then banishes exactly as many
        // graveyard monsters as the target's Level and destroys it.
        private bool SkullLairEffect()
        {
            ClientCard target = GetSkullLairTarget();
            if (target == null)
                return false;

            if (ActivateDescription != Util.GetStringId(CardId.SkullLair, 1))
                return true;

            AI.SelectNumber(target.Level);
            AI.SelectCard(GetSkullLairBanishList(target.Level));
            AI.SelectNextCard(target);
            return true;
        }

        // The opponent's strongest face-up monster whose Level we can pay for
        // without banishing the King's fuel while the King is on the field.
        private ClientCard GetSkullLairTarget()
        {
            int available = Bot.GetGraveyardMonsters().Count;
            if (Bot.HasInMonstersZone(CardId.KingOfTheSkullServants, true))
                available -= SkullServantGraveCount();

            return Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && card.Level > 0 && card.Level <= available)
                .Where(card => card.Attack >= 1500 || card.Attack >= Util.GetBestAttack(Bot))
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
        }

        private List<ClientCard> GetSkullLairBanishList(int count)
        {
            return Bot.GetGraveyardMonsters()
                .OrderBy(card => BanishRank(card))
                .Take(count)
                .ToList();
        }

        private static int BanishRank(ClientCard card)
        {
            for (int i = 0; i < SkullLairBanishOrder.Length; ++i)
            {
                if (card.IsCode(SkullLairBanishOrder[i]))
                    return i;
            }
            return -1;
        }

        // Foolish Burial: dump a Skull Servant to grow the King; once the
        // King is not around, Mezuki in the graveyard is the better pick.
        private bool FoolishBurialEffect()
        {
            bool kingInPlay = Bot.HasInHandOrHasInMonstersZone(CardId.KingOfTheSkullServants);
            bool mezukiUseful = Bot.GetRemainingCount(CardId.Mezuki, 1) > 0
                && Bot.Graveyard.Any(card => card.HasRace(CardRace.Zombie) && card.IsMonster());

            if (!kingInPlay && mezukiUseful)
            {
                AI.SelectCard(CardId.Mezuki, CardId.SkullServant);
                return true;
            }

            if (Bot.GetRemainingCount(CardId.SkullServant, 3) > 0)
            {
                AI.SelectCard(CardId.SkullServant, CardId.Mezuki);
                return true;
            }

            if (!mezukiUseful)
                return false;

            AI.SelectCard(CardId.Mezuki);
            return true;
        }

        // Mezuki (graveyard): banish it to raise the best Zombie. A King with
        // 2+ fuel left comes back as a 2000+ beater; Bone Crusher revived by a
        // Zombie pops a Spell/Trap; otherwise a Pyramid Turtle keeps floating.
        private bool MezukiEffect()
        {
            if (Card.Location != CardLocation.Grave)
                return false;

            ClientCard target = GetMezukiTarget();
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        private ClientCard GetMezukiTarget()
        {
            List<ClientCard> zombies = Bot.Graveyard
                .Where(card => card.IsMonster() && card.HasRace(CardRace.Zombie) && card.IsCanRevive() && !card.IsCode(CardId.Mezuki))
                .ToList();

            ClientCard king = zombies.FirstOrDefault(card => card.IsCode(CardId.KingOfTheSkullServants));
            if (king != null && SkullServantGraveCount() - 1 >= 2)
                return king;

            ClientCard crusher = zombies.FirstOrDefault(card => card.IsCode(CardId.BoneCrusher));
            if (crusher != null && Enemy.GetSpellCount() > 0)
                return crusher;

            return zombies.FirstOrDefault(card => card.IsCode(CardId.PyramidTurtle));
        }

        // Skull Conductor (hand): send it to the graveyard to Special Summon
        // Zombies from the hand totaling exactly 2000 ATK.
        private bool SkullConductorEffect()
        {
            if (Card.Location != CardLocation.Hand)
                return false;

            List<ClientCard> partners = GetSkullConductorPartners();
            if (partners == null)
                return false;

            AI.SelectCard(partners);
            return true;
        }

        private List<ClientCard> GetSkullConductorPartners()
        {
            int freeZones = 5 - Bot.GetMonsterCount();
            List<ClientCard> hand = Bot.Hand.Where(card => card != Card && card.IsMonster()).ToList();

            ClientCard otherConductor = hand.FirstOrDefault(card => card.IsCode(CardId.SkullConductor));
            if (otherConductor != null && freeZones >= 1)
                return new List<ClientCard> { otherConductor };

            if (freeZones < 2)
                return null;

            int[][] pairs =
            {
                new[] { CardId.Mezuki, CardId.SkullServant },
                new[] { CardId.BoneCrusher, CardId.SoulAbsorbingBoneTower }
            };
            foreach (int[] pair in pairs)
            {
                ClientCard first = hand.FirstOrDefault(card => card.IsCode(pair[0]));
                ClientCard second = hand.FirstOrDefault(card => card.IsCode(pair[1]));
                if (first != null && second != null)
                    return new List<ClientCard> { first, second };
            }
            return null;
        }

        // King of the Skull Servants (graveyard): when destroyed by battle,
        // banish a Skull Servant to come back, as long as it still returns
        // with at least 1000 ATK.
        private bool KingOfTheSkullServantsEffect()
        {
            if (SkullServantGraveCount() < 3)
                return false;

            AI.SelectCard(CardId.SkullServant, CardId.TheLadyInWight, CardId.KingOfTheSkullServants);
            return true;
        }

        // Pyramid Turtle: float into a big King when the fuel is there,
        // otherwise into another Turtle to keep the chain going.
        private bool PyramidTurtleEffect()
        {
            if (SkullServantGraveCount() >= 2)
                AI.SelectCard(CardId.KingOfTheSkullServants, CardId.PyramidTurtle, CardId.Mezuki, CardId.SkullServant);
            else
                AI.SelectCard(CardId.PyramidTurtle, CardId.Mezuki, CardId.KingOfTheSkullServants, CardId.SkullServant);
            return true;
        }

        private bool BoneCrusherEffect()
        {
            ClientCard target = Util.GetBestEnemySpell();
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // The King is a Level 1 with no Tribute: summon it once 2+ Skull
        // Servants in the graveyard make it a 2000+ ATK attacker.
        private bool KingOfTheSkullServantsSummon()
        {
            return SkullServantGraveCount() >= 2;
        }

        private bool KingOfTheSkullServantsRepos()
        {
            if (Card.IsFacedown())
                return SkullServantGraveCount() >= 2;
            bool weak = Card.Attack < 1000 || Util.IsAllEnemyBetterThanValue(Card.Attack, true);
            return Card.IsAttack() == weak;
        }

        // Summoned Skull: release the cheapest body (ideally a Skull Servant,
        // which then powers the King from the graveyard).
        private bool SkullTributeSummon()
        {
            ClientCard tribute = GetTribute(Card.Attack);
            if (tribute == null)
                return false;

            return true;
        }

        // Skull Archfiend of Lightning costs 500 LP every Standby Phase, so it
        // only comes down while the life total can carry it for a few turns.
        private bool SkullArchfiendSummon()
        {
            if (Bot.LifePoints <= 2000)
                return false;
            return SkullTributeSummon();
        }

        private ClientCard GetTribute(int maxPower)
        {
            return Bot.GetMonsters()
                .Where(card => card.GetDefensePower() < maxPower)
                .OrderBy(TributeRank)
                .ThenBy(card => card.GetDefensePower())
                .FirstOrDefault();
        }

        // Lower is a better tribute: Skull Servants fuel the King, The Lady in
        // Wight also counts as one once she is in the graveyard.
        private int TributeRank(ClientCard card)
        {
            if (card.IsCode(CardId.SkullServant))
                return 0;
            if (card.IsCode(CardId.SoulAbsorbingBoneTower))
                return 1;
            if (card.IsCode(CardId.TheLadyInWight) && !Bot.GetMonsters().Any(m => m.IsFaceup() && m.Level <= 3 && m.HasRace(CardRace.Zombie) && !m.IsCode(CardId.TheLadyInWight)))
                return 1;
            if (card.IsCode(CardId.KingOfTheSkullServants))
                return 4;
            return 2;
        }

        // Skull Conductor destroys itself at the end of the Battle Phase, so it
        // is only Normal Summoned to hit something right away and never while
        // its hand effect has a partner.
        private bool SkullConductorSummon()
        {
            if (Duel.Phase != DuelPhase.Main1 || GetSkullConductorPartners() != null)
                return false;

            if (Enemy.GetMonsterCount() == 0)
                return true;

            return Enemy.GetMonsters().Any(card => card.IsFaceup() && card.GetDefensePower() < Card.Attack);
        }

        // The Lady in Wight protects face-up Level 3 or lower Zombies, but has
        // 0 ATK: summon her face-up only when she has a King or Skull Servant
        // to shield and nothing can attack her this turn cycle; otherwise she
        // is Set as a 2200 DEF wall.
        private bool TheLadyInWightSummon()
        {
            bool hasWard = Bot.GetMonsters().Any(card => card.IsFaceup() && card.Level <= 3 && card.HasRace(CardRace.Zombie));
            bool enemyCanAttack = Enemy.GetMonsters().Any(card => card.IsFaceup() && card.IsAttack());
            return hasWard && !enemyCanAttack;
        }

        // Soul-Absorbing Bone Tower cannot be attacked while another Zombie is
        // with it, and mills 2 for every Zombie Special Summon.
        private bool SoulAbsorbingBoneTowerSummon()
        {
            return Bot.GetMonsters().Any(card => card.IsFaceup() && card.HasRace(CardRace.Zombie));
        }

        // Skull Servants are only Set as tribute fodder for a skull in hand,
        // or when there is nothing better to play.
        private bool SkullServantSet()
        {
            return Bot.HasInHand(CardId.SummonedSkull) || Bot.HasInHand(CardId.SkullArchfiendOfLightning)
                || Bot.GetMonsterCount() == 0;
        }

        // Tribute prompts not preselected by a summon rule still release the
        // King's fuel first.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (!AI.HaveSelectedCards() && hint == HintMsg.Release && cards.Count >= min)
                return cards.OrderBy(TributeRank).ThenBy(card => card.GetDefensePower()).Take(min).ToList();

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }
    }
}
