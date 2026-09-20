using System.Collections.Generic;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Actors
{
    public sealed class RpgSimulationApplication : ISimulationApplication<RpgSimulationState>
    {
        private readonly AnyAliveEnemyTargetSelector _targetSelector = new();

        void ISimulationApplication<RpgSimulationState>.BeginTick(SimulationContext<RpgSimulationState> context, SimulationTick tick) => context.State.BeginTick();

        void ISimulationApplication<RpgSimulationState>.HandleCommand(SimulationContext<RpgSimulationState> context, SessionContext session, in ClientCommandEnvelope command)
        {
            if (command.Payload is not AttackCommand attack ||
                !context.State.PlayerActors.TryGetValue(session.Player, out var attackerId) ||
                !context.State.Actors.TryGet(attackerId, out var attacker) ||
                attacker.IsDead ||
                (context.State.Turns.IsInitialized && context.State.Turns.ActiveActorId != attacker.Id) ||
                !TryResolveTarget(context.State, attacker, attack.Target, out var target))
                return;

            context.Publish(new ActorAttackStarted(attacker.Id, target.Id));
            var damage = target.ReceiveDamage(attacker.AttackPower);
            if (damage == 0)
            {
                if (context.State.Turns.IsInitialized)
                    context.Defer(state => state.Turns.Advance(state.Actors));
                return;
            }

            context.Publish(new ActorDamaged(attacker.Id, target.Id, damage));
            if (!target.IsDead)
            {
                if (context.State.Turns.IsInitialized)
                    context.Defer(state => state.Turns.Advance(state.Actors));
                return;
            }

            context.Publish(new ActorDied(attacker.Id, target.Id));
            context.Defer(state =>
            {
                state.Actors.Destroy(target.Id);
                if (state.Turns.IsInitialized)
                    state.Turns.Advance(state.Actors);
            });
        }

        void ISimulationApplication<RpgSimulationState>.Tick(SimulationContext<RpgSimulationState> context, SimulationTick tick) { }

        void ISimulationApplication<RpgSimulationState>.HandleEvents(SimulationContext<RpgSimulationState> context, IReadOnlyList<ISimulationEvent> events)
        {
            foreach (var simulationEvent in events)
            {
                switch (simulationEvent)
                {
                    case ActorAttackStarted attack:
                        context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.AttackStarted, attack.Source, attack.Target, 0));
                        break;
                    case ActorDamaged damaged when damaged.Damage > 0:
                        context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.Damaged, damaged.Target, damaged.Source, damaged.Damage));
                        break;
                    case ActorDied died:
                        context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.Died, died.Target, died.Source, 0));
                        break;
                }
            }
        }

        ISimulationUpdate ISimulationApplication<RpgSimulationState>.CreateUpdate(SimulationContext<RpgSimulationState> context, SimulationTick tick)
            => new WorldFrameUpdate(
                context.State.Actors.CreateSnapshot(),
                context.State.Signals.ToArray(),
                context.State.Turns.Round,
                context.State.Turns.TurnNumber,
                context.State.Turns.ActiveActorId,
                context.State.Turns.GetResult(context.State.Actors));

        private bool TryResolveTarget(RpgSimulationState state, Actor attacker, EntityId requestedTarget, out Actor target)
        {
            if (state.Actors.TryGet(requestedTarget, out target) &&
                !target.IsDead &&
                target.Faction != attacker.Faction &&
                target.Id != attacker.Id)
                return true;

            if (_targetSelector.TrySelect(attacker, state.Actors, out var replacementId))
                return state.Actors.TryGet(replacementId, out target);

            target = null;
            return false;
        }
    }
}
