using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeWorldHandle
    {
        public ulong Opaque;
    }
}
