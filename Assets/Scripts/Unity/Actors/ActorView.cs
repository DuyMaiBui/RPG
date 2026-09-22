using System;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorView : MonoBehaviour
    {
        [SerializeField] private ActorViewComponent[] _components;
        private bool _released;

        public SimulationEntityId EntityId { get; private set; }
        public FactionId Faction { get; private set; }

        public void Initialize(SimulationEntityId entityId, FactionId faction, FloatingCombatTextPool floatingTextPool)
        {
            if (_components == null || _components.Length == 0)
                throw new InvalidOperationException("ActorView prefab requires at least one ActorViewComponent.");

            EntityId = entityId;
            Faction = faction;
            _released = false;
            var context = new ActorViewContext(entityId, faction, floatingTextPool);
            foreach (var component in _components)
            {
                if (component == null)
                    throw new InvalidOperationException("ActorView prefab contains a missing ActorViewComponent reference.");
                component.Initialize(in context);
            }
        }

        public void ApplySnapshot(ActorSnapshot snapshot)
        {
            foreach (var component in _components)
                component.ApplySnapshot(snapshot);
        }

        public void PlaySignal(PresentationSignal signal)
        {
            foreach (var component in _components)
                component.PlaySignal(signal);
        }

        public void Release()
        {
            if (_released) return;
            _released = true;
            if (_components != null)
            {
                foreach (var component in _components)
                    component?.Release();
            }

            if (this != null) Destroy(gameObject);
        }
    }
}
