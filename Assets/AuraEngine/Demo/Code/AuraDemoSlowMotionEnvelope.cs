using System;

namespace AuraEngine.Demo
{
    /* Time scale multiplier for a slow-motion pulse: holds at the pulse scale, then eases linearly back to 1.
       Advance takes real (unscaled) seconds. A new trigger while active keeps the lower scale and restarts the hold.
       Plain C#, no engine references. */
    public sealed class AuraDemoSlowMotionEnvelope
    {
        private float _scale = 1f;
        private float _hold;
        private float _recover;
        private float _elapsed;
        private bool _active;

        public bool IsActive => _active;

        public void Trigger(float scale, float holdSeconds, float recoverSeconds)
        {
            var clamped = Math.Min(1f, Math.Max(0f, scale));
            _scale = _active ? Math.Min(_scale, clamped) : clamped;
            _hold = Math.Max(0f, holdSeconds);
            _recover = Math.Max(0f, recoverSeconds);
            _elapsed = 0f;
            _active = true;
        }

        public float Advance(float realDeltaTime)
        {
            if (!_active)
                return 1f;

            _elapsed += Math.Max(0f, realDeltaTime);
            if (_elapsed <= _hold)
                return _scale;

            if (_recover <= 0f || _elapsed >= _hold + _recover)
            {
                _active = false;
                return 1f;
            }

            var t = (_elapsed - _hold) / _recover;
            return _scale + (1f - _scale) * t;
        }
    }
}
