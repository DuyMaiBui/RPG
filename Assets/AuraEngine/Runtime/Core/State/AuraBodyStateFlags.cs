using System;

namespace AuraEngine.Core
{
    [Flags]
    public enum AuraBodyStateFlags : uint
    {
        None = 0u,

        /* The body is removed from the simulation (IPhysicsBodyControl.SetEnabled). */
        Disabled = 1u,
    }
}
