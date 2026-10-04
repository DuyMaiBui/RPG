using System;

namespace AuraEngine.Core
{
    /* Plays a sequence of timed segments for scripted smoke runs. The caller maps SegmentIndex (and Progress for
       interpolated tracks) to its own values. Zero length segments are skipped; a sequence without positive
       duration never advances. */
    public sealed class AuraScriptPlayer
    {
        private readonly float[] _durations;
        private readonly bool _loop;
        private float _elapsedInSegment;

        public AuraScriptPlayer(float[] durations, bool loop)
        {
            if (durations == null)
                throw new ArgumentNullException(nameof(durations));

            _durations = (float[])durations.Clone();
            _loop = loop;
            for (var index = 0; index < _durations.Length; index++)
            {
                if (float.IsNaN(_durations[index]) || _durations[index] < 0f)
                    _durations[index] = 0f;
            }

            SegmentIndex = FirstPlayable(0);
        }

        /* Current segment, or -1 when empty, finished (not looping) or every segment has zero length. */
        public int SegmentIndex { get; private set; }

        public bool IsFinished => SegmentIndex < 0;

        /* 0..1 position inside the current segment. */
        public float Progress =>
            SegmentIndex < 0 ? 0f : Math.Min(1f, _elapsedInSegment / _durations[SegmentIndex]);

        public int Advance(float deltaTime)
        {
            if (SegmentIndex < 0 || float.IsNaN(deltaTime) || deltaTime <= 0f)
                return SegmentIndex;

            _elapsedInSegment += deltaTime;
            while (SegmentIndex >= 0 && _elapsedInSegment >= _durations[SegmentIndex])
            {
                _elapsedInSegment -= _durations[SegmentIndex];
                var next = SegmentIndex + 1;
                if (next >= _durations.Length)
                    next = _loop ? 0 : -1;
                SegmentIndex = next < 0 ? -1 : FirstPlayable(next);
                if (SegmentIndex < 0)
                    _elapsedInSegment = 0f;
            }

            return SegmentIndex;
        }

        public void Restart()
        {
            _elapsedInSegment = 0f;
            SegmentIndex = FirstPlayable(0);
        }

        private int FirstPlayable(int start)
        {
            for (var step = 0; step < _durations.Length; step++)
            {
                var index = start + step;
                if (index >= _durations.Length)
                {
                    if (!_loop)
                        return -1;
                    index -= _durations.Length;
                }

                if (_durations[index] > 0f)
                    return index;
            }

            return -1;
        }
    }
}
