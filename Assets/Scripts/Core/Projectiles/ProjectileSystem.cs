using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Projectiles
{
    public sealed class ProjectileSystem
    {
        public void Tick(SimulationContext<RpgSimulationState> context)
        {
            var projectiles = context.State.Projectiles;
            for (var index = 0; index < projectiles.Count; index++)
            {
                if (!projectiles.TryGetAt(index, out var projectile)) continue;
                projectile.Tick(context.FixedDeltaTime);
                if (!context.State.Actors.TryGet(projectile.Target, out var target) ||
                    target.Components.Get<HealthComponent>().IsDead ||
                    projectile.RemainingLifetime <= 0f)
                {
                    projectiles.Destroy(projectile.Id);
                    continue;
                }

                var targetPosition = target.Components.Get<PositionComponent>().Position;
                var difference = targetPosition - projectile.Position;
                var distance = System.MathF.Sqrt(difference.LengthSquared);
                var travel = projectile.Speed * context.FixedDeltaTime;
                if (distance > travel + projectile.Radius + target.Components.Get<BodyComponent>().Radius)
                {
                    projectile.Position += difference.Normalized() * travel;
                    continue;
                }

                projectile.Position = targetPosition;
                var damage = target.Components.Get<HealthComponent>().ReceiveDamage(projectile.Damage);
                if (damage > 0)
                {
                    context.Publish(new ActorDamaged(projectile.Source, target.Id, damage));
                    if (target.Components.Get<HealthComponent>().IsDead)
                    {
                        target.Components.Get<AutoCombatStateComponent>().State = AutoCombatState.Dead;
                        context.Publish(new ActorDied(projectile.Source, target.Id));
                        context.Defer(state => state.Actors.Destroy(target.Id));
                    }
                }

                projectiles.Destroy(projectile.Id);
            }
        }
    }
}
