using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Actors
{
    /// <summary>Applies timed status effects once per simulation tick. It runs before the auto battle so a movement
    /// modifier affects this tick's movement. Order per actor: periodic damage/healing, then movement modifiers, then
    /// durations advance. Poison damage is published as a normal damage event so presenters see it.</summary>
    public sealed class StatusEffectSystem
    {
        public void Tick(SimulationContext<RpgSimulationState> context)
        {
            var actors = context.State.Actors;
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor)) continue;
                if (!actor.Components.TryGet<StatusEffectComponent>(out var effects)) continue;

                // Recompute every tick so the multiplier returns to 1 when the last Slow expires.
                ApplyMovementModifiers(actor, effects);
                if (effects.Count == 0) continue;

                if (!ApplyPeriodic(context, actor, effects))
                    continue;

                effects.Advance();
            }
        }

        /// <summary>Applies poison and regeneration. Returns false when the actor died and was scheduled for removal.
        /// </summary>
        private static bool ApplyPeriodic(
            SimulationContext<RpgSimulationState> context,
            Actor actor,
            StatusEffectComponent effects)
        {
            var health = actor.Components.Get<HealthComponent>();
            if (health.IsDead) return true;

            for (var index = 0; index < effects.Count; index++)
            {
                var effect = effects.GetAt(index);
                if (!StatusEffectRules.IsPeriodic(effect.Type)) continue;
                var amount = effect.Magnitude * effect.Stacks;

                if (effect.Type == StatusEffectType.Regeneration)
                {
                    health.ReceiveHealing(amount);
                    continue;
                }

                var applied = health.ReceiveDamage(amount);
                if (applied <= 0) continue;

                context.Publish(new ActorDamaged(EntityId.None, actor.Id, applied));
                if (!health.IsDead) continue;

                actor.Components.Get<AutoCombatStateComponent>().State = AutoCombatState.Dead;
                context.Publish(new ActorDied(EntityId.None, actor.Id));
                context.Defer(state => state.Actors.Destroy(actor.Id));
                return false;
            }

            return true;
        }

        private static void ApplyMovementModifiers(Actor actor, StatusEffectComponent effects)
        {
            var movement = actor.Components.Get<MovementComponent>();
            var multiplier = 1f;
            for (var index = 0; index < effects.Count; index++)
            {
                var effect = effects.GetAt(index);
                if (!StatusEffectRules.IsMovementModifier(effect.Type)) continue;
                multiplier *= 1f - effect.Magnitude * effect.Stacks / 100f;
            }

            movement.SpeedMultiplier = SimulationMath.Max(0f, multiplier);
        }
    }
}
