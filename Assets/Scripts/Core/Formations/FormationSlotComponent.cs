using RPG.Simulation.Contracts;

namespace RPG.Core.Formations
{
    public sealed class FormationSlotComponent : RPG.Core.Actors.IActorComponent
    {
        public FormationSlotComponent(int formationId, SimulationVector2 localOffset)
        {
            FormationId = formationId;
            LocalOffset = localOffset;
        }

        public int FormationId { get; }
        public SimulationVector2 LocalOffset { get; private set; }

        public void AssignOffset(SimulationVector2 localOffset) => LocalOffset = localOffset;
    }
}
