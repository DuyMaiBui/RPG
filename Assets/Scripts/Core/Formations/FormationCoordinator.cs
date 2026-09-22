using System.Collections.Generic;
using RPG.Simulation.Contracts;

namespace RPG.Core.Formations
{
    public sealed class FormationCoordinator
    {
        private readonly Dictionary<int, SimulationVector2> _origins = new();

        public void Register(int formationId, SimulationVector2 origin) => _origins[formationId] = origin;

        public bool TryGetPosition(FormationSlotComponent slot, out SimulationVector2 position)
        {
            if (_origins.TryGetValue(slot.FormationId, out var origin))
            {
                position = origin + slot.LocalOffset;
                return true;
            }

            position = SimulationVector2.Zero;
            return false;
        }
    }
}
