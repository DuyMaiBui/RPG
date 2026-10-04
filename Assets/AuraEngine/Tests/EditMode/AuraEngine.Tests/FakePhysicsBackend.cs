using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Tests
{
    public sealed class FakePhysicsBackend : IPhysicsBackend
    {
        private readonly List<AuraPhysicsEvent> _pendingEvents = new List<AuraPhysicsEvent>();

        string IPhysicsBackend.Name => "Fake";

        AuraPhysicsCapabilities IPhysicsBackend.Capabilities =>
            AuraPhysicsCapabilities.BodyStatic |
            AuraPhysicsCapabilities.BodyDynamic |
            AuraPhysicsCapabilities.BodyKinematic |
            AuraPhysicsCapabilities.ShapeBox |
            AuraPhysicsCapabilities.ShapeSphere |
            AuraPhysicsCapabilities.QueryRaycast |
            AuraPhysicsCapabilities.QueryOverlap;

        public FakePhysicsWorld World { get; private set; }

        IPhysicsWorld IPhysicsBackend.CreateWorld(in AuraWorldDefinition definition)
        {
            World = new FakePhysicsWorld(definition, _pendingEvents);
            return World;
        }

        public void EnqueueEvent(in AuraPhysicsEvent value) => _pendingEvents.Add(value);
    }
}
