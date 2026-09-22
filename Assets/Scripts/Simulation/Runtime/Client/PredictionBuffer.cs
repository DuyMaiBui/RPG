using System;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class PredictionBuffer
    {
        private readonly PredictedMoveInput[] _inputs;
        private int _count;
        private int _head;

        public PredictionBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _inputs = new PredictedMoveInput[capacity];
        }

        public int Count => _count;

        public void Record(PredictedMoveInput input)
        {
            var index = (_head + _count) % _inputs.Length;
            if (_count == _inputs.Length)
            {
                _inputs[_head] = input;
                _head = (_head + 1) % _inputs.Length;
                return;
            }

            _inputs[index] = input;
            _count++;
        }

        public SimulationVector2 Reconcile(
            SimulationVector2 authoritativePosition,
            ClientSequence acknowledgedSequence,
            float moveSpeed,
            float fixedDeltaTime)
        {
            while (_count > 0 && _inputs[_head].Sequence.Value <= acknowledgedSequence.Value)
            {
                _head = (_head + 1) % _inputs.Length;
                _count--;
            }

            var position = authoritativePosition;
            for (var index = 0; index < _count; index++)
            {
                var input = _inputs[(_head + index) % _inputs.Length];
                position += input.Direction.Normalized() * (moveSpeed * fixedDeltaTime);
            }

            return position;
        }

        public void Clear()
        {
            _count = 0;
            _head = 0;
        }
    }
}
