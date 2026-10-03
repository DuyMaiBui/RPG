using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsHair
    {
        AuraHairId CreateHair(in AuraHairDefinition definition);
        AuraResult DestroyHair(AuraHairId hair);
        bool TryGetState(AuraHairId hair, out AuraHairState state);
    }
}
