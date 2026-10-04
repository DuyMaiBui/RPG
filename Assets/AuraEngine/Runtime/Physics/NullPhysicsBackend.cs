using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public sealed class NullPhysicsBackend : IPhysicsBackend
    {
        public static readonly NullPhysicsBackend Instance = new NullPhysicsBackend();

        string IPhysicsBackend.Name => "Null";

        AuraPhysicsCapabilities IPhysicsBackend.Capabilities =>
            AuraPhysicsCapabilities.BodyStatic |
            AuraPhysicsCapabilities.BodyDynamic |
            AuraPhysicsCapabilities.BodyKinematic |
            AuraPhysicsCapabilities.ShapeBox |
            AuraPhysicsCapabilities.ShapeSphere |
            AuraPhysicsCapabilities.ShapeCapsule |
            AuraPhysicsCapabilities.ShapeCylinder |
            AuraPhysicsCapabilities.ShapeConvexMesh |
            AuraPhysicsCapabilities.ShapeTriangleMesh;

        IPhysicsWorld IPhysicsBackend.CreateWorld(in AuraWorldDefinition definition) =>
            new NullPhysicsWorld(definition);
    }
}
