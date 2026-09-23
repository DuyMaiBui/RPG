using System.Collections.Generic;
using RPG.Simulation.Contracts;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;

namespace RPG.Unity
{
    public sealed class ActorViewRegistry
    {
        private readonly Dictionary<SimulationEntityId, ActorView> _views = new();

        public void Add(SimulationEntityId id, ActorView view) => _views.Add(id, view);

        public bool TryGet(SimulationEntityId id, out ActorView view) => _views.TryGetValue(id, out view);

        public IEnumerable<KeyValuePair<EntityId, ActorView>> Entries => _views;

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
