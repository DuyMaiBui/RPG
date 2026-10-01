using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeQuaternion
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public static NativeQuaternion From(AuraQuaternion value) => new NativeQuaternion { X = value.X, Y = value.Y, Z = value.Z, W = value.W };

        public AuraQuaternion ToManaged() => new AuraQuaternion(X, Y, Z, W);
    }
}
