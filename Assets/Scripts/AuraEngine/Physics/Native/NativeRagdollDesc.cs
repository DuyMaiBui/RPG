using System;
using System.Runtime.InteropServices;
namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRagdollDesc
    {
        public NativeRigDesc Rig;
        public IntPtr Parts;
        public uint PartCount;
        public uint CollisionGroup;
    }
}
