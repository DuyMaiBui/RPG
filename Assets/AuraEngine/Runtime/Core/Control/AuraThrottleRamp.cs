using System;

namespace AuraEngine.Core
{
    /* Moves a control value toward a target at a bounded rate, so keyboard or scripted input becomes a smooth
       throttle. Deterministic and engine independent; the caller supplies the delta time. */
    public sealed class AuraThrottleRamp
    {
        public AuraThrottleRamp(float riseRate, float fallRate)
        {
            if (!(riseRate > 0f) || float.IsInfinity(riseRate))
                throw new ArgumentOutOfRangeException(nameof(riseRate), "Rise rate must be positive and finite.");
            if (!(fallRate > 0f) || float.IsInfinity(fallRate))
                throw new ArgumentOutOfRangeException(nameof(fallRate), "Fall rate must be positive and finite.");

            RiseRate = riseRate;
            FallRate = fallRate;
        }

        /* Units per second while the magnitude grows (or the sign flips), and while it shrinks toward zero. */
        public float RiseRate { get; }

        public float FallRate { get; }

        public float Value { get; private set; }

        public float Step(float target, float deltaTime)
        {
            if (float.IsNaN(target) || float.IsNaN(deltaTime) || deltaTime <= 0f)
                return Value;

            var shrinking = target == 0f || (Value != 0f && Math.Sign(target) == Math.Sign(Value) && Math.Abs(target) < Math.Abs(Value));
            var maxDelta = (shrinking ? FallRate : RiseRate) * deltaTime;
            var difference = target - Value;
            Value = Math.Abs(difference) <= maxDelta ? target : Value + Math.Sign(difference) * maxDelta;
            return Value;
        }

        public void Reset() => Value = 0f;
    }
}
