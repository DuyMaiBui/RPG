using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public readonly struct OrcaLine
    {
        public OrcaLine(SimulationVector2 point, SimulationVector2 direction)
        {
            Point = point;
            Direction = direction;
        }

        public SimulationVector2 Point { get; }
        public SimulationVector2 Direction { get; }
    }
}
