using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsSoftBodies
    {
        AuraSoftBodyId CreateSoftBody(in AuraSoftBodyDefinition definition);
        AuraResult DestroySoftBody(AuraSoftBodyId softBody);
        bool TryGetState(AuraSoftBodyId softBody, out AuraSoftBodyState state);
    }
}
