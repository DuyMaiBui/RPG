using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePose
    {
        public NativeVector3 Position;
        public NativeQuaternion Rotation;

        public static NativePose From(AuraPose value) =>
            new NativePose { Position = NativeVector3.From(value.Position), Rotation = NativeQuaternion.From(value.Rotation) };

        public AuraPose ToManaged() => new AuraPose(Position.ToManaged(), Rotation.ToManaged());
    }
}
