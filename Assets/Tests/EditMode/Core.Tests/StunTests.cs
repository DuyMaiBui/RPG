using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    public sealed class StunTests
    {
        private static SimulationVector2 V(float x, float y) => new SimulationVector2(x, y);

        private static Actor Spawn(RpgSimulationState state, FactionId faction, float x, float speed = 2f)
        {
            var id = state.Actors.Spawn(ActorKind.Monster, faction, new ActorSpawnData(
                10, 1, V(x, 0f), 0.3f, speed, 6f, 0.5f, 1f));
            state.Actors.TryGet(id, out var actor);
            return actor;
        }

        [Test]
        public void ActorStatus_ReportsDisableWhileStunned()
        {
            var state = new RpgSimulationState();
            var actor = Spawn(state, FactionId.Red, 0f);

            Assert.That(ActorStatus.IsDisabled(actor), Is.False);

            actor.Components.Get<StatusEffectComponent>().Apply(StatusEffectType.Stun, 1, 3);

            Assert.That(ActorStatus.IsDisabled(actor), Is.True);
        }

        [Test]
        public void AutoBattle_StunnedActorDoesNotMove()
        {
            var state = new RpgSimulationState();
            var red = Spawn(state, FactionId.Red, -2f);
            Spawn(state, FactionId.Blue, 2f);
            red.Components.Get<StatusEffectComponent>().Apply(StatusEffectType.Stun, 1, 5);

            new AutoBattleSystem().Tick(new SimulationContext<RpgSimulationState>(state, 1f / 30f));

            Assert.That(red.Components.Get<PositionComponent>().Position.X, Is.EqualTo(-2f).Within(0.0001f));
        }

        [Test]
        public void AbilitySystem_StunnedCasterDoesNotCast()
        {
            var state = new RpgSimulationState();
            var strikerId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, V(0f, 0f), 0.3f, 2f, 6f, 1f, 1f,
                abilities: new[]
                {
                    new AbilityDefinition(1, 2, 3f, AbilityTargetMode.CurrentTarget,
                        new[] { new AbilityEffect(AbilityEffectType.Damage, 4) }),
                }));
            var targetId = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, V(1f, 0f), 0.3f, 2f, 6f, 0.5f, 1f));
            state.Actors.TryGet(strikerId, out var striker);
            state.Actors.TryGet(targetId, out var target);
            striker.Components.Get<TargetComponent>().CurrentTarget = targetId;
            striker.Components.Get<StatusEffectComponent>().Apply(StatusEffectType.Stun, 1, 3);

            new AbilitySystem().Tick(new SimulationContext<RpgSimulationState>(state, 1f / 30f));

            Assert.That(target.Components.Get<HealthComponent>().CurrentHealth, Is.EqualTo(10));
        }
    }
}
