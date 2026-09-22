using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class PathFollowerComponent : IActorComponent
    {
        public SimulationVector2[] Path { get; private set; }
        public int NextNode { get; private set; }
        public SimulationVector2 Destination { get; private set; }

        public bool HasPath => Path != null && NextNode < Path.Length;

        public void SetPath(SimulationVector2 destination, SimulationVector2[] path)
        {
            Destination = destination;
            Path = path;
            NextNode = path != null && path.Length > 1 ? 1 : 0;
        }

        public void Clear()
        {
            Path = null;
            NextNode = 0;
        }

        public SimulationVector2 CurrentNode => Path[NextNode];

        public void AdvanceIfClose(SimulationVector2 position, float threshold)
        {
            if (!HasPath) return;
            var difference = CurrentNode - position;
            if (difference.LengthSquared <= threshold * threshold)
                NextNode++;
        }
    }
}
