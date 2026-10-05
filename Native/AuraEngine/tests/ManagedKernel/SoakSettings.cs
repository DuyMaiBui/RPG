namespace AuraEngine.KernelTests
{
    /* Process-wide soak knobs, written once before the first episode. */
    internal static class SoakSettings
    {
        /* Probability that a generated float is non-finite or enormous (input the kernel must reject). 0 keeps every
           generated argument finite and well formed so the run hunts crashes and memory errors, not validation gaps. */
        public static float EvilRate = 0f;

        /* True with probability p, only in hostile runs (EvilRate > 0): malformed finite input such as a zero axis,
           an unnormalized quaternion, an out-of-range enum or an out-of-range mesh index. */
        public static bool Bad(SoakRng rng, float probability) => EvilRate > 0f && rng.Chance(probability);

        public static int OpsPerEpisode = 1500;

        /* Full audit of a world every N operations (1 pins a corruption to the exact operation). */
        public static int AuditEvery = 100;

        /* AURA_SOAK_TRACE=1: keep every audited body state as text so a replay divergence can be shown field by field. */
        public static bool Trace;

        /* AURA_SOAK_KNOWN=1 steers around defects already reported, so a run can look past them for others:
           Plane2D query buffers get slack (Box2D overlap capacity overrun), Step(dt = 0) becomes 0.001
           (Aura_SetKinematicTarget after it yields inf/NaN), Aura_SetKinematicTarget is not called on mesh/height
           field/plane bodies (null deref), Jolt joints are not created on disabled bodies (segfault in the next Step),
           Box2D distance, rope and slider joints and Box2D joints between bodies more than 4 m apart are not created
           (explosion / NaN),
           the largest Step dt is 0.1 instead of 0.25 (a Box2D spring joint exploded at 0.25 s), and control-call velocities stay at 12 or below (a 200 m/s body on a Box2D distance joint exploded). */
        public static bool TolerateKnown;

        public static long KnownOverruns;

        /* AURA_SOAK_CONTINUE=1: a non-finite or exploded body state is recorded as a finding and the world is excluded
           from further state checks instead of ending the run. */
        public static bool ContinueOnCorruption;

        public static readonly System.Collections.Generic.List<string> Findings = new System.Collections.Generic.List<string>();

        private static readonly System.Collections.Generic.HashSet<string> FindingKeys = new System.Collections.Generic.HashSet<string>();

        public static void Record(string key, string text)
        {
            if (FindingKeys.Add(key))
                Findings.Add(text);
        }

        /* Calls that rejected a live but broken joint (HasJoint false, control INVALID_HANDLE). */
        public static long BrokenJointRejections;

        /* Seconds without progress before the watchdog reports a hang. */
        public static int HangSeconds = 120;
    }
}
