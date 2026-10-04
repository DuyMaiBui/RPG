using System;

namespace AuraEngine.Core
{
    /* Freedom, range and friction of one joint axis. Axis order for SixDof: 0..2 translation X/Y/Z (m),
       3..5 rotation X/Y/Z (rad) in the joint frame. For rotation Y/Z the Limited range is a swing cone, so only
       Max (the half angle, 0..pi) is used unless the joint selects the pyramid swing shape. */
    public readonly struct AuraJointAxisLimit
    {
        public AuraJointAxisLimit(AuraJointAxisMode mode, float min, float max, float maxFriction = 0f)
        {
            Mode = mode;
            Min = min;
            Max = max;
            MaxFriction = maxFriction;
        }

        public AuraJointAxisMode Mode { get; }
        public float Min { get; }
        public float Max { get; }

        /* Friction force (N) or torque (N*m) applied while no motor drives the axis. 0 = none. */
        public float MaxFriction { get; }

        public static AuraJointAxisLimit Locked => new AuraJointAxisLimit(AuraJointAxisMode.Locked, 0f, 0f);

        public static AuraJointAxisLimit Free => new AuraJointAxisLimit(AuraJointAxisMode.Free, 0f, 0f);

        public static AuraJointAxisLimit Limited(float min, float max, float maxFriction = 0f) =>
            new AuraJointAxisLimit(AuraJointAxisMode.Limited, min, max, maxFriction);

        /* Mirrors the kernel's validation so authoring errors surface before CreateJoint. */
        public bool IsValid(int axis, bool pyramidSwing)
        {
            const float pi = (float)Math.PI;
            if (axis < 0 || axis > 5 || Mode < AuraJointAxisMode.Locked || Mode > AuraJointAxisMode.Limited)
                return false;
            if (float.IsNaN(MaxFriction) || float.IsInfinity(MaxFriction) || MaxFriction < 0f)
                return false;
            if (Mode != AuraJointAxisMode.Limited)
                return true;
            if (float.IsNaN(Min) || float.IsInfinity(Min) || float.IsNaN(Max) || float.IsInfinity(Max) || Min > Max)
                return false;
            if (axis == 3)
                return Min >= -pi && Max <= pi;
            if (axis >= 4)
                return pyramidSwing ? Min >= -pi && Max <= pi : Max >= 0f && Max <= pi;
            return true;
        }
    }
}
