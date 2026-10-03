using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeVehicleHandle
    {
        public ulong Opaque;
        public AuraVehicleId ToManaged() => new AuraVehicleId((int)(Opaque & 0xFFFFFFFFul), (int)(Opaque >> 32));
        public static NativeVehicleHandle From(AuraVehicleId id) => new NativeVehicleHandle { Opaque = ((ulong)(uint)id.Generation << 32) | (uint)id.Index };
    }
}
