using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeSoftBodyHandle
    {
        public ulong Opaque;
        public AuraSoftBodyId ToManaged() => new AuraSoftBodyId((int)Opaque, (int)(Opaque >> 32));
        public static NativeSoftBodyHandle From(AuraSoftBodyId value) => new NativeSoftBodyHandle { Opaque = ((ulong)(uint)value.Generation << 32) | (uint)value.Index };
    }
}
