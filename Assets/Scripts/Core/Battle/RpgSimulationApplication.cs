using System.Collections.Generic;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Actors
{
    public sealed class RpgSimulationApplication : ISimulationApplication<RpgSimulationState>
    {
        private readonly AutoBattleSystem _autoBattle = new();
        private readonly StatusEffectSystem _statusEffects = new();
        private readonly AbilitySystem _abilities = new();

        void ISimulationApplication<RpgSimulationState>.BeginTick(SimulationContext<RpgSimulationState> context, SimulationTick tick) => context.State.BeginTick();

        void ISimulationApplication<RpgSimulationState>.HandleCommand(
            SimulationContext<RpgSimulationState> context,
            SessionContext session,
            in ClientCommandEnvelope command)
        {
            if (command.Payload is not MoveIntentCommand move ||
                !context.State.PlayerActors.TryGetValue(session.Player, out var actorId) ||
                !context.State.Actors.TryGet(actorId, out var actor) ||
                !actor.Components.TryGet<ManualMovementComponent>(out var manualMovement))
                return;

            manualMovement.Direction = move.Direction.Normalized();
        }

        void ISimulationApplication<RpgSimulationState>.Tick(SimulationContext<RpgSimulationState> context, SimulationTick tick)
        {
            _statusEffects.Tick(context);
            _autoBattle.Tick(context);
            _abilities.Tick(context);
        }

        void ISimulationApplication<RpgSimulationState>.HandleEvents(
            SimulationContext<RpgSimulationState> context,
            IReadOnlyList<ISimulationEvent> events)
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

        ISimulationUpdate ISimulationApplication<RpgSimulationState>.CreateUpdate(
            SimulationContext<RpgSimulationState> context,
            SimulationTick tick)
            => new WorldFrameUpdate(
                context.State.Actors.CreateSnapshot(),
                context.State.Projectiles.CreateSnapshot(),
                context.State.Signals.ToArray(),
                0,
                (int)tick.Value,
                EntityId.None,
                context.State.Actors.GetBattleResult());
    }
}
