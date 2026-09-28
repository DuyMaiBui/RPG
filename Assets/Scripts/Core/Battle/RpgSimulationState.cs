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
        public NavigationGrid Navigation { get; }
        public AStarPathfinder Pathfinder { get; }
        public NavigationFlowFieldCache FlowFields { get; }
        public DynamicOccupancyGrid Occupancy { get; }
        public MovementCohortCoordinator MovementCohorts { get; }
        public ProjectileRegistry Projectiles { get; } = new();
        public FormationCoordinator Formations { get; } = new();
        public WaveSpawner Waves { get; } = new();
        public SimulationVector2 RedBasePosition { get; private set; }
        public SimulationVector2 BlueBasePosition { get; private set; }
        public float BaseReach { get; private set; } = 1f;

        public RpgSimulationState(int navigationWidth = 20, int navigationHeight = 10, float navigationCellSize = 1f)
        {
            Navigation = new NavigationGrid(
                navigationWidth,
                navigationHeight,
                navigationCellSize,
                new SimulationVector2(
                    -navigationWidth * navigationCellSize * 0.5f,
                    -navigationHeight * navigationCellSize * 0.5f));
            Pathfinder = new AStarPathfinder(Navigation);
            FlowFields = new NavigationFlowFieldCache(Navigation);
            Occupancy = new DynamicOccupancyGrid(Navigation);
            MovementCohorts = new MovementCohortCoordinator();
            var baseOffset = navigationWidth * navigationCellSize * 0.4f;
            RedBasePosition = new SimulationVector2(-baseOffset, 0f);
            BlueBasePosition = new SimulationVector2(baseOffset, 0f);
        }

        public void ConfigureBases(SimulationVector2 redBase, SimulationVector2 blueBase, float reach)
        {
            RedBasePosition = redBase;
            BlueBasePosition = blueBase;
            BaseReach = System.MathF.Max(0.1f, reach);
        }

        public SimulationVector2 GetEnemyBasePosition(FactionId faction) =>
            faction == FactionId.Red ? BlueBasePosition : RedBasePosition;

        public void BeginTick() => Signals.Clear();
    }
}
