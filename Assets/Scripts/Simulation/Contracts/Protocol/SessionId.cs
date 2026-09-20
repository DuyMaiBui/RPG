using System;

namespace RPG.Simulation.Contracts
{
    public readonly struct SessionId
    {
        public SessionId(Guid value) => Value = value;
        public Guid Value { get; }
    }
}
