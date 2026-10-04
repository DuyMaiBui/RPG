using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRagdollHandle
    {
        public ulong Opaque;
        public AuraRagdollId ToManaged() => new AuraRagdollId((int)(Opaque & 0xFFFFFFFFul), (int)(Opaque >> 32));
        public static NativeRagdollHandle From(AuraRagdollId id) => new NativeRagdollHandle { Opaque = ((ulong)(uint)id.Generation << 32) | (uint)id.Index };
    }
}
