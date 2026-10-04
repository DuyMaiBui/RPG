using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Simulation
{
    public sealed class EntityRegistry : IDisposable
    {
        private readonly List<EntitySlot> _slots = new List<EntitySlot>();
        private readonly List<int> _freeSlots = new List<int>();
        private readonly Dictionary<PhysicsBodyId, SimulationEntityId> _bodyToEntity =
            new Dictionary<PhysicsBodyId, SimulationEntityId>();
        private readonly IPhysicsWorld _physics;
        private int _aliveCount;

        public EntityRegistry(IPhysicsWorld physics)
        {
            _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        }

        public int Capacity => _slots.Count;

        public int AliveCount => _aliveCount;

        public SimulationEntityId CreateEntity()
        {
            if (_freeSlots.Count > 0)
            {
                var index = _freeSlots[_freeSlots.Count - 1];
                _freeSlots.RemoveAt(_freeSlots.Count - 1);
                var slot = _slots[index];
                slot.Alive = true;
                slot.HasBody = false;
                slot.Body = PhysicsBodyId.Invalid;
                _slots[index] = slot;
                _aliveCount++;
                return new SimulationEntityId(index, slot.Generation);
            }

            _slots.Add(new EntitySlot
            {
                Generation = 0,
                Alive = true,
                HasBody = false,
                Body = PhysicsBodyId.Invalid,
            });
            _aliveCount++;
            return new SimulationEntityId(_slots.Count - 1, 0);
        }

        public bool DestroyEntity(SimulationEntityId entity)
        {
            if (!TryResolve(entity, out var index))
                return false;

            var slot = _slots[index];
            if (slot.HasBody)
            {
                _bodyToEntity.Remove(slot.Body);
                _physics.DestroyBody(slot.Body);
                slot.HasBody = false;
                slot.Body = PhysicsBodyId.Invalid;
            }

            slot.Alive = false;
            slot.Generation++;
            _slots[index] = slot;
            _freeSlots.Add(index);
            _aliveCount--;
            return true;
        }

        public bool IsAlive(SimulationEntityId entity) => TryResolve(entity, out _);

        public PhysicsBodyId AttachBody(SimulationEntityId entity, in AuraPhysicsBodyDefinition definition)
        {
            if (!TryResolve(entity, out var index))
                return PhysicsBodyId.Invalid;

            var slot = _slots[index];
            if (slot.HasBody)
            {
                _bodyToEntity.Remove(slot.Body);
                _physics.DestroyBody(slot.Body);
                slot.HasBody = false;
                slot.Body = PhysicsBodyId.Invalid;
                _slots[index] = slot;
            }

            var body = _physics.CreateBody(definition);
            if (!body.IsValid)
                return PhysicsBodyId.Invalid;

            slot.HasBody = true;
            slot.Body = body;
            _slots[index] = slot;
            _bodyToEntity[body] = new SimulationEntityId(index, slot.Generation);
            return body;
        }

        public bool TryGetBody(SimulationEntityId entity, out PhysicsBodyId body)
        {
            if (TryResolve(entity, out var index) && _slots[index].HasBody)
            {
                body = _slots[index].Body;
                return true;
            }

            body = PhysicsBodyId.Invalid;
            return false;
        }

        public bool TryResolveBody(PhysicsBodyId body, out SimulationEntityId entity)
        {
            if (_bodyToEntity.TryGetValue(body, out entity) && TryResolve(entity, out _))
                return true;

            entity = SimulationEntityId.None;
            return false;
        }

        public int CopyAliveEntities(Span<SimulationEntityId> buffer)
        {
            var written = 0;
            for (var index = 0; index < _slots.Count && written < buffer.Length; index++)
            {
                var slot = _slots[index];
                if (slot.Alive)
                    buffer[written++] = new SimulationEntityId(index, slot.Generation);
            }

            return written;
        }

        void IDisposable.Dispose()
        {
            _slots.Clear();
            _freeSlots.Clear();
            _bodyToEntity.Clear();
            _aliveCount = 0;
        }

        private bool TryResolve(SimulationEntityId entity, out int index)
        {
            index = entity.Index;
            return !entity.IsNone &&
                   index < _slots.Count &&
                   _slots[index].Alive &&
                   _slots[index].Generation == entity.Generation;
        }
    }
}
