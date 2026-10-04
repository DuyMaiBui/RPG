using System;
using System.Collections.Generic;
using AuraEngine.Core;

namespace AuraEngine.Networking
{
    /* Client-side predicted copy of a world. While an input is unacknowledged
       the predicted world advances with the local input; when an authoritative
       snapshot arrives the world is restored and every unacknowledged input is
       re-applied so the local view converges on the server state. */
    public sealed class AuraPredictedWorld
    {
        private readonly AuraPredictionReplay _replay;
        private readonly List<AuraInputCommand> _pending = new List<AuraInputCommand>();

        public AuraPredictedWorld(AuraEngine.Simulation.AuraSimulationWorld world, AuraPredictionReplay replay)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            _replay = replay ?? throw new ArgumentNullException(nameof(replay));
        }

        public AuraEngine.Simulation.AuraSimulationWorld World { get; }

        public int PendingInputCount => _pending.Count;

        public void Predict(in AuraInputCommand command, in SimulationStep step)
        {
            _pending.Add(command);
            _replay.Apply(World, command);
            World.Step(step);
        }

        public void Reconcile(uint serverTick, byte[] serverState, uint acknowledgedSequence)
        {
            World.RestoreState(serverState);

            for (var index = _pending.Count - 1; index >= 0; index--)
            {
                if (_pending[index].Sequence <= acknowledgedSequence)
                    _pending.RemoveAt(index);
            }

            for (var index = 0; index < _pending.Count; index++)
                _replay.Apply(World, _pending[index]);
        }

        public byte[] SaveState() => World.SaveState();
    }
}
