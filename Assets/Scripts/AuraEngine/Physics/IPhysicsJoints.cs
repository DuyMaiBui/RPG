using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsJoints
    {
        AuraJointId CreateJoint(in AuraJointDefinition definition);

        AuraResult DestroyJoint(AuraJointId joint);

        bool HasJoint(AuraJointId joint);
    }
}
