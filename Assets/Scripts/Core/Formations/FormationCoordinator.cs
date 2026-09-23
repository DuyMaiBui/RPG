using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Formations
{
    public sealed class FormationCoordinator
    {
        private readonly Dictionary<int, SimulationVector2> _origins = new();
        private readonly Dictionary<int, SimulationVector2[]> _layouts = new();

        public void Register(int formationId, SimulationVector2 origin) => _origins[formationId] = origin;

        public void RegisterLayout(int formationId, SimulationVector2 origin, SimulationVector2[] offsets)
        {
            if (offsets == null || offsets.Length == 0)
                throw new System.ArgumentException("A formation layout requires at least one slot.", nameof(offsets));

            _origins[formationId] = origin;
            _layouts[formationId] = (SimulationVector2[])offsets.Clone();
        }

        public void ReassignSlots(ActorRegistry actors, int formationId)
        {
            if (!_layouts.TryGetValue(formationId, out var layout)) return;

            var candidates = new List<Actor>();
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor) ||
                    !actor.Components.TryGet<FormationSlotComponent>(out var slot) ||
                    slot.FormationId != formationId || actor.Components.Get<HealthComponent>().IsDead)
                    continue;
                candidates.Add(actor);
            }

            var assigned = new bool[layout.Length];
            var origin = _origins[formationId];
            for (var actorIndex = 0; actorIndex < candidates.Count; actorIndex++)
            {
                var actor = candidates[actorIndex];
                var position = actor.Components.Get<PositionComponent>().Position;
                var bestSlot = -1;
                var bestDistance = float.MaxValue;
                for (var slotIndex = 0; slotIndex < layout.Length; slotIndex++)
                {
                    if (assigned[slotIndex]) continue;
                    var delta = origin + layout[slotIndex] - position;
                    var distance = delta.LengthSquared;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestSlot = slotIndex;
                }

                if (bestSlot < 0) break;
                assigned[bestSlot] = true;
                actor.Components.Get<FormationSlotComponent>().AssignOffset(layout[bestSlot]);
            }
        }

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
