using System;

namespace AuraEngine.Core
{
    /// <summary>
    /// Deterministic wind sampler for Verlet cloth and hair: a constant base wind plus a
    /// sinusoidal gust. Pure function of the explicit time argument (no clock, no RNG).
    /// </summary>
    public static class AuraVerletWind
    {
        public static AuraVector3 Sample(AuraVector3 baseWind, AuraVector3 gustAmplitude, float gustFrequencyHz, float timeSeconds) =>
            baseWind + gustAmplitude * MathF.Sin((2f * MathF.PI) * gustFrequencyHz * timeSeconds);
    }
}
