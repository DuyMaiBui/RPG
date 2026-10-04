using System;

namespace AuraEngine.Simulation
{
    /* Turns wall-clock time into a whole number of fixed simulation steps under a time scale, so slow motion,
       hit-stop and pause never change the kernel's fixed delta time (the step size stays the caller's
       stepDelta; only the number of steps per wall tick varies).

       Semantics: progress (in steps) += wallDelta / stepDelta * Scale. Every whole unit of progress runs one step,
       the fraction carries over. Scale 0.5 therefore runs a step every second wall tick, Scale 0 runs none and
       Scale 2 runs two per tick. At most MaxStepsPerAdvance steps run per call; surplus whole steps are dropped
       (spiral-of-death guard) while the fraction is kept. Progress is a double and the whole-step test has a small
       tolerance so exact ratios (0.5, 0.25, 2) never lose a step to rounding, which keeps the sequence
       deterministic for a given sequence of (wallDelta, Scale) inputs. */
    public sealed class AuraTimeStepper
    {
        private const double Tolerance = 1e-6;

        private double _progress;
        private float _scale = 1f;
        private int _maxStepsPerAdvance = 16;

        public float Scale
        {
            get => _scale;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    throw new ArgumentOutOfRangeException(nameof(value), "Time scale must be finite and non-negative.");

                _scale = value;
            }
        }

        public int MaxStepsPerAdvance
        {
            get => _maxStepsPerAdvance;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _maxStepsPerAdvance = value;
            }
        }

        /* Fractional progress toward the next step, in [0, 1). */
        public double Progress => _progress;

        public void Reset() => _progress = 0d;

        public int Advance(float wallDeltaTime, float stepDeltaTime)
        {
            if (!(stepDeltaTime > 0f))
                throw new ArgumentOutOfRangeException(nameof(stepDeltaTime));

            if (!(wallDeltaTime > 0f) || _scale <= 0f)
                return 0;

            _progress += (double)wallDeltaTime / stepDeltaTime * _scale;
            var whole = (long)Math.Floor(_progress + Tolerance);
            if (whole <= 0)
                return 0;

            _progress -= whole;
            if (_progress < 0d)
                _progress = 0d;

            return (int)Math.Min(whole, _maxStepsPerAdvance);
        }
    }
}
