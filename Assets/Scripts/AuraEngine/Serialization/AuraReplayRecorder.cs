using System;
using System.Collections.Generic;
using AuraEngine.Simulation;

namespace AuraEngine.Serialization
{
    public sealed class AuraReplayRecorder
    {
        private readonly List<AuraReplayFrame> _frames = new List<AuraReplayFrame>();
        private AuraSimulationSnapshot _initialState;

        public int FrameCount => _frames.Count;

        public void CaptureInitialState(AuraSimulationWorld world) =>
            _initialState = AuraSimulationSnapshot.Capture(world);

        public void Record(AuraSimulationWorld world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            _initialState ??= AuraSimulationSnapshot.Capture(world);
            _frames.Add(new AuraReplayFrame(world.CurrentTick, world.ComputeStateHash()));
        }

        public AuraReplay Build()
        {
            if (_initialState == null)
                throw new InvalidOperationException("No initial state was captured.");

            return new AuraReplay(_initialState, _frames);
        }

        public void Reset()
        {
            _frames.Clear();
            _initialState = null;
        }
    }
}
