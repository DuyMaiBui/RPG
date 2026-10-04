using System;

namespace AuraEngine.Demo
{
    /* Wind gust envelope in 0..1: calm at time 0, one peak per period. sharpness 1 is a smooth raised cosine, larger
       values give short strong gusts with long lulls. Plain C#, no engine references. */
    public static class AuraDemoGustCurve
    {
        public static float Evaluate(float time, float periodSeconds, float sharpness)
        {
            if (!(periodSeconds > 0f))
                return 0f;

            var cycles = (double)time / periodSeconds;
            var phase = cycles - Math.Floor(cycles);
            var smooth = 0.5 - 0.5 * Math.Cos(phase * 2.0 * Math.PI);
            return (float)Math.Pow(smooth, Math.Max(0.1f, sharpness));
        }
    }
}
