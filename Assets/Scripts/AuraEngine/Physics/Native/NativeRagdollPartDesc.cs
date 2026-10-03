using System.Runtime.InteropServices;
namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRagdollPartDesc
    {
        public NativeBodyDesc Body;
        public NativeJointDesc JointToParent;
    }
}
