using System;

namespace AuraEngine.Demo
{
    /* Fires once after startDelay and then, when repeating, every interval seconds. Advance reports at most one fire
       per call. Plain C#, no engine references. */
    public sealed class AuraDemoIntervalTimer
    {
        private readonly float _interval;
        private readonly bool _repeat;
        private float _remaining;
        private bool _finished;

        public AuraDemoIntervalTimer(float startDelay, float interval, bool repeat)
        {
            _remaining = Math.Max(0f, startDelay);
            _interval = Math.Max(0.01f, interval);
            _repeat = repeat;
        }

        public bool Advance(float deltaTime)
        {
            if (_finished || !(deltaTime > 0f))
                return false;

            _remaining -= deltaTime;
            if (_remaining > 0f)
                return false;

            if (_repeat)
                _remaining += _interval;
            else
                _finished = true;

            if (_remaining < 0f)
                _remaining = 0f;

            return true;
        }
    }
}
