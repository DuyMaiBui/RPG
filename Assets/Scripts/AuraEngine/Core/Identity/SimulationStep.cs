namespace AuraEngine.Core
{
    public readonly struct SimulationStep
    {
        public SimulationStep(SimulationTick tick, float deltaTime)
        {
            Tick = tick;
            DeltaTime = deltaTime;
        }

        public SimulationTick Tick { get; }
        public float DeltaTime { get; }
    }
}
