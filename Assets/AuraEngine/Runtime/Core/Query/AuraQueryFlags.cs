using System;

namespace AuraEngine.Core
{
    [Flags]
    public enum AuraQueryFlags
    {
        None = 0,
        ClosestHit = 1 << 0,
        AllHits = 1 << 1,
        IgnoreSelf = 1 << 2,
        Backface = 1 << 3,
    }
}
