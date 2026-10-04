using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeCharacterState
    {
        public NativeVector3 Position;
        public NativeVector3 Velocity;
        public byte IsGrounded;
        public byte Pad0;
        public byte Pad1;
        public byte Pad2;
    }
}
