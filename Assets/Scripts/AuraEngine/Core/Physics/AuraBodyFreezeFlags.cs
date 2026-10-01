using System;

namespace AuraEngine.Core
{
    [Flags]
    public enum AuraBodyFreezeFlags
    {
        None = 0,
        PositionX = 1 << 0,
        PositionY = 1 << 1,
        PositionZ = 1 << 2,
        RotationX = 1 << 3,
        RotationY = 1 << 4,
        RotationZ = 1 << 5,

        Position = PositionX | PositionY | PositionZ,
        Rotation = RotationX | RotationY | RotationZ,
        All = Position | Rotation,
    }
}
