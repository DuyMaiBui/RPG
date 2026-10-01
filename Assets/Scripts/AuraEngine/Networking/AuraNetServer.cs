using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Networking
{
    /* Authoritative server: receives input frames, applies them to the canonical
       world and broadcasts the resulting state snapshot with an acknowledged
       input sequence. */
    public sealed class AuraNetServer
    {
        private readonly AuraSimulationWorld _world;
        private readonly IAuraTransport _transport;
        private readonly Dictionary<uint, uint> _lastAcknowledged = new Dictionary<uint, uint>();
        private readonly int _tickRate;
        private uint _tick;

        public AuraNetServer(AuraSimulationWorld world, IAuraTransport transport, int tickRate = 60)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _tickRate = Math.Max(1, tickRate);
        }

        public SimulationTick CurrentTick => _world.CurrentTick;

        public void Pump()
        {
            while (_transport.TryReceive(out var payload))
            {
                if (AuraNetCodec.TryDecodeInput(payload, out var command))
                {
                    _world.EnqueueCommand(command);
                    _lastAcknowledged[command.ClientId] = command.Sequence;
                }
            }
        }

        public void Step()
        {
            var step = new SimulationStep(new SimulationTick(_tick++), 1f / _tickRate);
            _world.Step(step);
            _transport.Send(AuraNetCodec.EncodeSnapshot(step.Tick.Value, _world.SaveState()));
        }

        public uint LastAcknowledged(uint clientId) =>
            _lastAcknowledged.TryGetValue(clientId, out var sequence) ? sequence : 0u;
    }
}
