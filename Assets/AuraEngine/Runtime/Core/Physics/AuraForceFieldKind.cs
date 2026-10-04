namespace AuraEngine.Core
{
    public enum AuraForceFieldKind
    {
        /* Constant vector (acceleration or force). */
        Directional = 0,

        /* Toward the zone centre for positive strength, away for negative. */
        Radial = 1,

        /* Linear drag toward the wind velocity: water currents, wind tunnels, air resistance. */
        Drag = 2,
    }
}
