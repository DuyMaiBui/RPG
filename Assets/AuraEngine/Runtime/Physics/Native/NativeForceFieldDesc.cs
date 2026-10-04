using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    /* Mirror of AuraForceFieldDesc (aura_types.h, 104 bytes). Keep both in lockstep. */
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeForceFieldDesc
    {
        public int Shape;
        public int Kind;
        public int Mode;
        public int Falloff;
        public NativePose Pose;
        public NativeVector3 HalfExtents;
        public float Radius;
        public NativeVector3 Vector;
        public float Strength;
        public float MinRadius;
        public float MaxRadius;
        public ulong LayerMask;
        public byte Enabled;
        public byte Pad0;
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
        public byte Pad4;
        public byte Pad5;
        public byte Pad6;

        public static NativeForceFieldDesc From(in AuraForceFieldDefinition definition) => new NativeForceFieldDesc
        {
            Shape = (int)definition.Shape,
            Kind = (int)definition.Kind,
            Mode = (int)definition.Mode,
            Falloff = (int)definition.Falloff,
            Pose = NativePose.From(definition.Pose),
            HalfExtents = NativeVector3.From(definition.HalfExtents),
            Radius = definition.Radius,
            Vector = NativeVector3.From(definition.Vector),
            Strength = definition.Strength,
            MinRadius = definition.MinRadius,
            MaxRadius = definition.MaxRadius,
            LayerMask = definition.LayerMask.Bits,
            Enabled = (byte)(definition.Enabled ? 1 : 0),
        };
    }
}
