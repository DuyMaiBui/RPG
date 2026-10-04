using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeBodyState
    {
        public NativeBodyHandle Body;
        public NativeEntityHandle Entity;
        public NativePose Pose;
        public NativeVector3 LinearVelocity;
        public NativeVector3 AngularVelocity;
        public byte IsAwake;
        public byte Pad0;
        public byte Pad1;
        public byte Pad2;
        public uint Flags;

        public AuraBodyState ToManaged(SimulationEntityId entity) =>
            new AuraBodyState(
                Body.ToManaged(),
                entity,
                Pose.ToManaged(),
                LinearVelocity.ToManaged(),
                AngularVelocity.ToManaged(),
                IsAwake != 0,
                Flags);
    }
}
