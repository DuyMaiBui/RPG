using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeVehicleWheelState
    {
        public byte HasContact;
        public byte Pad0, Pad1, Pad2;
        public float SuspensionLength, SteerAngle, AngularVelocity;
        public NativeVector3 ContactNormal;
        public AuraVehicleWheelState ToManaged() => new AuraVehicleWheelState(HasContact != 0, SuspensionLength, SteerAngle, AngularVelocity, ContactNormal.ToManaged());
    }
}
