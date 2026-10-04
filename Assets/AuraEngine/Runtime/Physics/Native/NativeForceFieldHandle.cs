using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeForceFieldHandle
    {
        public ulong Opaque;
    }
}
