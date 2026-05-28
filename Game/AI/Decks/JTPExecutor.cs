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
            public const int PrematureBurial = 70828912;
            public const int NoblemanOfCrossout = 17449108;
            public const int Fissure = 66788016;
            public const int TributeToTheDoomed = 79759861;
            public const int MirrorForce = 44095762;
            public const int MagicCylinder = 62279055;
            public const int Ceasefire = 36468556;

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
            AddExecutor(ExecutorType.Activate, CardId.SwordsOfRevealingLight);
            AddExecutor(ExecutorType.Activate, CardId.SnatchSteal);
            AddExecutor(ExecutorType.Activate, CardId.PrematureBurial);
            AddExecutor(ExecutorType.Activate, CardId.NoblemanOfCrossout);
            AddExecutor(ExecutorType.Activate, CardId.Fissure);
            AddExecutor(ExecutorType.Activate, CardId.TributeToTheDoomed);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.MagicCylinder);
            AddExecutor(ExecutorType.Activate, CardId.Ceasefire);

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
    }
}
