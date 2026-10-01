using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePhysicsEvent
    {
        public int Type;
        public NativeEntityHandle EntityA;
        public NativeEntityHandle EntityB;
        public NativeBodyHandle BodyA;
        public NativeBodyHandle BodyB;
        public uint ShapeA;
        public uint ShapeB;
        public NativeVector3 Point;
        public NativeVector3 Normal;
        public float Impulse;

        public AuraPhysicsEvent ToManaged() =>
            new AuraPhysicsEvent(
                (AuraPhysicsEventType)Type,
                SimulationEntityId.None,
                SimulationEntityId.None,
                BodyA.ToManaged(),
                BodyB.ToManaged(),
                new PhysicsShapeId((int)ShapeA, 0),
                new PhysicsShapeId((int)ShapeB, 0),
                Point.ToManaged(),
                Normal.ToManaged(),
                Impulse);
    }
}
