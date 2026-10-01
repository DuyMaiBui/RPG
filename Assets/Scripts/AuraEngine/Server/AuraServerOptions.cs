using System;

namespace AuraEngine.Server
{
    public sealed class AuraServerOptions
    {
        public AuraServerOptions(int tickRate = 30, int maximumCatchUpTicks = 5, int commandQueueCapacity = 1024)
        {
            TickRate = tickRate;
            MaximumCatchUpTicks = maximumCatchUpTicks;
            CommandQueueCapacity = commandQueueCapacity;
        }

        public int TickRate { get; }

        public int MaximumCatchUpTicks { get; }

        public int CommandQueueCapacity { get; }

        public float FixedDeltaTime => 1f / TickRate;

        public void Validate()
        {
            if (TickRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(TickRate));

            if (MaximumCatchUpTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaximumCatchUpTicks));

            if (CommandQueueCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(CommandQueueCapacity));
        }
    }
}
