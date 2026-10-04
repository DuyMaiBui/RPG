using System;
using System.Collections.Generic;

namespace AuraEngine.Serialization
{
    public sealed class AuraReplay
    {
        private readonly List<AuraReplayFrame> _frames;

        public AuraReplay(AuraSimulationSnapshot initialState, IEnumerable<AuraReplayFrame> frames)
        {
            InitialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
            _frames = new List<AuraReplayFrame>(frames ?? Array.Empty<AuraReplayFrame>());
        }

        public AuraSimulationSnapshot InitialState { get; }

        public IReadOnlyList<AuraReplayFrame> Frames => _frames;

        public int FrameCount => _frames.Count;

        public AuraReplayFrame this[int index] => _frames[index];
    }
}
