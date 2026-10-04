using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeJointMotorDesc
    {
        public int Mode;
        public float Target;
        public float MaxForce;
        public float SpringFrequency;
        public float SpringDamping;
        public uint Pad0;

        public static NativeJointMotorDesc From(in AuraJointMotorDefinition motor) =>
            new NativeJointMotorDesc
            {
                Mode = (int)motor.Mode,
                Target = motor.Target,
                MaxForce = motor.MaxForce,
                SpringFrequency = motor.SpringFrequency,
                SpringDamping = motor.SpringDamping,
            };
    }
}
