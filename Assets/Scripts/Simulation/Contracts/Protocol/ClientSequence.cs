namespace RPG.Simulation.Contracts
{
    public readonly struct ClientSequence
    {
        public ClientSequence(long value) => Value = value;
        public long Value { get; }
    }
}
