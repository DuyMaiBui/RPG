using System.Collections.Generic;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Actors
{
    public sealed class RpgSimulationApplication : ISimulationApplication<RpgSimulationState>
    {
        void ISimulationApplication<RpgSimulationState>.BeginTick(SimulationContext<RpgSimulationState> context, SimulationTick tick) => context.State.BeginTick();

        void ISimulationApplication<RpgSimulationState>.HandleCommand(SimulationContext<RpgSimulationState> context, SessionContext session, in ClientCommandEnvelope command)
        {
            if (command.Payload is not AttackCommand attack ||
                !context.State.PlayerActors.TryGetValue(session.Player, out var attackerId) ||
                !context.State.Actors.TryGet(attackerId, out var attacker) ||
                !context.State.Actors.TryGet(attack.Target, out var target) ||
                attacker.IsDead || target.IsDead || attacker.Id == target.Id)
                return;

            var damage = target.ReceiveDamage(attacker.AttackPower);
            if (damage == 0) return;

            context.Publish(new ActorDamaged(attacker.Id, target.Id, damage));
            if (!target.IsDead) return;

            context.Publish(new ActorDied(attacker.Id, target.Id));
            context.Defer(state => state.Actors.Destroy(target.Id));
        }

        void ISimulationApplication<RpgSimulationState>.Tick(SimulationContext<RpgSimulationState> context, SimulationTick tick) { }

        void ISimulationApplication<RpgSimulationState>.HandleEvents(SimulationContext<RpgSimulationState> context, IReadOnlyList<ISimulationEvent> events)
        {
            foreach (var simulationEvent in events)
            {
                switch (simulationEvent)
                {
                    case ActorDamaged damaged:
                        context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.Damaged, damaged.Target, damaged.Source, damaged.Damage));
                        break;
                    case ActorDied died:
                        context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.Died, died.Target, died.Source, 0));
                        break;
                }
            }
        }

        ISimulationUpdate ISimulationApplication<RpgSimulationState>.CreateUpdate(SimulationContext<RpgSimulationState> context, SimulationTick tick)
            => new WorldFrameUpdate(context.State.Actors.CreateSnapshot(), context.State.Signals.ToArray());
    }
}
