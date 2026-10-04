using System;
using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeWorldDesc
    {
        public int Mode;
        public NativeVector3 Gravity;
        public uint InitialBodyCapacity;
        public float FixedDeltaTime;
        public IntPtr CollisionMasks;
        public uint CollisionMaskCount;

        public static NativeWorldDesc From(in AuraWorldDefinition definition) =>
            new NativeWorldDesc
            {
                Mode = (int)definition.Mode,
                Gravity = NativeVector3.From(definition.Gravity),
                InitialBodyCapacity = (uint)definition.InitialBodyCapacity,
                FixedDeltaTime = definition.FixedDeltaTime,
            };
    }
}
