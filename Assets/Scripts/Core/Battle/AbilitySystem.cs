using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Actors
{
    /// <summary>Auto-casts actor abilities once per simulation tick. It runs after the auto battle so targets are
    /// current. A <see cref="AbilityTargetMode.CurrentTarget"/> ability needs a live enemy in range with a clear line
    /// of sight; a <see cref="AbilityTargetMode.Self"/> ability casts whenever it is ready. Casting is deterministic:
    /// actors are visited in slot order and, per actor, the first ready ability wins (at most one cast per tick).</summary>
    public sealed class AbilitySystem
    {
        public void Tick(SimulationContext<RpgSimulationState> context)
        {
            var actors = context.State.Actors;
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor)) continue;
                if (!actor.Components.TryGet<AbilityComponent>(out var abilities) || abilities.Count == 0) continue;

                abilities.Tick();
                if (ActorStatus.IsDisabled(actor)) continue;
                if (actor.Components.Get<HealthComponent>().IsDead) continue;

                if (TryResolveOrderedCast(context, actor, abilities)) continue;

                for (var abilityIndex = 0; abilityIndex < abilities.Count; abilityIndex++)
                {
                    if (!abilities.IsReady(abilityIndex)) continue;
                    if (TryBeginCast(context, actor, abilityIndex, actor.Components.Get<TargetComponent>().CurrentTarget))
                        break;
                }
            }
        }

        /// <summary>Attempts one cast: applies the ability's effects and starts its cooldown. Returns false when the
        /// caster is dead or disabled, the ability is on cooldown, or the target is missing, out of range or out of
        /// sight. Callers retry on later ticks, so a blocked cast is a wait rather than a failure.</summary>
        public bool TryBeginCast(
            SimulationContext<RpgSimulationState> context,
            Actor caster,
            int abilityIndex,
            EntityId targetId)
        {
            if (ActorStatus.IsDisabled(caster)) return false;
            if (caster.Components.Get<HealthComponent>().IsDead) return false;
            if (!caster.Components.TryGet<AbilityComponent>(out var abilities)) return false;
            if (abilityIndex < 0 || abilityIndex >= abilities.Count) return false;
            if (!abilities.IsReady(abilityIndex)) return false;

            var ability = abilities.GetAt(abilityIndex);
            if (!TryResolveTarget(context, caster, ability, targetId, out var target)) return false;

            ApplyEffects(context, caster, target, ability);
            context.Publish(new ActorAbilityCast(caster.Id, target.Id, ability.Id));
            abilities.StartCooldown(abilityIndex);
            return true;
        }

        /// <summary>Handles a <see cref="OrderKind.CastAbility"/> order. Returns true when the order owns this actor's
        /// cast for the tick — whether it landed or is still waiting for its cooldown, range or target — so an ordered
        /// ability is never silently replaced by an automatic one. An order naming an ability the actor does not have
        /// is dropped, and the actor falls back to choosing for itself.</summary>
        private bool TryResolveOrderedCast(
            SimulationContext<RpgSimulationState> context,
            Actor actor,
            AbilityComponent abilities)
        {
            if (!actor.Components.TryGet<OrderQueueComponent>(out var orders) || !orders.HasOrder)
                return false;
            if (orders.Current.Kind != OrderKind.CastAbility)
                return false;

            var abilityIndex = abilities.IndexOf(orders.Current.AbilityId);
            if (abilityIndex < 0)
            {
                orders.Advance();
                return false;
            }

            if (TryBeginCast(context, actor, abilityIndex, orders.Current.Target))
                orders.Advance();

            return true;
        }

        private static bool TryResolveTarget(
            SimulationContext<RpgSimulationState> context,
            Actor caster,
            AbilityDefinition ability,
            EntityId requestedTarget,
            out Actor target)
        {
            if (ability.TargetMode == AbilityTargetMode.Self)
            {
                target = caster;
                return true;
            }

            target = null!;
            if (!context.State.Actors.TryGet(requestedTarget, out var candidate)) return false;
            if (candidate.Id == caster.Id) return false;
            if (candidate.Components.Get<HealthComponent>().IsDead) return false;

            var casterPosition = caster.Components.Get<PositionComponent>().Position;
            var targetPosition = candidate.Components.Get<PositionComponent>().Position;
            var difference = targetPosition - casterPosition;
            var range = ability.Range +
                        caster.Components.Get<BodyComponent>().Radius +
                        candidate.Components.Get<BodyComponent>().Radius;
            if (difference.LengthSquared > range * range) return false;

            if (!context.State.Navigation.HasLineOfSight(casterPosition, targetPosition, 0f)) return false;

            target = candidate;
            return true;
        }

        private static void ApplyEffects(
            SimulationContext<RpgSimulationState> context,
            Actor source,
            Actor target,
            AbilityDefinition ability)
        {
            var health = target.Components.Get<HealthComponent>();
            for (var index = 0; index < ability.Effects.Length; index++)
            {
                var effect = ability.Effects[index];
                if (health.IsDead) return;

                switch (effect.Type)
                {
                    case AbilityEffectType.Damage:
                    {
                        var applied = health.ReceiveDamage(effect.Magnitude);
                        if (applied <= 0) break;
                        context.Publish(new ActorDamaged(source.Id, target.Id, applied));
                        if (!health.IsDead) break;
                        target.Components.Get<AutoCombatStateComponent>().State = AutoCombatState.Dead;
                        context.Publish(new ActorDied(source.Id, target.Id));
                        context.Defer(state => state.Actors.Destroy(target.Id));
                        return;
                    }

                    case AbilityEffectType.Heal:
                        health.ReceiveHealing(effect.Magnitude);
                        break;

                    default:
                        if (effect.Magnitude <= 0 || effect.DurationTicks <= 0) break;
                        target.Components.Get<StatusEffectComponent>().Apply(
                            ToStatusEffect(effect.Type),
                            effect.Magnitude,
                            effect.DurationTicks);
                        break;
                }
            }
        }

        private static StatusEffectType ToStatusEffect(AbilityEffectType type) => type switch
        {
            AbilityEffectType.Poison => StatusEffectType.Poison,
            AbilityEffectType.Regeneration => StatusEffectType.Regeneration,
            AbilityEffectType.Stun => StatusEffectType.Stun,
            _ => StatusEffectType.Slow,
        };
    }
}
