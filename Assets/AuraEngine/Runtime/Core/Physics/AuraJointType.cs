namespace AuraEngine.Core
{
    public enum AuraJointType
    {
        Distance = 0,
        Fixed = 1,
        Hinge = 2,
        Point = 3,
        Slider = 4,
        Cone = 5,
        SwingTwist = 6,
        Pulley = 7,
        Spring = 8,
        SixDof = 9,
        /* Jolt (Full3D) only. Gear and RackAndPinion read two existing joints. Path is reserved and unsupported. */
        Gear = 13,
        RackAndPinion = 14,
        Path = 15,

        /* Plane2D (Box2D) only. Wheel: chassis (A) + wheel (B) on a suspension axis. Mouse: drag B toward a target.
           Rope: distance joint that only limits the maximum length. */
        Wheel = 10,
        Mouse = 11,
        Rope = 12,
    }
}
