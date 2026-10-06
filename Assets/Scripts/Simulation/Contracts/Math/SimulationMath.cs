using System;

namespace RPG.Simulation.Contracts
{
    /// <summary>Single entry point for the simulation's floating-point math. Simulation code calls this instead of
    /// <see cref="MathF"/> directly so the numeric backend can be replaced (for example by fixed-point for bit-exact
    /// cross-platform replay) in one place. It currently delegates to <see cref="MathF"/>.</summary>
    public static class SimulationMath
    {
        public const float PI = MathF.PI;

        public static float Sqrt(float value) => MathF.Sqrt(value);

        public static float Abs(float value) => MathF.Abs(value);

        public static float Min(float left, float right) => MathF.Min(left, right);

        public static float Max(float left, float right) => MathF.Max(left, right);

        public static float Floor(float value) => MathF.Floor(value);

        public static float Ceiling(float value) => MathF.Ceiling(value);

        public static float Sin(float radians) => MathF.Sin(radians);

        public static float Cos(float radians) => MathF.Cos(radians);
    }
}
