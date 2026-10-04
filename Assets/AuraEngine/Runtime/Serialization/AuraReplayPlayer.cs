using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Serialization
{
    public static class AuraReplayPlayer
    {
        public static bool TryValidate(
            AuraSimulationWorld world,
            AuraReplay replay,
            AuraReplayCommandSource commandSource,
            float deltaTime,
            out SimulationTick mismatchTick)
        {
            mismatchTick = default;
            if (world == null || replay == null)
                return false;

            if (AuraSimulationSnapshot.Capture(world).ComputeHash() != replay.InitialState.ComputeHash())
                return false;

            for (var index = 0; index < replay.FrameCount; index++)
            {
                var frame = replay[index];
                commandSource?.Invoke(world, frame.Tick);
                world.Step(new SimulationStep(frame.Tick, deltaTime));
                if (world.ComputeStateHash() != frame.StateHash)
                {
                    mismatchTick = frame.Tick;
                    return false;
                }
            }

            return true;
        }
    }
}
