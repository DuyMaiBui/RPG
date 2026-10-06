using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class MovementCohortComponent : IActorComponent
    {
        public int CohortId { get; private set; } = -1;

        public bool IsAssigned => CohortId >= 0;

        public void Assign(int cohortId) => CohortId = cohortId;

        public void Clear() => CohortId = -1;
    }
}
