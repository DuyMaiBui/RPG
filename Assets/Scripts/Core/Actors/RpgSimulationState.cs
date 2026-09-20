using System.Collections.Generic;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class RpgSimulationState
    {
        public ActorRegistry Actors { get; } = new();
        public Dictionary<PlayerId, EntityId> PlayerActors { get; } = new();
        public List<PresentationSignal> Signals { get; } = new();

        public void BeginTick() => Signals.Clear();
    }
}
