using System.Collections.Generic;

namespace AuraEngine.Networking
{
    public sealed class AuraLockstepBuffer
    {
        private readonly Dictionary<uint, List<AuraInputCommand>> _commandsByTick = new Dictionary<uint, List<AuraInputCommand>>();
        private readonly HashSet<uint> _locallySubmitted = new HashSet<uint>();

        public void Submit(uint tick, AuraInputCommand command)
        {
            if (!_commandsByTick.TryGetValue(tick, out var commands))
            {
                commands = new List<AuraInputCommand>();
                _commandsByTick[tick] = commands;
            }

            commands.Add(command);
        }

        public void SubmitLocal(uint tick)
        {
            _locallySubmitted.Add(tick);
        }

        public bool IsTickComplete(uint tick, int expectedClients)
        {
            if (!_commandsByTick.TryGetValue(tick, out var commands))
                return expectedClients <= 0;

            return commands.Count >= expectedClients;
        }

        public IReadOnlyList<AuraInputCommand> Take(uint tick)
        {
            if (_commandsByTick.TryGetValue(tick, out var commands))
            {
                _commandsByTick.Remove(tick);
                return commands;
            }

            return System.Array.Empty<AuraInputCommand>();
        }

        public int PendingTickCount => _commandsByTick.Count;
    }
}
