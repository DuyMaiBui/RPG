namespace RPG.Simulation.Contracts
{
    public readonly struct PredictedMoveInput
    {
        public PredictedMoveInput(ClientSequence sequence, SimulationTick tick, SimulationVector2 direction)
        {
            Sequence = sequence;
            Tick = tick;
            Direction = direction;
        }

        public ClientSequence Sequence { get; }
        public SimulationTick Tick { get; }
        public SimulationVector2 Direction { get; }
    }
}
