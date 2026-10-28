using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Pilot for the Necromancer player deck: the same Zombie synchro engine as
    // the Lich King boss, so it reuses that game plan and only adds rules for
    // the cards the boss list does not run.
    [Deck("PilotNecromancer", "AI_PilotNecromancer")]
    public class HalloweenPilotNecromancerExecutor : HalloweenLichExecutor
    {
        public class NecromancerCardId
        {
            public const int VampireLady = 26495087;
            public const int GoldSarcophagus = 75500286;
            public const int DarkBribe = 77538567;
            public const int IronChainDragon = 19974580;
            public const int ArchfiendZombieSkull = 59969392;
            public const int ArmoryArm = 910003009; // PRE-ERRATA passcode (Edison whitelist).
        }

        public HalloweenPilotNecromancerExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, NecromancerCardId.DarkBribe, DefaultTrap);
            AddExecutor(ExecutorType.Activate, NecromancerCardId.GoldSarcophagus);
            AddExecutor(ExecutorType.SpSummon, NecromancerCardId.ArchfiendZombieSkull);
            AddExecutor(ExecutorType.SpSummon, NecromancerCardId.IronChainDragon);
            AddExecutor(ExecutorType.SpSummon, NecromancerCardId.ArmoryArm);
            AddExecutor(ExecutorType.SummonOrSet, NecromancerCardId.VampireLady);
        }
    }
}
