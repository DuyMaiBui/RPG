using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeQueryHit
    {
        public NativeEntityHandle Entity;
        public NativeBodyHandle Body;
        public uint Shape;
        public float Distance;
        public NativeVector3 Point;
        public NativeVector3 Normal;

        public AuraPhysicsQueryHit ToManaged() =>
            new AuraPhysicsQueryHit(
                SimulationEntityId.None,
                Body.ToManaged(),
                new PhysicsShapeId((int)Shape, 0),
                Distance,
                Point.ToManaged(),
                Normal.ToManaged());
    }
}
