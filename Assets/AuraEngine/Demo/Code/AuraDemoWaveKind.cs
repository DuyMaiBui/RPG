namespace AuraEngine.Demo
{
    public enum AuraDemoWaveKind
    {
        /* Always +1: offset + amplitude. */
        Constant = 0,
        Sine = 1,
        /* +1 for the first half period, -1 for the second. */
        Square = 2,
        /* Linear ramp -1..1 and back. */
        Triangle = 3,
    }
}
