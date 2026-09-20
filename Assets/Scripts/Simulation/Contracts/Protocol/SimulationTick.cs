namespace RPG.Simulation.Contracts
{
    public readonly struct SimulationTick
    {
        public SimulationTick(long value) => Value = value;
        public long Value { get; }
    }
}
