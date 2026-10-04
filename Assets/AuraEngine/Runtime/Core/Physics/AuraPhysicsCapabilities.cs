using System;

namespace AuraEngine.Core
{
    [Flags]
    public enum AuraPhysicsCapabilities
    {
        None = 0,

        BodyStatic = 1 << 0,
        BodyDynamic = 1 << 1,
        BodyKinematic = 1 << 2,

        ShapeBox = 1 << 3,
        ShapeSphere = 1 << 4,
        ShapeCapsule = 1 << 5,
        ShapeCylinder = 1 << 6,
        ShapeConvexMesh = 1 << 7,
        ShapeTriangleMesh = 1 << 8,

        QueryRaycast = 1 << 9,
        QueryShapeCast = 1 << 10,
        QueryOverlap = 1 << 11,

        Triggers = 1 << 12,
        Contacts = 1 << 13,
        SleepWake = 1 << 14,

        ShapePlane = 1 << 15,
        ShapeTaperedCapsule = 1 << 16,
        ShapeTaperedCylinder = 1 << 17,

        Joints = 1 << 18,

        Characters = 1 << 19,
        ShapeHeightField = 1 << 20,
        Vehicles = 1 << 21,
        SoftBodies = 1 << 22,
        Hair = 1 << 23,
        Ragdolls = 1 << 24,
        Water = 1 << 25,

        /* Runtime body control: IPhysicsWorld.BodyControl. */
        BodyControl = 1 << 26,

        /* Runtime joint control: IPhysicsWorld.JointControl. */
        JointControl = 1 << 27,

        /* World gravity setter and force field zones: IPhysicsWorld.ForceFields. Bit 30 keeps clear of the
           bits parallel work packages append after JointControl. */
        ForceFields = 1 << 30,

        All = ~0,
    }
}
