using System;
using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeCharacterDesc
    {
        public NativePose Pose;
        public float Radius;
        public float Height;
        public float Mass;
        public float MaxSlopeAngle;
        public uint Layer;
        public uint Pad0;
        public ulong CollisionMask;
        public float StepHeight;
        public uint Pad1;

        public static NativeCharacterDesc From(in AuraCharacterDefinition definition) =>
            new NativeCharacterDesc
            {
                Pose = NativePose.From(definition.Pose),
                Radius = definition.Radius,
                Height = definition.Height,
                Mass = definition.Mass,
                MaxSlopeAngle = definition.MaxSlopeAngle,
                Layer = (uint)definition.Layer.Value,
                CollisionMask = definition.CollisionMask.Bits,
                StepHeight = definition.StepHeight,
            };
    }
}
