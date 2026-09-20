using System;

namespace RPG.Simulation.Runtime
{
    public sealed class SimulationOptions
    {
        public SimulationOptions(int tickRate = 30, int maximumCatchUpTicks = 5, int inboundQueueCapacity = 1024)
        {
            TickRate = tickRate;
            MaximumCatchUpTicks = maximumCatchUpTicks;
            InboundQueueCapacity = inboundQueueCapacity;
        }

        public int TickRate { get; }
        public int MaximumCatchUpTicks { get; }
        public int InboundQueueCapacity { get; }

        public void Validate()
        {
            if (TickRate <= 0) throw new ArgumentOutOfRangeException(nameof(TickRate));
            if (MaximumCatchUpTicks <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumCatchUpTicks));
            if (InboundQueueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(InboundQueueCapacity));
        }
    }
}
