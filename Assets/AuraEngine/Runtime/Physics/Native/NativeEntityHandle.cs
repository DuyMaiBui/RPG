using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeEntityHandle
    {
        public uint Index;
        public uint Generation;
    }
}
