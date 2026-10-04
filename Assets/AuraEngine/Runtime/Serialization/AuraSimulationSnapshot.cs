using System;
using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Serialization
{
    public sealed class AuraSimulationSnapshot
    {
        public AuraSimulationSnapshot(SimulationTick tick, AuraBodyState[] bodyStates)
        {
            Tick = tick;
            BodyStates = bodyStates ?? Array.Empty<AuraBodyState>();
        }

        public SimulationTick Tick { get; }

        public AuraBodyState[] BodyStates { get; }

        public static AuraSimulationSnapshot Capture(AuraSimulationWorld world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            var states = new AuraBodyState[world.BodyCount];
            var count = world.CopyBodyStates(states);
            if (count != states.Length)
                Array.Resize(ref states, count);

            if (count > 1)
                Array.Sort(states, 0, count, AuraBodyStateComparer.Instance);

            return new AuraSimulationSnapshot(world.CurrentTick, states);
        }

        public ulong ComputeHash()
        {
            var hash = AuraStateHasher.CombineInt(AuraStateHasher.Begin(), unchecked((int)Tick.Value));
            var sorted = new AuraBodyState[BodyStates.Length];
            Array.Copy(BodyStates, sorted, BodyStates.Length);
            if (sorted.Length > 1)
                Array.Sort(sorted, 0, sorted.Length, AuraBodyStateComparer.Instance);

            for (var index = 0; index < sorted.Length; index++)
                hash = AuraStateHasher.HashBodyState(hash, sorted[index]);

            return hash;
        }
    }
}
