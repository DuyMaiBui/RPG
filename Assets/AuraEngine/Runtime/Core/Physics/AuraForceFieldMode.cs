namespace AuraEngine.Core
{
    public enum AuraForceFieldMode
    {
        /* Mass independent and scaled by the body gravity scale (gravity-like). Drag strength is 1/s. */
        Acceleration = 0,

        /* Divided by the body mass, ignores the gravity scale. Drag strength is N per m/s. */
        Force = 1,
    }
}
