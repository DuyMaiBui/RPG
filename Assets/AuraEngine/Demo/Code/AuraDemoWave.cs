using System;

namespace AuraEngine.Demo
{
    /* Periodic waveforms in -1..1 for demo drivers. Plain C#, no engine references. */
    public static class AuraDemoWave
    {
        public static float Evaluate(AuraDemoWaveKind kind, float time, float frequencyHz)
        {
            if (kind == AuraDemoWaveKind.Constant)
                return 1f;

            var cycles = (double)time * frequencyHz;
            var phase = (float)(cycles - Math.Floor(cycles));
            switch (kind)
            {
                case AuraDemoWaveKind.Sine:
                    return (float)Math.Sin(phase * 2.0 * Math.PI);
                case AuraDemoWaveKind.Square:
                    return phase < 0.5f ? 1f : -1f;
                case AuraDemoWaveKind.Triangle:
                    return phase < 0.5f ? -1f + 4f * phase : 3f - 4f * phase;
                default:
                    return 0f;
            }
        }
    }
}
