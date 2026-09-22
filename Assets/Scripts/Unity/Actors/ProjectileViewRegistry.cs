using System;
using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ProjectileViewRegistry
    {
        private readonly ProjectileView _prefab;
        private readonly Transform _root;
        private readonly Dictionary<SimulationEntityId, ProjectileView> _active = new();
        private readonly Stack<ProjectileView> _pool = new();
        private readonly List<SimulationEntityId> _expired = new();

        public ProjectileViewRegistry(ProjectileView prefab, Transform root)
        {
            _prefab = prefab;
            _root = root;
        }

        public void Apply(ReadOnlyMemory<ProjectileSnapshot> snapshots)
        {
            var span = snapshots.Span;
            for (var index = 0; index < span.Length; index++)
            {
                var snapshot = span[index];
                if (!_active.TryGetValue(snapshot.Entity, out var view))
                {
                    view = Rent();
                    _active.Add(snapshot.Entity, view);
                }

                view.ApplySnapshot(snapshot);
            }

            _expired.Clear();
            foreach (var pair in _active)
            {
                var found = false;
                for (var index = 0; index < span.Length; index++)
                {
                    if (span[index].Entity != pair.Key) continue;
                    found = true;
                    break;
                }

                if (!found) _expired.Add(pair.Key);
            }

            for (var index = 0; index < _expired.Count; index++)
            {
                var id = _expired[index];
                var view = _active[id];
                _active.Remove(id);
                view.Release();
                _pool.Push(view);
            }
        }

        public void ReleaseAll()
        {
            foreach (var view in _active.Values)
            {
                view.Release();
                _pool.Push(view);
            }

            _active.Clear();
        }

        private ProjectileView Rent()
        {
            if (_pool.Count > 0) return _pool.Pop();
            return UnityEngine.Object.Instantiate(_prefab, _root);
        }
    }
}
