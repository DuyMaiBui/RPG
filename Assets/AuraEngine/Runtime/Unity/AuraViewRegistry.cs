using System;
using System.Collections.Generic;
using AuraEngine.Core;

namespace AuraEngine.Unity
{
    public sealed class AuraViewRegistry
    {
        private readonly Dictionary<SimulationEntityId, AuraPhysicsView> _views =
            new Dictionary<SimulationEntityId, AuraPhysicsView>();

        public int Count => _views.Count;

        public void Register(SimulationEntityId entity, AuraPhysicsView view)
        {
            if (entity.IsNone)
                throw new ArgumentException("The entity id is invalid.", nameof(entity));

            if (view == null)
                throw new ArgumentNullException(nameof(view));

            _views[entity] = view;
        }

        public bool Unregister(SimulationEntityId entity) => _views.Remove(entity);

        public bool TryGet(SimulationEntityId entity, out AuraPhysicsView view) => _views.TryGetValue(entity, out view);

        public void Clear() => _views.Clear();

        public Dictionary<SimulationEntityId, AuraPhysicsView>.Enumerator GetEnumerator() => _views.GetEnumerator();
    }
}
