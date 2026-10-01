using System;
using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Networking
{
    /* Client: submits local input to the server, predicts locally against the
       predicted world and reconciles when an authoritative snapshot arrives. */
    public sealed class AuraNetClient
    {
        private readonly uint _clientId;
        private readonly IAuraTransport _transport;
        private readonly AuraPredictedWorld _predicted;
        private readonly int _tickRate;
        private uint _sequence;
        private ulong _responseTimeSum;

        public AuraNetClient(uint clientId, IAuraTransport transport, AuraPredictedWorld predicted, int tickRate = 60)
        {
            _clientId = clientId;
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _predicted = predicted ?? throw new ArgumentNullException(nameof(predicted));
            _tickRate = Math.Max(1, tickRate);
        }

        public uint Sequence => _sequence;

        public int PendingInputCount => _predicted.PendingInputCount;

        public double AverageResponseTimeTicks => _corrections == 0 ? 0d : (double)_responseTimeSum / _corrections;

        private int _corrections;

        public void SubmitInput(int moveX, int moveY)
        {
            _sequence++;
            var command = new AuraInputCommand(_clientId, _sequence, moveX, moveY);
            _transport.Send(AuraNetCodec.EncodeInput(_clientId, _sequence, moveX, moveY));

            var step = new SimulationStep(new SimulationTick(_predicted.World.CurrentTick.Value + 1u), 1f / _tickRate);
            _predicted.Predict(command, step);
        }

        public bool Pump()
        {
            if (!_transport.TryReceive(out var payload))
                return false;

            if (!AuraNetCodec.TryDecodeSnapshot(payload, out var serverTick, out var worldState))
                return false;

            var before = _predicted.World.CurrentTick.Value;
            _predicted.Reconcile(serverTick, worldState, _sequence);
            _responseTimeSum += before >= serverTick ? before - serverTick : 0u;
            _corrections++;
            return true;
        }

    }
}
