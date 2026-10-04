using System.Runtime.InteropServices;
namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRigJointDesc
    {
        public int ParentIndex;
        public NativePose BindPose;
    }
}
