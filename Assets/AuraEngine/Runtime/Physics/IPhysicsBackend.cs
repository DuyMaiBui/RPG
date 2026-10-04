using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsBackend
    {
        string Name { get; }

        AuraPhysicsCapabilities Capabilities { get; }

        IPhysicsWorld CreateWorld(in AuraWorldDefinition definition);
    }
}
