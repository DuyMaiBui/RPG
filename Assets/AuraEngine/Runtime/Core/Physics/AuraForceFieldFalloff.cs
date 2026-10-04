namespace AuraEngine.Core
{
    /* Radial distance falloff. */
    public enum AuraForceFieldFalloff
    {
        /* Constant strength. */
        None = 0,

        /* strength * (1 - d / reach), reach = MaxRadius or the zone extent. */
        Linear = 1,

        /* strength / d^2 (strength is the magnitude at distance 1), d clamped to MinRadius. */
        InverseSquare = 2,
    }
}
