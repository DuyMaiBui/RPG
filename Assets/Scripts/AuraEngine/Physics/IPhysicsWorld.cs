using System;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsWorld : IDisposable, IPhysicsQuery, IPhysicsEventSource
    {
        AuraPhysicsMode Mode { get; }

        AuraPhysicsCapabilities Capabilities { get; }

        int BodyCount { get; }

        IPhysicsJoints Joints { get; }

        IPhysicsCharacters Characters { get; }
        IPhysicsVehicles Vehicles { get; }
        IPhysicsSoftBodies SoftBodies { get; }

        IPhysicsContacts Contacts { get; }

        IPhysicsSerialization Serialization { get; }

        PhysicsBodyId CreateBody(in AuraPhysicsBodyDefinition definition);

        AuraResult DestroyBody(PhysicsBodyId body);

        bool HasBody(PhysicsBodyId body);

        void SetKinematicTarget(PhysicsBodyId body, in AuraPose pose);

        void ApplyImpulse(PhysicsBodyId body, AuraVector3 impulse);

        AuraResult SetSurfaceVelocity(PhysicsBodyId body, AuraVector3 velocity);

        AuraResult GetBodyState(PhysicsBodyId body, out AuraBodyState state);

        int CopyBodyStates(Span<AuraBodyState> buffer);

        void Step(float deltaTime);
    }
}
