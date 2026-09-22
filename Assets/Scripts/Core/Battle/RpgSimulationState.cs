using System.Collections.Generic;
using RPG.Core.Navigation;
using RPG.Core.Projectiles;
using RPG.Core.Formations;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class RpgSimulationState
    {
        public ActorRegistry Actors { get; } = new();
        public Dictionary<PlayerId, EntityId> PlayerActors { get; } = new();
        public List<PresentationSignal> Signals { get; } = new();
        public NavigationGrid Navigation { get; } = new(20, 10, 1f, new SimulationVector2(-10f, -5f));
        public AStarPathfinder Pathfinder { get; }
        public ProjectileRegistry Projectiles { get; } = new();
        public FormationCoordinator Formations { get; } = new();

        public RpgSimulationState()
        {
            Pathfinder = new AStarPathfinder(Navigation);
        }
        public void BeginTick() => Signals.Clear();
    }
}
