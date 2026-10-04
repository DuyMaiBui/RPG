using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeJointRef
    {
        public uint Index;
        public uint Generation;

        public static NativeJointRef From(AuraJointId joint) =>
            joint.IsValid
                ? new NativeJointRef { Index = (uint)joint.Index, Generation = (uint)joint.Generation }
                : new NativeJointRef { Index = uint.MaxValue, Generation = uint.MaxValue };
    }
}
