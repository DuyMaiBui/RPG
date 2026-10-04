using AuraEngine.Core;

namespace AuraEngine.Physics
{
    /* Runtime target of a mouse joint (AuraJointType.Mouse, Plane2D). World space; z is ignored. Returns
       UnsupportedOperation for other joint types and backends, InvalidHandle for stale ids and InvalidDefinition for
       non-finite targets. */
    public interface IPhysicsJointTarget
    {
        AuraResult SetJointTarget(AuraJointId joint, AuraVector3 target);
    }
}
