using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeQueryFilter
    {
        public ulong LayerMask;
        public int TriggerInteraction;
        public int Flags;
        public NativeEntityHandle IgnoredEntity;
        public NativeBodyHandle IgnoredBody;

        public static NativeQueryFilter From(in AuraPhysicsQueryFilter filter) =>
            new NativeQueryFilter
            {
                LayerMask = filter.LayerMask.Bits,
                TriggerInteraction = (int)filter.TriggerInteraction,
                Flags = (int)filter.Flags,
                IgnoredEntity = new NativeEntityHandle
                {
                    Index = filter.IgnoredEntity.IsNone ? uint.MaxValue : (uint)filter.IgnoredEntity.Index,
                    Generation = filter.IgnoredEntity.IsNone ? uint.MaxValue : (uint)filter.IgnoredEntity.Generation,
                },
                IgnoredBody = new NativeBodyHandle
                {
                    Index = filter.IgnoredBody.IsValid ? (uint)filter.IgnoredBody.Index : uint.MaxValue,
                    Generation = filter.IgnoredBody.IsValid ? (uint)filter.IgnoredBody.Generation : uint.MaxValue,
                },
            };
    }
}
