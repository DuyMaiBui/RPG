using System.Collections.Generic;

namespace AuraEngine.Networking
{
    public readonly struct AuraLockstepFrame
    {
        public AuraLockstepFrame(uint tick, List<AuraInputCommand> commands)
        {
            Tick = tick;
            Commands = commands ?? new List<AuraInputCommand>();
        }

        public uint Tick { get; }

        public List<AuraInputCommand> Commands { get; }
    }
}
