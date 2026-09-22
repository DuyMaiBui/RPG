using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public readonly struct ActorViewContext
    {
        public ActorViewContext(SimulationEntityId entityId, FactionId faction, FloatingCombatTextPool floatingTextPool)
        {
            EntityId = entityId;
            Faction = faction;
            FloatingTextPool = floatingTextPool;
            FactionColor = faction == FactionId.Red
                ? new Color(0.85f, 0.18f, 0.2f)
                : new Color(0.18f, 0.35f, 0.9f);
        }

        public SimulationEntityId EntityId { get; }
        public FactionId Faction { get; }
        public FloatingCombatTextPool FloatingTextPool { get; }
        public Color FactionColor { get; }
    }
}
