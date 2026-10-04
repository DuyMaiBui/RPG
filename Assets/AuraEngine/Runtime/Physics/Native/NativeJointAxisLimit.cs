using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeJointAxisLimit
    {
        public byte Mode;
        public byte Pad0;
        public byte Pad1;
        public byte Pad2;
        public float MinLimit;
        public float MaxLimit;
        public float MaxFriction;

        public static NativeJointAxisLimit From(in AuraJointAxisLimit limit) =>
            new NativeJointAxisLimit
            {
                Mode = (byte)limit.Mode,
                MinLimit = limit.Min,
                MaxLimit = limit.Max,
                MaxFriction = limit.MaxFriction,
            };
    }
}
