namespace AuraEngine.Demo
{
    public enum AuraDemoKickMode
    {
        /* AddImpulse / AddAngularImpulse: the velocity change depends on the body mass. */
        Impulse = 0,

        /* Adds to the current velocity, independent of the body mass. */
        VelocityChange = 1,
    }
}
