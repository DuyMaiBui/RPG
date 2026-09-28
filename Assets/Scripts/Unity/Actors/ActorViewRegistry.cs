using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorViewRegistry
    {
        private readonly Dictionary<SimulationEntityId, ActorView> _views = new();
        private readonly ActorView _prefab;
        private readonly Transform _root;
        private readonly FloatingCombatTextPool _floatingTextPool;

        public ActorViewRegistry(ActorView prefab = null, Transform root = null, FloatingCombatTextPool floatingTextPool = null)
        {
            _prefab = prefab;
            _root = root;
            _floatingTextPool = floatingTextPool;
        }

        public void Add(SimulationEntityId id, ActorView view) => _views.Add(id, view);

        public bool TryGet(SimulationEntityId id, out ActorView view) => _views.TryGetValue(id, out view);

        public bool Ensure(SimulationEntityId id, FactionId faction)
        {
            if (_views.ContainsKey(id))
                return true;
            if (_prefab == null || _root == null || _floatingTextPool == null)
                throw new System.InvalidOperationException("ActorViewRegistry cannot create a view without serialized prefab references.");

            var view = Object.Instantiate(_prefab, _root);
            view.name = $"{faction}_{id.Index}";
            view.Initialize(id, faction, _floatingTextPool);
            _views.Add(id, view);
            return true;
        }

        public IEnumerable<KeyValuePair<SimulationEntityId, ActorView>> Entries => _views;

        public Dictionary<SimulationEntityId, ActorView>.Enumerator GetEnumerator() => _views.GetEnumerator();

        public void Remove(SimulationEntityId id)
        {
            if (!_views.Remove(id, out var view)) return;
            view.Release();
        }

        public void ReleaseAll()
        {
            foreach (var view in _views.Values)
                view.Release();
            _views.Clear();
        }
    }
}
