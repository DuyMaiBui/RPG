using System.Runtime.InteropServices;
namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRigDesc
    {
        public System.IntPtr Joints;
        public uint JointCount;
    }
}
