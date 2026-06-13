using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Generic-AI beatdown bot for the JTP (Joey the Passion) classic format.
    // No archetype combo logic — relies on DefaultExecutor's generic play:
    // summon the strongest monster, set backrow, and (via GameAI.OnSelectBattleCmd)
    // auto-attack. Staples get smart timing where DefaultExecutor provides it;
    // pure value/removal cards are activated whenever the engine offers them legally.
    // Deck: AI_JTP.ydk (JTP-whitelist-legal beatdown).
    [Deck("JTP", "AI_JTP")]
    public class JTPExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int PotOfGreed = 55144522;
            public const int GracefulCharity = 79571449;
            public const int SwordsOfRevealingLight = 72302403;
            public const int SnatchSteal = 45986603;
            public const int MonsterReborn = 83764718;
            public const int PrematureBurial = 70828912;
            public const int NoblemanOfCrossout = 17449108;
            public const int Fissure = 66788016;
            public const int TributeToTheDoomed = 79759861;
            public const int MirrorForce = 44095762;
            public const int MagicCylinder = 62279055;
            public const int Ceasefire = 36468556;
            public const int TimeWizard = 71625222;
            public const int GearfriedTheIronKnight = 423705;

            // Fusion package (Joey theme).
            public const int Polymerization = 24094653;
            public const int FlameSwordsman = 45231177;
            public const int FlameManipulator = 34460851;
            public const int Masaki = 44287299;
            public const int AlligatorsSwordDragon = 3366982;
            public const int AlligatorsSword = 64428736;
            public const int BabyDragon = 88819587;
        }

        public JTPExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Pure value / removal: activate whenever legally offered.
            // (The engine only lists a card in ActivableCards when its activation
            //  is actually legal, so a null-func "always" rule is safe here.)
            AddExecutor(ExecutorType.Activate, CardId.PotOfGreed);
            AddExecutor(ExecutorType.Activate, CardId.GracefulCharity);
            AddExecutor(ExecutorType.Activate, CardId.SwordsOfRevealingLight, SwordsOfRevealingLightEffect);
            AddExecutor(ExecutorType.Activate, CardId.SnatchSteal, SnatchStealEffect);
            AddExecutor(ExecutorType.Activate, CardId.MonsterReborn, MonsterRebornEffect);
            AddExecutor(ExecutorType.Activate, CardId.PrematureBurial, PrematureBurialEffect);
            AddExecutor(ExecutorType.Activate, CardId.NoblemanOfCrossout, NoblemanOfCrossoutEffect);
            AddExecutor(ExecutorType.Activate, CardId.Fissure);
            AddExecutor(ExecutorType.Activate, CardId.TributeToTheDoomed, TributeToTheDoomedEffect);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.MagicCylinder);
            AddExecutor(ExecutorType.Activate, CardId.Ceasefire);

            // Time Wizard's coin toss is a board gamble: activate only when the
            // opponent's board is at least as strong as ours (see TimeWizardEffect).
            AddExecutor(ExecutorType.Activate, CardId.TimeWizard, TimeWizardEffect);

            // Fusion summon (Joey theme): fuse before the generic summon rule so
            // the materials are spent on the fusion instead of being set/summoned.
            AddExecutor(ExecutorType.Activate, CardId.Polymerization, PolymerizationEffect);

            // Staples with DefaultExecutor's smart timing.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, DefaultCallOfTheHaunted);

            // Generic play: summon the best monster, set spells/traps, reposition.
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        // Activate Polymerization only when a known Joey fusion can actually be
        // made from cards in hand / on the field. Picks the fusion monster and its
        // two materials explicitly; returns false (skip) when no fusion is possible.
        private bool PolymerizationEffect()
        {
            // Flame Swordsman = Flame Manipulator + Masaki the Legendary Swordsman.
            if (Bot.HasInExtra(CardId.FlameSwordsman)
                && Bot.HasInHandOrHasInMonstersZone(CardId.FlameManipulator)
                && Bot.HasInHandOrHasInMonstersZone(CardId.Masaki))
            {
                AI.SelectCard(CardId.FlameSwordsman);
                AI.SelectMaterials(new[] { CardId.FlameManipulator, CardId.Masaki });
                return true;
            }

            // Alligator's Sword Dragon = Alligator's Sword + Baby Dragon.
            if (Bot.HasInExtra(CardId.AlligatorsSwordDragon)
                && Bot.HasInHandOrHasInMonstersZone(CardId.AlligatorsSword)
                && Bot.HasInHandOrHasInMonstersZone(CardId.BabyDragon))
            {
                AI.SelectCard(CardId.AlligatorsSwordDragon);
                AI.SelectMaterials(new[] { CardId.AlligatorsSword, CardId.BabyDragon });
                return true;
            }

            return false;
        }

        // Tribute to the Doomed: discard 1 card to destroy 1 monster on the field.
        // Only worth the card (and the discard cost) when the opponent actually has a
        // monster to blow up — gating on that also guarantees we never destroy one of
        // OUR own monsters. The destroy target (biggest enemy threat) and the discard
        // cost (least useful card) are both chosen in OnSelectCard below.
        private bool TributeToTheDoomedEffect()
        {
            return Enemy.GetMonsterCount() > 0;
        }

        // Swords of Revealing Light is a DEFENSIVE stall: it freezes the opponent's
        // monsters from attacking for 3 turns. Worth a card only when we're under
        // pressure — the opponent has monsters and isn't behind our board. Activating
        // it while we're safely ahead (and want to be attacking) just wastes it.
        private bool SwordsOfRevealingLightEffect()
        {
            if (Enemy.GetMonsterCount() == 0)
                return false; // nothing to freeze.

            ClientCard enemyBest = Enemy.GetMonsters().GetHighestAttackMonster();
            ClientCard botBest = Bot.GetMonsters().GetHighestAttackMonster();
            int enemyTopAtk = (enemyBest != null) ? enemyBest.Attack : 0;
            int botTopAtk = (botBest != null) ? botBest.Attack : 0;

            // Stall only when we're NOT safely ahead: the opponent has a monster that
            // matches or beats our best, so buying three turns of safety is worth it.
            return enemyTopAtk >= botTopAtk;
        }

        // Snatch Steal is an EQUIP spell — same Gearfried trap as Premature Burial:
        // equipping it to Gearfried the Iron Knight gets the spell (and the steal)
        // destroyed. Take the opponent's strongest FACE-UP monster that accepts
        // equips; skip the activation when the only target rejects equips.
        private bool SnatchStealEffect()
        {
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsCode(CardId.GearfriedTheIronKnight))
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();

            if (target == null)
                return false; // only equip-rejecting / face-down targets: don't waste it.

            AI.SelectCard(target);
            return true;
        }

        // Monster Reborn revives the strongest monster from EITHER graveyard.
        // Unlike Premature Burial it is NOT an equip, so even Gearfried is a fine
        // target. A minimum ATK floor keeps the one copy from being wasted on a
        // small body (fusion material, Time Wizard).
        private bool MonsterRebornEffect()
        {
            ClientCard target = Bot.Graveyard.Concat(Enemy.Graveyard)
                .Where(card => card != null && card.IsCanRevive() && card.Attack >= 1500)
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();

            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Nobleman of Crossout can hit ANY face-down monster — including ours.
        // Only fire it at an ENEMY face-down, and pick that target explicitly.
        private bool NoblemanOfCrossoutEffect()
        {
            ClientCard target = Enemy.GetMonsters().FirstOrDefault(card => card.IsFacedown());
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Premature Burial is an EQUIP spell: it revives a monster from the GY AND
        // stays equipped to it. Gearfried the Iron Knight destroys any Equip Card put
        // on it — which then triggers Premature Burial's "when destroyed, destroy that
        // monster", so reviving Gearfried this way loses 800 LP, the spell, AND the
        // monster in one shot. Pick the strongest revivable target that ACCEPTS equips;
        // skip the activation entirely when the only candidates reject equips.
        private bool PrematureBurialEffect()
        {
            ClientCard target = Bot.Graveyard
                .GetMatchingCards(card => card.IsCanRevive() && !card.IsCode(CardId.GearfriedTheIronKnight))
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();

            if (target == null)
                return false; // only equip-rejecting targets available: don't waste the card.

            AI.SelectCard(target);
            return true;
        }

        // Time Wizard: 50/50 coin toss. Win -> destroy ALL opponent monsters.
        // Lose -> destroy ALL our monsters and take half their total ATK as damage.
        // The engine resolves the toss itself; our only decision is whether to gamble.
        private bool TimeWizardEffect()
        {
            return ShouldGambleTimeWizard();
        }

        // The gamble is worth it only when the opponent has monsters AND their board
        // is at least as strong as ours (more monsters, or a bigger/equal beater):
        // winning clears a threatening field, and losing our weaker board costs less.
        //
        // We evaluate OUR side WITHOUT Time Wizard — it's the chip we're betting, so
        // its puny 500 ATK must not make our board look "strong enough" to skip the
        // gamble. Excluding it also keeps this decision consistent whether Time Wizard
        // is still in hand (summon time) or already face-up on the field (activate time).
        private bool ShouldGambleTimeWizard()
        {
            int enemyMonsters = Enemy.GetMonsterCount();
            if (enemyMonsters == 0)
                return false; // nothing to destroy: only the downside is on the table.

            int botMonsters = 0;
            int botTopAtk = 0;
            foreach (ClientCard m in Bot.GetMonsters())
            {
                if (m.IsCode(CardId.TimeWizard))
                    continue;
                botMonsters++;
                if (m.Attack > botTopAtk)
                    botTopAtk = m.Attack;
            }

            ClientCard enemyBest = Enemy.GetMonsters().GetHighestAttackMonster();
            int enemyTopAtk = (enemyBest != null) ? enemyBest.Attack : 0;

            return enemyMonsters > botMonsters || enemyTopAtk >= botTopAtk;
        }

        // Centralised card-selection smarts, handled per hint so it's independent of
        // the order the engine asks (more robust than queuing via AI.SelectCard):
        //   • Discard cost (Graceful Charity, Tribute to the Doomed) → pitch the least
        //     useful cards (see DiscardScore).
        //   • Destroy target (Tribute to the Doomed, Nobleman) → the biggest ENEMY
        //     monster; never one of ours.
        // An explicit queued selection (AI.SelectCard) always takes precedence.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (!AI.HaveSelectedCards())
            {
                if (hint == HintMsg.Discard && cards.Count >= min)
                {
                    List<ClientCard> ordered = new List<ClientCard>(cards);
                    ordered.Sort((a, b) => DiscardScore(b).CompareTo(DiscardScore(a))); // discard-first first
                    return ordered.Take(min).ToList();
                }

                if (hint == HintMsg.Destroy)
                {
                    List<ClientCard> enemyMonsters = Enemy.GetMonsters();
                    ClientCard biggestEnemy = cards
                        .Where(card => enemyMonsters.Contains(card))
                        .OrderByDescending(card => card.Attack)
                        .FirstOrDefault();
                    if (biggestEnemy != null)
                        return new List<ClientCard> { biggestEnemy };
                }
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        // How readily we can part with a card when forced to discard (HIGHER = pitch
        // sooner). Monsters go before spells/traps — we keep removal, draw and
        // protection in hand — and among monsters the higher-Level ones go first:
        // they need tributes and clog the hand, and Joey can revive a big body from
        // the GY later (Premature Burial / Call of the Haunted), so pitching it is cheap.
        private static int DiscardScore(ClientCard card)
        {
            if (card.HasType(CardType.Monster))
                return card.Level; // 1..12 — bigger monsters discarded first
            return 0;              // spells / traps: keep the longest
        }

        // The conservative DefaultExecutor sets weak monsters face-down in defense —
        // but a set (face-down) monster can't use its ignition effect, so Time Wizard
        // would never get to gamble. Summon it face-up when the gamble is favorable;
        // otherwise fall back to the safe default (set it in defense and wait).
        public override bool OnSelectMonsterSummonOrSet(ClientCard card)
        {
            if (card.IsCode(CardId.TimeWizard) && ShouldGambleTimeWizard())
                return false; // false = summon face-up in attack (not set face-down).

            return base.OnSelectMonsterSummonOrSet(card);
        }

        // Beatdown attack policy: trade into an EQUAL-ATK monster in attack position
        // instead of refusing the even trade like the conservative DefaultExecutor
        // (which only trades on the very last attacker). Clearing the opponent's board
        // is worth the trade for an aggressive deck. Safety checks (dangerous /
        // invincible / battle-immune defenders) are reused from OnPreBattleBetween.
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
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
