using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    public sealed class OrderResolutionTests
    {
        [Test]
        public void MoveOrder_WalksToTheDestinationAndCompletes()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(state, FactionId.Red, new SimulationVector2(0f, 0f));
            using var harness = Own(state, actorId);
            harness.Send(new MoveOrderCommand(actorId, new SimulationVector2(3f, 0f)));
            harness.Send(new HoldOrderCommand(actorId, queued: true));

            Assert.That(RunUntilHeld(harness, state, actorId), Is.True, "the move order never completed");

            var position = PositionOf(state, actorId);
            Assert.That(position.X, Is.EqualTo(3f).Within(OrderSystem.ArrivalRadius + 0.05f));
            Assert.That(position.Y, Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void MoveOrder_IgnoresAnEnemyItWalksPast()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(state, FactionId.Red, new SimulationVector2(0f, 0f));
            var enemyId = Spawn(state, FactionId.Blue, new SimulationVector2(2f, 0.6f), moveSpeed: 0f);
            using var harness = Own(state, actorId);
            harness.Send(new MoveOrderCommand(actorId, new SimulationVector2(5f, 0f)));
            harness.Send(new HoldOrderCommand(actorId, queued: true));

            Assert.That(RunUntilHeld(harness, state, actorId), Is.True, "the move order never completed");

            Assert.That(PositionOf(state, actorId).X, Is.EqualTo(5f).Within(OrderSystem.ArrivalRadius + 0.05f));
            Assert.That(HealthOf(state, enemyId), Is.EqualTo(10), "a move order must not engage");
            Assert.That(QueueOf(state, actorId).Current.Kind, Is.EqualTo(OrderKind.Hold));
        }

        [Test]
        public void MoveOrder_RoutesAroundAUnitStandingOnThePath()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(state, FactionId.Red, new SimulationVector2(0f, 0f));
            Spawn(state, FactionId.Blue, new SimulationVector2(2f, 0f), moveSpeed: 0f);
            using var harness = Own(state, actorId);
            harness.Send(new MoveOrderCommand(actorId, new SimulationVector2(5f, 0f)));
            harness.Send(new HoldOrderCommand(actorId, queued: true));

            Assert.That(RunUntilHeld(harness, state, actorId), Is.True, "a unit on the path blocked the move");

            Assert.That(PositionOf(state, actorId).X, Is.EqualTo(5f).Within(OrderSystem.ArrivalRadius + 0.05f));
        }

        [Test]
        public void AttackMoveOrder_EngagesAnEnemyInsteadOfWalkingPast()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(state, FactionId.Red, new SimulationVector2(0f, 0f), attackPower: 1, attackCooldown: 0.2f);
            var enemyId = Spawn(state, FactionId.Blue, new SimulationVector2(1f, 0f), maximumHealth: 200, moveSpeed: 0f);
            using var harness = Own(state, actorId);
            harness.Send(new AttackMoveOrderCommand(actorId, new SimulationVector2(5f, 0f)));

            harness.RunTicks(60);

            Assert.That(PositionOf(state, actorId).X, Is.LessThan(2.5f), "the actor should stop to fight");
            Assert.That(HealthOf(state, enemyId), Is.LessThan(200), "an attack move must engage");
        }

        [Test]
        public void HoldOrder_KeepsTheActorInPlace()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(state, FactionId.Red, new SimulationVector2(0f, 0f));
            var enemyId = Spawn(state, FactionId.Blue, new SimulationVector2(2f, 0f), moveSpeed: 0f);
            using var harness = Own(state, actorId);
            harness.Send(new HoldOrderCommand(actorId));
            Assert.That(harness.RunUntil(() => QueueOf(state, actorId).Current.Kind == OrderKind.Hold), Is.True);
            var held = PositionOf(state, actorId).X;

            harness.RunTicks(60);

            Assert.That(PositionOf(state, actorId).X, Is.EqualTo(held).Within(0.001f));
            Assert.That(HealthOf(state, enemyId), Is.EqualTo(10));
            Assert.That(QueueOf(state, actorId).Current.Kind, Is.EqualTo(OrderKind.Hold));
        }

        [Test]
        public void StopOrder_ReleasesAHeldActor()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(state, FactionId.Red, new SimulationVector2(0f, 0f), moveSpeed: 1f);
            Spawn(state, FactionId.Blue, new SimulationVector2(4f, 0f), moveSpeed: 0f);
            using var harness = Own(state, actorId);
            harness.Send(new HoldOrderCommand(actorId));
            harness.RunTicks(5);
            var held = PositionOf(state, actorId).X;

            harness.Send(new StopOrderCommand(actorId));
            harness.RunTicks(90);

            Assert.That(QueueOf(state, actorId).HasOrder, Is.False);
            Assert.That(PositionOf(state, actorId).X, Is.GreaterThan(held + 0.5f), "a stopped actor resumes on its own");
        }

        [Test]
        public void AttackTargetOrder_ClearsWhenTheTargetDies()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(state, FactionId.Red, new SimulationVector2(0f, 0f), attackPower: 5, attackCooldown: 0.2f);
            var enemyId = Spawn(state, FactionId.Blue, new SimulationVector2(1.5f, 0f), moveSpeed: 0f);
            using var harness = Own(state, actorId);
            harness.Send(new AttackOrderCommand(actorId, enemyId));

            Assert.That(RunUntilOrderGone(harness, state, actorId), Is.True, "the attack order never cleared");

            Assert.That(state.Actors.TryGet(enemyId, out _), Is.False, "the target should have died");
        }

        [Test]
        public void CastAbilityOrder_CastsTheOrderedAbilityNotTheAutomaticOne()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(
                state,
                FactionId.Red,
                new SimulationVector2(0f, 0f),
                attackCooldown: 100f,
                abilities: new[] { SelfHealAbility(1), DamageAbility(2, 5) });
            var enemyId = Spawn(state, FactionId.Blue, new SimulationVector2(1.5f, 0f), maximumHealth: 100, moveSpeed: 0f);
            using var harness = Own(state, actorId);
            harness.Send(new CastAbilityOrderCommand(actorId, 2, enemyId));

            Assert.That(harness.RunUntil(() => HealthOf(state, enemyId) < 100), Is.True, "the ordered ability never landed");

            Assert.That(HealthOf(state, enemyId), Is.EqualTo(95), "only the ordered ability should have landed");
            Assert.That(QueueOf(state, actorId).HasOrder, Is.False, "the order completes with its cast");
        }

        [Test]
        public void CastAbilityOrder_OwnsTheCastSlotWhileItWaitsForTheCooldown()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(
                state,
                FactionId.Red,
                new SimulationVector2(0f, 0f),
                attackCooldown: 100f,
                abilities: new[] { SelfHealAbility(1), DamageAbility(2, 5, cooldownTicks: 600) });
            var enemyId = Spawn(state, FactionId.Blue, new SimulationVector2(1.5f, 0f), maximumHealth: 100, moveSpeed: 0f);
            using var harness = Own(state, actorId);
            harness.Send(new CastAbilityOrderCommand(actorId, 2, enemyId));
            Assert.That(harness.RunUntil(() => HealthOf(state, enemyId) < 100), Is.True, "the first cast never landed");

            Assert.That(harness.Send(new CastAbilityOrderCommand(actorId, 2, enemyId)), Is.True, "the client refused the command");
            harness.RunTicks(1);

            var queue = QueueOf(state, actorId);
            Assert.That(
                queue.HasOrder,
                Is.True,
                $"the order should wait for the cooldown; cooldown={CooldownOf(state, actorId, 1)} enemyHealth={HealthOf(state, enemyId)}");
            Assert.That(queue.Current.Kind, Is.EqualTo(OrderKind.CastAbility));
            Assert.That(HealthOf(state, enemyId), Is.EqualTo(95), "a waiting order must not let another ability fire");
        }

        [Test]
        public void CastAbilityOrder_WalksIntoRangeBeforeCasting()
        {
            var state = new RpgSimulationState();
            var actorId = Spawn(
                state,
                FactionId.Red,
                new SimulationVector2(0f, 0f),
                visionRange: 8f,
                attackCooldown: 100f,
                abilities: new[] { SelfHealAbility(1), DamageAbility(2, 5) });
            var enemyId = Spawn(state, FactionId.Blue, new SimulationVector2(6f, 0f), maximumHealth: 100, moveSpeed: 0f);
            using var harness = Own(state, actorId);
            harness.Send(new CastAbilityOrderCommand(actorId, 2, enemyId));
            harness.Send(new HoldOrderCommand(actorId, queued: true));

            Assert.That(harness.RunUntil(() => HealthOf(state, enemyId) < 100), Is.True, "the cast order never landed");

            // Ability range 2 plus both body radii: the caster must walk from 6 to about 3.4, not cast from where it stood.
            Assert.That(PositionOf(state, actorId).X, Is.EqualTo(3.4f).Within(0.1f));
            Assert.That(HealthOf(state, enemyId), Is.EqualTo(95));
        }

        private static bool RunUntilOrderGone(SimulationTestHarness harness, RpgSimulationState state, EntityId actorId)
        {
            harness.RunTicks(1);
            Assert.That(QueueOf(state, actorId).HasOrder, Is.True, "the order was not applied");
            return harness.RunUntil(() => !QueueOf(state, actorId).HasOrder);
        }

        /// <summary>Waits for a move order to complete and the queued hold behind it to take over, which freezes the
        /// actor so position assertions cannot drift while the harness waits.</summary>
        private static bool RunUntilHeld(SimulationTestHarness harness, RpgSimulationState state, EntityId actorId)
        {
            harness.RunTicks(1);
            Assert.That(QueueOf(state, actorId).Current.Kind, Is.EqualTo(OrderKind.Move), "the move order was not applied");
            return harness.RunUntil(() => QueueOf(state, actorId).Current.Kind == OrderKind.Hold);
        }

        private static SimulationTestHarness Own(RpgSimulationState state, EntityId actorId)
        {
            var player = new PlayerId("local");
            state.Players.Assign(player, actorId);
            return new SimulationTestHarness(state, player);
        }

        private static EntityId Spawn(
            RpgSimulationState state,
            FactionId faction,
            SimulationVector2 position,
            int maximumHealth = 10,
            int attackPower = 1,
            float moveSpeed = 1f,
            float visionRange = 6f,
            float attackRange = 0.5f,
            float attackCooldown = 1f,
            AbilityDefinition[] abilities = null) =>
            state.Actors.Spawn(
                faction == FactionId.Red ? ActorKind.Player : ActorKind.Monster,
                faction,
                new ActorSpawnData(
                    maximumHealth,
                    attackPower,
                    position,
                    0.3f,
                    moveSpeed,
                    visionRange,
                    attackRange,
                    attackCooldown,
                    abilities: abilities));

        private static AbilityDefinition DamageAbility(int id, int damage, int cooldownTicks = 30) =>
            new AbilityDefinition(
                id,
                cooldownTicks,
                2f,
                AbilityTargetMode.CurrentTarget,
                new[] { new AbilityEffect(AbilityEffectType.Damage, damage) });

        /// <summary>A harmless ability for the automatic cast to pick first: it heals its own caster at full health,
        /// so an ordered cast is the only thing that can change an enemy's health. Without it a test would race the
        /// actor's automatic choice in the ticks before the order arrives.</summary>
        private static AbilityDefinition SelfHealAbility(int id) =>
            new AbilityDefinition(
                id,
                30,
                0f,
                AbilityTargetMode.Self,
                new[] { new AbilityEffect(AbilityEffectType.Heal, 1) });

        private static OrderQueueComponent QueueOf(RpgSimulationState state, EntityId actorId)
        {
            Assert.That(state.Actors.TryGet(actorId, out var actor), Is.True, "the actor is gone");
            return actor.Components.Get<OrderQueueComponent>();
        }

        private static SimulationVector2 PositionOf(RpgSimulationState state, EntityId actorId)
        {
            Assert.That(state.Actors.TryGet(actorId, out var actor), Is.True, "the actor is gone");
            return actor.Components.Get<PositionComponent>().Position;
        }

        private static int HealthOf(RpgSimulationState state, EntityId actorId)
        {
            Assert.That(state.Actors.TryGet(actorId, out var actor), Is.True, "the actor is gone");
            return actor.Components.Get<HealthComponent>().CurrentHealth;
        }

        private static int CooldownOf(RpgSimulationState state, EntityId actorId, int abilityIndex)
        {
            Assert.That(state.Actors.TryGet(actorId, out var actor), Is.True, "the actor is gone");
            return actor.Components.Get<AbilityComponent>().CooldownRemaining(abilityIndex);
        }
    }
}
