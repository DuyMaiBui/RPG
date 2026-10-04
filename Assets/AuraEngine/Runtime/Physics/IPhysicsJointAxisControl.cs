using AuraEngine.Core;

namespace AuraEngine.Physics
{
    /* Per-axis runtime joint control for the Jolt 3D constraint set. Obtain it with
       `world.JointControl as IPhysicsJointAxisControl`; backends without it (Null, Box2D) do not implement it.
       Support (other joint types return UnsupportedOperation, stale ids InvalidHandle, bad values InvalidDefinition):
         SixDof     axis 0..5: SetAxisLimits (Locked/Free/Limited + friction), SetAxisMotor (velocity, or position on
                    translation axes and rotation X).
         SwingTwist axis 0 = twist (Limited min/max, friction; motor velocity or position), axis 1 = normal swing half
                    cone, axis 2 = plane swing half cone (Limited, Max only; velocity motor about constraint Y/Z). */
    public interface IPhysicsJointAxisControl
    {
        AuraResult SetAxisLimits(AuraJointId joint, int axis, in AuraJointAxisLimit limit);

        AuraResult SetAxisMotor(AuraJointId joint, int axis, in AuraJointMotorDefinition motor);
    }
}
