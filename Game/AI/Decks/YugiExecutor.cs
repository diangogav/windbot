using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Generic-AI beatdown bot for the JTP (Joey the Passion) classic format,
    // Yami Yugi theme. Same skeleton as JTPExecutor: DefaultExecutor generic
    // play (summon the strongest monster, set backrow, auto-attack via
    // GameAI.OnSelectBattleCmd) plus per-card smarts for the staples and the
    // Yugi signature package: Valkyrion + Magnet Warriors, Chimera fusion,
    // Dark Magician + Thousand Knives, and Kuriboh as a battle shield.
    // Deck: AI_Yugi.ydk (JTP-whitelist-legal beatdown).
    [Deck("Yugi", "AI_Yugi")]
    public class YugiExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Draw / value.
            public const int PotOfGreed = 55144522;
            public const int GracefulCharity = 79571449;

            // Removal / disruption.
            public const int DarkHole = 53129443;
            public const int Fissure = 66788016;
            public const int NoblemanOfCrossout = 17449108;
            public const int TributeToTheDoomed = 79759861;
            public const int ThousandKnives = 63391643;
            public const int ChangeOfHeart = 4031928;
            public const int SnatchSteal = 45986603;
            public const int SwordsOfRevealingLight = 72302403;
            public const int MonsterReborn = 83764718;
            public const int PrematureBurial = 70828912;

            // Traps.
            public const int MirrorForce = 44095762;
            public const int MagicCylinder = 62279055;
            public const int SpellbindingCircle = 18807108;
            public const int Ceasefire = 36468556;

            // Monsters.
            public const int DarkMagician = 46986414;
            public const int SummonedSkull = 70781052;
            public const int BusterBlader = 78193831;
            public const int Kuriboh = 40640057;
            public const int BigShieldGardna = 65240384;

            // Magnet package: Valkyrion special summons by tributing the three.
            public const int AlphaTheMagnetWarrior = 99785935;
            public const int BetaTheMagnetWarrior = 39256679;
            public const int GammaTheMagnetWarrior = 11549357;
            public const int ValkyrionTheMagnaWarrior = 75347539;

            // Fusion package (Yugi theme): Gazelle + Berfomet = Chimera.
            public const int Polymerization = 24094653;
            public const int Gazelle = 5818798;
            public const int Berfomet = 77207191;
            public const int Chimera = 4796100;

            // Opposing JTP hazard: destroys any Equip Card placed on it, so it
            // must never be the target of Snatch Steal (see SnatchStealEffect).
            public const int GearfriedTheIronKnight = 423705;
        }

        public YugiExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Pure value / removal: activate whenever legally offered.
            // (The engine only lists a card in ActivableCards when its activation
            //  is actually legal, so a null-func "always" rule is safe here.)
            AddExecutor(ExecutorType.Activate, CardId.PotOfGreed);
            AddExecutor(ExecutorType.Activate, CardId.GracefulCharity);
            AddExecutor(ExecutorType.Activate, CardId.SwordsOfRevealingLight, SwordsOfRevealingLightEffect);
            AddExecutor(ExecutorType.Activate, CardId.ChangeOfHeart, ChangeOfHeartEffect);
            AddExecutor(ExecutorType.Activate, CardId.SnatchSteal, SnatchStealEffect);
            AddExecutor(ExecutorType.Activate, CardId.MonsterReborn, MonsterRebornEffect);
            AddExecutor(ExecutorType.Activate, CardId.PrematureBurial, PrematureBurialEffect);
            AddExecutor(ExecutorType.Activate, CardId.NoblemanOfCrossout, NoblemanOfCrossoutEffect);
            AddExecutor(ExecutorType.Activate, CardId.Fissure);
            // Thousand Knives needs Dark Magician on our field and an enemy
            // monster to hit — both enforced by the engine's legality check.
            AddExecutor(ExecutorType.Activate, CardId.ThousandKnives);
            AddExecutor(ExecutorType.Activate, CardId.TributeToTheDoomed, TributeToTheDoomedEffect);
            AddExecutor(ExecutorType.Activate, CardId.DarkHole, DefaultDarkHole);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.MagicCylinder);
            AddExecutor(ExecutorType.Activate, CardId.SpellbindingCircle, SpellbindingCircleEffect);
            AddExecutor(ExecutorType.Activate, CardId.Ceasefire);
            AddExecutor(ExecutorType.Activate, CardId.Kuriboh, KuribohEffect);

            // Berfomet's summon trigger searches Gazelle from the deck — pure value.
            AddExecutor(ExecutorType.Activate, CardId.Berfomet);
            // Chimera floats when destroyed: revive a material from the GY.
            AddExecutor(ExecutorType.Activate, CardId.Chimera, ChimeraReviveEffect);

            // Valkyrion: the engine offers this special summon only when Alpha,
            // Beta and Gamma are actually available to tribute, so an
            // unconditional rule is safe — always cash in the 3500 body.
            AddExecutor(ExecutorType.SpSummon, CardId.ValkyrionTheMagnaWarrior);

            // Fusion summon (Yugi theme): fuse before the generic summon rule so
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

        // Activate Polymerization only when Chimera can actually be made from
        // cards in hand / on the field. Picks the fusion monster and its two
        // materials explicitly; returns false (skip) when no fusion is possible.
        private bool PolymerizationEffect()
        {
            // Chimera the Flying Mythical Beast = Gazelle + Berfomet.
            if (Bot.HasInExtra(CardId.Chimera)
                && Bot.HasInHandOrHasInMonstersZone(CardId.Gazelle)
                && Bot.HasInHandOrHasInMonstersZone(CardId.Berfomet))
            {
                AI.SelectCard(CardId.Chimera);
                AI.SelectMaterials(new[] { CardId.Gazelle, CardId.Berfomet });
                return true;
            }

            return false;
        }

        // Chimera's death trigger revives Gazelle or Berfomet from the GY — a
        // free body, so always take it. Prefer Gazelle, the better attacker.
        private bool ChimeraReviveEffect()
        {
            AI.SelectCard(CardId.Gazelle, CardId.Berfomet);
            return true;
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

        // Change of Heart steals the opponent's best face-up monster for the turn:
        // it can attack its former owner or be spent as tribute / fusion material.
        // Only worth the card when there is a real target to take.
        private bool ChangeOfHeartEffect()
        {
            ClientCard target = Util.GetBestEnemyMonster(true, true);
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Snatch Steal is an EQUIP spell: equipping it to Gearfried the Iron Knight
        // (the opposing JTP bot runs it) gets the spell — and the steal — destroyed.
        // Take the opponent's strongest FACE-UP monster that accepts equips; skip
        // the activation when the only target rejects equips.
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
        // Valkyrion is excluded: it can only be special summoned by its own
        // tribute procedure, so it is not a legal revival target. A minimum ATK
        // floor keeps the one copy from being wasted on a small body.
        private bool MonsterRebornEffect()
        {
            ClientCard target = Bot.Graveyard.Concat(Enemy.Graveyard)
                .Where(card => card != null
                    && card.IsCanRevive()
                    && card.Attack >= 1500
                    && !card.IsCode(CardId.ValkyrionTheMagnaWarrior))
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();

            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Premature Burial is an EQUIP spell that revives from OUR graveyard for
        // 800 LP. Same Valkyrion exclusion as Monster Reborn, plus an ATK floor so
        // the LP cost is never paid to bring back a weenie like Kuriboh.
        private bool PrematureBurialEffect()
        {
            ClientCard target = Bot.Graveyard
                .GetMatchingCards(card => card.IsCanRevive()
                    && card.Attack >= 1400
                    && !card.IsCode(CardId.ValkyrionTheMagnaWarrior))
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

        // Spellbinding Circle locks one enemy monster out of attacking and
        // changing position. Spend it on a real threat, not on a weenie.
        private bool SpellbindingCircleEffect()
        {
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && card.Attack >= 1500)
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();

            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Kuriboh discards itself to make one battle damage 0 — a one-shot hand
        // shield. Save it for damage that actually threatens the game: low LP,
        // or an enemy attacker big enough to take half our LP in one hit.
        private bool KuribohEffect()
        {
            return Bot.LifePoints <= 3000 || Util.GetBestAttack(Enemy) * 2 >= Bot.LifePoints;
        }

        // Centralised card-selection smarts, handled per hint so it's independent of
        // the order the engine asks (more robust than queuing via AI.SelectCard):
        //   • Discard cost (Graceful Charity, Tribute to the Doomed) → pitch the least
        //     useful cards (see DiscardScore).
        //   • Destroy target (Tribute to the Doomed, Thousand Knives) → the biggest
        //     ENEMY monster; never one of ours.
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
        // they clog the hand and a big body can be revived from the GY later
        // (Monster Reborn / Premature Burial / Call of the Haunted), so pitching
        // one is cheap. Two exceptions kept to the very end:
        //   • Valkyrion — discarding it loses it forever (it can't be revived).
        //   • Kuriboh — its whole value is BEING in hand (battle shield).
        private static int DiscardScore(ClientCard card)
        {
            if (card.IsCode(CardId.ValkyrionTheMagnaWarrior) || card.IsCode(CardId.Kuriboh))
                return 1;          // pitch only when nothing else is left
            if (card.HasType(CardType.Monster))
                return card.Level; // 1..12 — bigger monsters discarded first
            return 0;              // spells / traps: keep the longest
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
