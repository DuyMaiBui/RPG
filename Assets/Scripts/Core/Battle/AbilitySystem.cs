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
                if (actor.Components.Get<HealthComponent>().IsDead) continue;

                for (var abilityIndex = 0; abilityIndex < abilities.Count; abilityIndex++)
                {
                    if (!abilities.IsReady(abilityIndex)) continue;
                    var ability = abilities.GetAt(abilityIndex);
                    if (!TryResolveTarget(context, actor, ability, out var target)) continue;

                    ApplyEffects(context, actor, target, ability);
                    context.Publish(new ActorAbilityCast(actor.Id, target.Id, ability.Id));
                    abilities.StartCooldown(abilityIndex);
                    break;
                }
            }
        }

        private static bool TryResolveTarget(
            SimulationContext<RpgSimulationState> context,
            Actor caster,
            AbilityDefinition ability,
            out Actor target)
        {
            if (ability.TargetMode == AbilityTargetMode.Self)
            {
                target = caster;
                return true;
            }

            target = null!;
            var targetId = caster.Components.Get<TargetComponent>().CurrentTarget;
            if (!context.State.Actors.TryGet(targetId, out var candidate)) return false;
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
            _ => StatusEffectType.Slow,
        };
    }
}
