using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal sealed class SoakBody
    {
        public NativeBodyHandle Handle;
        public SoakHandleState State = SoakHandleState.Live;
        public int Type;

        /* Built from a triangle mesh, height field or plane shape. */
        public bool StaticOnly;

        /* Removed from the simulation with Aura_SetBodyEnabled(false). */
        public bool Disabled;

        public string Description = string.Empty;
    }
}
