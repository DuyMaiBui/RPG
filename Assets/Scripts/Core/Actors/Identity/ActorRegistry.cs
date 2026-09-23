using System.Collections.Generic;
using RPG.Core.Formations;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class ActorRegistry
    {
        private readonly List<Actor> _actors = new();
        private readonly List<int> _generations = new();
        private readonly Stack<int> _freeIndices = new();

        public EntityId Spawn(ActorKind kind, int maximumHealth, int attackPower)
            => Spawn(kind, kind == ActorKind.Player ? FactionId.Red : FactionId.Blue, new ActorSpawnData(
                maximumHealth,
                attackPower,
                SimulationVector2.Zero,
                0.35f,
                0f,
                10f,
                0.5f,
                1f));

        public EntityId Spawn(ActorKind kind, FactionId faction, int maximumHealth, int attackPower)
            => Spawn(kind, faction, new ActorSpawnData(
                maximumHealth,
                attackPower,
                SimulationVector2.Zero,
                0.35f,
                0f,
                10f,
                0.5f,
                1f));

        public EntityId Spawn(ActorKind kind, FactionId faction, ActorSpawnData data)
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
            components.Add(new HealthComponent(data.MaximumHealth));
            components.Add(new AttackComponent(data.AttackPower));
            components.Add(new PositionComponent(data.Position));
            components.Add(new BodyComponent(data.Radius));
            components.Add(new MovementComponent(data.MoveSpeed));
            if (kind == ActorKind.Player)
                components.Add(new ManualMovementComponent());
            components.Add(new VisionComponent(data.VisionRange));
            components.Add(new TargetComponent());
            components.Add(new AttackRangeComponent(data.AttackRange));
            components.Add(new AttackCooldownComponent(data.AttackCooldown));
            components.Add(new AutoCombatStateComponent());
            components.Add(new TargetPriorityComponent(data.TargetPriority));
            components.Add(new PathFollowerComponent());
            if (data.AttackType == AttackType.Projectile)
                components.Add(new ProjectileWeaponComponent(data.ProjectileSpeed, data.ProjectileRadius, data.ProjectileLifetime));
            if (data.FormationId >= 0)
                components.Add(new FormationSlotComponent(data.FormationId, data.FormationOffset));
            _actors[index] = new Actor(id, components);
            return id;
        }

        public int SlotCount => _actors.Count;

        public bool TryGetAt(int index, out Actor actor)
        {
            if (index < 0 || index >= _actors.Count || _actors[index] == null)
            {
                actor = null!;
                return false;
            }

            actor = _actors[index]!;
            return true;
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
                var position = actor.Components.Get<PositionComponent>();
                var body = actor.Components.Get<BodyComponent>();
                var attackRange = actor.Components.Get<AttackRangeComponent>();
                var vision = actor.Components.Get<VisionComponent>();
                var target = actor.Components.Get<TargetComponent>();
                var behavior = actor.Components.Get<AutoCombatStateComponent>();
                snapshots.Add(new ActorSnapshot(actor.Id, kind, faction, health.CurrentHealth, health.MaximumHealth,
                    position.Position, body.Radius, attackRange.Reach, vision.Range, target.CurrentTarget,
                    health.IsDead ? ActorVisualState.Dead : ActorVisualState.Idle, behavior.State));
            }

            return snapshots.ToArray();
        }

        public BattleResult GetBattleResult()
        {
            var redAlive = false;
            var blueAlive = false;
            for (var index = 0; index < _actors.Count; index++)
            {
                if (!TryGetAt(index, out var actor) || actor.Components.Get<HealthComponent>().IsDead)
                    continue;

                if (actor.Components.Get<FactionComponent>().Faction == FactionId.Red) redAlive = true;
                if (actor.Components.Get<FactionComponent>().Faction == FactionId.Blue) blueAlive = true;
            }

            if (redAlive && blueAlive) return BattleResult.Ongoing;
            if (redAlive) return BattleResult.RedWon;
            if (blueAlive) return BattleResult.BlueWon;
            return BattleResult.Draw;
        }
    }
}
