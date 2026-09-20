using System;
using System.Collections.Generic;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class SimulationContext<TState>
    {
        private readonly List<ISimulationEvent> _events = new();
        private readonly List<Action<TState>> _deferredActions = new();

        internal SimulationContext(TState state, float fixedDeltaTime)
        {
            State = state;
            FixedDeltaTime = fixedDeltaTime;
        }

        public TState State { get; }
        public float FixedDeltaTime { get; }

        public void Publish(ISimulationEvent simulationEvent)
        {
            if (simulationEvent == null) throw new ArgumentNullException(nameof(simulationEvent));
            _events.Add(simulationEvent);
        }

        public void Defer(Action<TState> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _deferredActions.Add(action);
        }

        internal IReadOnlyList<ISimulationEvent> DrainEvents() => _events;

        internal void ResetForNextTick()
        {
            _events.Clear();
            _deferredActions.Clear();
        }

        internal void CommitDeferredActions()
        {
            foreach (var action in _deferredActions)
                action(State);

            _deferredActions.Clear();
        }
    }
}
