using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeVector3
    {
        public float X;
        public float Y;
        public float Z;

        public static NativeVector3 From(AuraVector3 value) => new NativeVector3 { X = value.X, Y = value.Y, Z = value.Z };

        public AuraVector3 ToManaged() => new AuraVector3(X, Y, Z);
    }
}
