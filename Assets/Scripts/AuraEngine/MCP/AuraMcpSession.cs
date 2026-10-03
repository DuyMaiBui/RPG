using System;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;

namespace AuraEngine.MCP
{
    public sealed class AuraMcpSession : IDisposable
    {
        private AuraSimulationWorld _world;
        private uint _tick;

        public bool HasWorld => _world != null;

        public void CreateWorld()
        {
            if (_world != null)
                ((IDisposable)_world).Dispose();

            if (!AuraEngine.Physics.Native.NativePhysicsBackend.IsAvailable())
                throw new InvalidOperationException("The AuraEngine native physics plugin (libaura) is not available.");

            _world = new AuraSimulationWorld(new AuraEngine.Physics.Native.NativePhysicsBackend(), new AuraWorldDefinition(initialBodyCapacity: 256));
            _tick = 0;
        }

        public void AddBox(AuraVector3 position, AuraVector3 halfExtents, bool dynamic)
        {
            EnsureWorld();
            var shape = AuraPhysicsShapeDefinition.Box(halfExtents);
            var definition = dynamic
                ? AuraPhysicsBodyDefinition.CreateDynamic(new AuraPose(position, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, shape)
                : AuraPhysicsBodyDefinition.CreateStatic(new AuraPose(position, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, shape);
            _world.AttachBody(_world.CreateEntity(), definition);
        }

        public void AddSphere(AuraVector3 position, float radius, bool dynamic)
        {
            EnsureWorld();
            var shape = AuraPhysicsShapeDefinition.Sphere(radius);
            var definition = dynamic
                ? AuraPhysicsBodyDefinition.CreateDynamic(new AuraPose(position, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, shape)
                : AuraPhysicsBodyDefinition.CreateStatic(new AuraPose(position, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, shape);
            _world.AttachBody(_world.CreateEntity(), definition);
        }

        public void Step(int count)
        {
            EnsureWorld();
            for (var index = 0; index < count; index++)
                _world.Step(new SimulationStep(new SimulationTick(_tick++), _world.Definition.FixedDeltaTime));
        }

        public ulong StateHash()
        {
            EnsureWorld();
            return _world.ComputeStateHash();
        }

        public int BodyCount()
        {
            EnsureWorld();
            return _world.BodyCount;
        }

        public uint Tick => _tick;

        public bool Raycast(AuraVector3 origin, AuraVector3 direction, float maxDistance, out AuraPhysicsQueryHit hit) =>
            _world.Raycast(new AuraRay(origin, direction), maxDistance, AuraPhysicsQueryFilter.All, out hit);

        public void Dispose()
        {
            if (_world != null)
                ((IDisposable)_world).Dispose();
            _world = null;
        }

        private void EnsureWorld()
        {
            if (_world == null)
                throw new InvalidOperationException("No world exists. Call world_create first.");
        }
    }
}
