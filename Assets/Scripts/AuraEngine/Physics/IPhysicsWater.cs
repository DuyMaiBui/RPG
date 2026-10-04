using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsWater
    {
        AuraWaterId CreateWater(in AuraWaterDefinition definition);
        AuraResult DestroyWater(AuraWaterId water);
        AuraResult SetWaterParameters(AuraWaterId water, in AuraWaterDefinition definition);
        AuraResult ApplyWaterStep(AuraWaterId water, float deltaTime);
    }
}
