using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public sealed class ManagedPhysicsBackend : IPhysicsBackend
    {
        public static readonly ManagedPhysicsBackend Instance = new ManagedPhysicsBackend();

        string IPhysicsBackend.Name => "AuraEngine.Managed";

        AuraPhysicsCapabilities IPhysicsBackend.Capabilities =>
            AuraPhysicsCapabilities.BodyStatic |
            AuraPhysicsCapabilities.BodyDynamic |
            AuraPhysicsCapabilities.BodyKinematic |
            AuraPhysicsCapabilities.ShapeBox |
            AuraPhysicsCapabilities.ShapeSphere |
            AuraPhysicsCapabilities.QueryRaycast |
            AuraPhysicsCapabilities.QueryOverlap |
            AuraPhysicsCapabilities.Triggers |
            AuraPhysicsCapabilities.Contacts;

        IPhysicsWorld IPhysicsBackend.CreateWorld(in AuraWorldDefinition definition) =>
            new ManagedPhysicsWorld(definition);
    }
}
