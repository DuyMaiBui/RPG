using System.Collections.Generic;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class ActorRegistry
    {
        private readonly List<Actor> _actors = new();
        private readonly List<int> _generations = new();
        private readonly Stack<int> _freeIndices = new();

        public EntityId Spawn(ActorKind kind, int maximumHealth, int attackPower)
            => Spawn(kind, kind == ActorKind.Player ? FactionId.Red : FactionId.Blue, maximumHealth, attackPower);

        public EntityId Spawn(ActorKind kind, FactionId faction, int maximumHealth, int attackPower)
        {
            var index = _freeIndices.Count > 0 ? _freeIndices.Pop() : _actors.Count;
            if (index == _actors.Count)
            {
                _actors.Add(null);
                _generations.Add(0);
            }

            var id = new EntityId(index, _generations[index]);
            var components = new ActorComponentSet();
            components.Add(new ActorKindComponent(kind));
            components.Add(new FactionComponent(faction));
            components.Add(new HealthComponent(maximumHealth));
            components.Add(new AttackComponent(attackPower));
            _actors[index] = new Actor(id, components);
            return id;
        }

        public bool TryGet(EntityId id, out Actor actor)
        {
            if (id.Index < 0 || id.Index >= _actors.Count || _generations[id.Index] != id.Generation || _actors[id.Index] == null)
            {
                actor = null!;
                return false;
            }

            actor = _actors[id.Index]!;
            return true;
        }

        public bool Destroy(EntityId id)
        {
            if (!TryGet(id, out _)) return false;
            _actors[id.Index] = null;
            _generations[id.Index]++;
            _freeIndices.Push(id.Index);
            return true;
        }

        public ActorSnapshot[] CreateSnapshot()
        {
            var snapshots = new List<ActorSnapshot>(_actors.Count);
            foreach (var actor in _actors)
            {
                if (actor == null) continue;
                var kind = actor.Components.Get<ActorKindComponent>().Kind;
                var faction = actor.Components.Get<FactionComponent>().Faction;
                var health = actor.Components.Get<HealthComponent>();
                snapshots.Add(new ActorSnapshot(actor.Id, kind, faction, health.CurrentHealth, health.MaximumHealth,
                    health.IsDead ? ActorVisualState.Dead : ActorVisualState.Idle));
            }

            return snapshots.ToArray();
        }
    }
}
