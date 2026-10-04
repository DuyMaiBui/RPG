using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeJointFeedback
    {
        public float Force;
        public float Torque;
        public float MotorLoad;
        public float Position;
        public int MotorMode;
        public byte IsBroken;
        public byte Pad0;
        public byte Pad1;
        public byte Pad2;

        public AuraJointFeedback ToManaged() =>
            new AuraJointFeedback(Force, Torque, MotorLoad, Position, (AuraJointMotorMode)MotorMode, IsBroken != 0);
    }
}
