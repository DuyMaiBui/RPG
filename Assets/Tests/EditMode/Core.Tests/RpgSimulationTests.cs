using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Core.Navigation;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{

public sealed class RpgSimulationTests
{
    [Test]
    public void DestroyedEntityId_CannotResolveAfterItsSlotIsReused()
    {
        var registry = new ActorRegistry();
        var oldId = registry.Spawn(ActorKind.Monster, 10, 0);

        Assert.That(registry.Destroy(oldId), Is.True);
        var replacementId = registry.Spawn(ActorKind.Monster, 10, 0);

        Assert.That(replacementId.Index, Is.EqualTo(oldId.Index));
        Assert.That(replacementId.Generation, Is.Not.EqualTo(oldId.Generation));
        Assert.That(registry.TryGet(oldId, out _), Is.False);
        Assert.That(registry.TryGet(replacementId, out _), Is.True);
    }

    [Test]
    public void AutoBattle_ProducesAuthoritativeDeathFrame()
    {
        var state = new RpgSimulationState();
        var attacker = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            5, 5, new SimulationVector2(-0.3f, 0f), 0.3f, 1f, 5f, 0.2f, 0.01f));
        var target = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
            5, 5, new SimulationVector2(0.3f, 0f), 0.3f, 1f, 5f, 0.2f, 0.01f));

        using var host = new SimulationHost<RpgSimulationState>(
            state,
            new RpgSimulationApplication(),
            new SimulationOptions(tickRate: 120));
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
            host,
            new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("observer")));

        host.Start();
        var update = WaitForUpdate(client, frame =>
            frame.Payload is WorldFrameUpdate && !state.Actors.TryGet(target, out _));

        Assert.That(update.Payload, Is.TypeOf<WorldFrameUpdate>());
        Assert.That(state.Actors.TryGet(target, out _), Is.False);
    }

    [Test]
    public void AutoBattle_ProjectileProducesAuthoritativeDeathFrame()
    {
        var state = new RpgSimulationState();
        var attacker = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            5, 5, new SimulationVector2(-0.8f, 0f), 0.3f, 0f, 5f, 5f, 0.01f,
            attackType: AttackType.Projectile, projectileSpeed: 8f, projectileRadius: 0.05f));
        var target = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
            5, 1, new SimulationVector2(0.8f, 0f), 0.3f, 0f, 5f, 0.2f, 0.01f));

        using var host = new SimulationHost<RpgSimulationState>(
            state, new RpgSimulationApplication(), new SimulationOptions(tickRate: 60));
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
            host,
            new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("projectile-observer")));

        host.Start();
        var update = WaitForUpdate(client, frame =>
            frame.Payload is WorldFrameUpdate && !state.Actors.TryGet(target, out _));

        Assert.That(update.Payload, Is.TypeOf<WorldFrameUpdate>());
        Assert.That(state.Actors.TryGet(attacker, out _), Is.True);
    }

    [Test]
    public void FaultedSimulationHost_StopsAndReportsItsOriginalException()
    {
        using var host = new SimulationHost<object>(
            new object(),
            new ThrowingApplication(),
            new SimulationOptions(tickRate: 120));

        host.Start();
        var timeout = Stopwatch.StartNew();
        while (host.IsRunning && timeout.ElapsedMilliseconds < 1000)
            Thread.Sleep(5);

        Assert.That(host.IsRunning, Is.False);
        Assert.That(host.Fault, Is.TypeOf<InvalidOperationException>());
        Assert.Throws<InvalidOperationException>(() => host.TryReadLatest(out _));
    }

    [Test]
    public void TargetSelector_RetargetsToNextAliveEnemyWithoutRangeChecks()
    {
        var registry = new ActorRegistry();
        var attackerId = registry.Spawn(ActorKind.Player, FactionId.Red, 10, 1);
        var defeatedId = registry.Spawn(ActorKind.Monster, FactionId.Blue, 1, 0);
        var replacementId = registry.Spawn(ActorKind.Monster, FactionId.Blue, 10, 0);
        Assert.That(registry.TryGet(attackerId, out var attacker), Is.True);
        Assert.That(registry.Destroy(defeatedId), Is.True);

        var selector = new AnyAliveEnemyTargetSelector();

        Assert.That(selector.TrySelect(attacker, registry, out var selected), Is.True);
        Assert.That(selected, Is.EqualTo(replacementId));
    }

    [Test]
    public void AutoBattle_ZeroDamageDoesNotDestroyTarget()
    {
        var state = new RpgSimulationState();
        var redId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            10, 0, new SimulationVector2(-0.3f, 0f), 0.3f, 0f, 5f, 0.2f, 0.1f));
        var blueId = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
            10, 1, new SimulationVector2(0.3f, 0f), 0.3f, 0f, 5f, 0.2f, 0.1f));
        using var host = new SimulationHost<RpgSimulationState>(state, new RpgSimulationApplication(), new SimulationOptions(30));
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
            host,
            new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("observer")));

        host.Start();
        WaitForUpdate(client, update => update.Payload is WorldFrameUpdate frame && frame.TurnNumber >= 2);
        Assert.That(state.Actors.TryGet(redId, out _), Is.True);
        Assert.That(state.Actors.TryGet(blueId, out _), Is.True);
    }

    [Test]
    public void MoveIntentCommand_DrivesPlayerMovementThroughSimulation()
    {
        var state = new RpgSimulationState();
        var actorId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            10, 0, SimulationVector2.Zero, 0.3f, 1f, 5f, 0.2f, 0.1f));
        var player = new PlayerId("local-player");
        state.Players.Assign(player, actorId);
        using var host = new SimulationHost<RpgSimulationState>(state, new RpgSimulationApplication(), new SimulationOptions(60));
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
            host, new SessionContext(new SessionId(Guid.NewGuid()), player));

        host.Start();
        Assert.That(client.TrySend(new ClientCommandEnvelope(
            ProtocolVersion.Current,
            new ClientSequence(1),
            new SimulationTick(0),
            new MoveIntentCommand(new SimulationVector2(1f, 0f)))), Is.True);
        WaitForUpdate(client, update => update.Payload is WorldFrameUpdate frame && frame.TurnNumber >= 2);

        Assert.That(state.Actors.TryGet(actorId, out var actor), Is.True);
        Assert.That(actor.Components.Get<PositionComponent>().Position.X, Is.GreaterThan(0f));
    }

    [Test]
    public void AutoBattle_MovesStraightOnClearPathBeforeCollision()
    {
        var state = new RpgSimulationState();
        var redId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            100, 1, new SimulationVector2(-3f, 0f), 0.3f, 1f, 6f, 0.1f, 100f));
        var blueId = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
            100, 1, new SimulationVector2(3f, 0f), 0.3f, 1f, 6f, 0.1f, 100f));
        using var host = new SimulationHost<RpgSimulationState>(state, new RpgSimulationApplication(), new SimulationOptions(60));
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
            host, new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("straight-path")));

        host.Start();
        WaitForUpdate(client, update => update.Payload is WorldFrameUpdate frame && frame.TurnNumber >= 10);

        Assert.That(state.Actors.TryGet(redId, out var red), Is.True);
        Assert.That(state.Actors.TryGet(blueId, out var blue), Is.True);
        Assert.That(red.Components.Get<PositionComponent>().Position.Y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(blue.Components.Get<PositionComponent>().Position.Y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(red.Components.Get<PositionComponent>().Position.X, Is.GreaterThan(-3f));
        Assert.That(blue.Components.Get<PositionComponent>().Position.X, Is.LessThan(3f));
    }

    [Test]
    public void AutoBattle_MovesTowardEnemyOutsideVisionUntilItCanAcquireTarget()
    {
        var state = new RpgSimulationState();
        var redId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            100, 1, new SimulationVector2(-3f, 0f), 0.3f, 1f, 0.5f, 0.1f, 100f));
        state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
            100, 1, new SimulationVector2(3f, 0f), 0.3f, 1f, 0.5f, 0.1f, 100f));
        using var host = new SimulationHost<RpgSimulationState>(state, new RpgSimulationApplication(), new SimulationOptions(60));
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
            host, new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("long-route")));

        host.Start();
        WaitForUpdate(client, update => update.Payload is WorldFrameUpdate frame && frame.TurnNumber >= 10);

        Assert.That(state.Actors.TryGet(redId, out var red), Is.True);
        Assert.That(red.Components.Get<PositionComponent>().Position.X, Is.GreaterThan(-3f));
    }

    [Test]
    public void AutoBattle_RoutesAroundStationaryCrowdBlocker()
    {
        var state = new RpgSimulationState();
        var redId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            100, 1, new SimulationVector2(-3f, 0f), 0.3f, 1f, 8f, 0.1f, 100f));
        state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            100, 1, new SimulationVector2(-2.1f, -0.45f), 0.3f, 0f, 8f, 0.1f, 100f));
        state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            100, 1, new SimulationVector2(-2.1f, 0f), 0.3f, 0f, 8f, 0.1f, 100f));
        state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            100, 1, new SimulationVector2(-2.1f, 0.45f), 0.3f, 0f, 8f, 0.1f, 100f));
        state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
            100, 1, new SimulationVector2(3f, 0f), 0.3f, 0f, 8f, 0.1f, 100f));

        using var host = new SimulationHost<RpgSimulationState>(
            state,
            new RpgSimulationApplication(),
            new SimulationOptions(60));
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
            host,
            new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("crowd-detour")));

        host.Start();
        WaitForUpdate(client, update => update.Payload is WorldFrameUpdate frame && frame.TurnNumber >= 60);

        Assert.That(state.Actors.TryGet(redId, out var red), Is.True);
        var position = red.Components.Get<PositionComponent>().Position;
        Assert.That(position.X, Is.GreaterThan(-3f));
        Assert.That(System.MathF.Abs(position.Y), Is.GreaterThan(0.1f));
    }

    [Test]
    public void AutoBattle_CommitsToOneStaticObstacleSideWithoutRouteOscillation()
    {
        var state = new RpgSimulationState(12, 8, 1f);
        state.Navigation.ApplyObstacle(new NavigationObstacle(
            1,
            new SimulationVector2(0f, 0f),
            CollisionShape.Box(new SimulationVector2(0.4f, 1.2f))));
        var redId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
            1000, 0, new SimulationVector2(-4f, 0f), 0.3f, 3f, 10f, 0.1f, 100f));
        state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
            1000, 0, new SimulationVector2(4f, 0f), 0.3f, 0f, 10f, 0.1f, 100f));
        using var host = new SimulationHost<RpgSimulationState>(
            state,
            new RpgSimulationApplication(),
            new SimulationOptions(60));
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
            host,
            new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("static-obstacle")));

        host.Start();
        var side = 0;
        var previousX = -4f;
        for (var tick = 0; tick < 180; tick++)
        {
            WaitForUpdate(client, update => update.Payload is WorldFrameUpdate frame && frame.TurnNumber >= tick + 1);
            Assert.That(state.Actors.TryGet(redId, out var red), Is.True);
            var position = red.Components.Get<PositionComponent>().Position;
            Assert.That(position.X, Is.GreaterThanOrEqualTo(previousX - 0.001f));
            previousX = position.X;
            if (System.MathF.Abs(position.Y) > 0.1f)
            {
                var currentSide = position.Y > 0f ? 1 : -1;
                if (side == 0) side = currentSide;
                Assert.That(currentSide, Is.EqualTo(side));
            }
        }

        Assert.That(previousX, Is.GreaterThan(-1f));
        Assert.That(side, Is.Not.EqualTo(0));
    }

    [Test]
    public void WaveSpawner_SpawnsThreeMeleeThenThreeRangedPerFaction()
    {
        var state = new RpgSimulationState(60, 40, 1f);
        var melee = new ActorSpawnData(
            10, 1, SimulationVector2.Zero, 0.35f, 0f, 6f, 0.25f, 1f);
        var ranged = new ActorSpawnData(
            10, 1, SimulationVector2.Zero, 0.35f, 0f, 6f, 3.5f, 1f,
            attackType: AttackType.Projectile, projectileSpeed: 5f, projectileLifetime: 5f);
        state.Waves.Configure(
            3, 3, 0.5f, 10f, 0f,
            melee, ranged, melee, ranged,
            new SimulationVector2(-24f, 0f), new SimulationVector2(24f, 0f));

        for (var tick = 0; tick < 6; tick++)
            state.Waves.Tick(state, 0.5f);

        state.Waves.Tick(state, 6.5f);
        Assert.That(state.Actors.SlotCount, Is.EqualTo(12));
        state.Waves.Tick(state, 0.5f);
        Assert.That(state.Actors.SlotCount, Is.EqualTo(14));

        var redCount = 0;
        var blueCount = 0;
        var redMelee = 0;
        var redRanged = 0;
        var blueMelee = 0;
        var blueRanged = 0;
        for (var index = 0; index < state.Actors.SlotCount; index++)
        {
            if (!state.Actors.TryGetAt(index, out var actor)) continue;
            var faction = actor.Components.Get<FactionComponent>().Faction;
            if (faction == FactionId.Red) redCount++;
            if (faction == FactionId.Blue) blueCount++;
            if (faction == FactionId.Red && actor.Components.TryGet<ProjectileWeaponComponent>(out _)) redRanged++;
            if (faction == FactionId.Red && !actor.Components.TryGet<ProjectileWeaponComponent>(out _)) redMelee++;
            if (faction == FactionId.Blue && actor.Components.TryGet<ProjectileWeaponComponent>(out _)) blueRanged++;
            if (faction == FactionId.Blue && !actor.Components.TryGet<ProjectileWeaponComponent>(out _)) blueMelee++;
            Assert.That(state.Navigation.IsPositionWalkable(
                actor.Components.Get<PositionComponent>().Position,
                actor.Components.Get<ColliderComponent>().Compound.BoundingRadius), Is.True);
        }

        Assert.That(redCount, Is.EqualTo(7));
        Assert.That(blueCount, Is.EqualTo(7));
        Assert.That(redMelee, Is.EqualTo(4));
        Assert.That(redRanged, Is.EqualTo(3));
        Assert.That(blueMelee, Is.EqualTo(4));
        Assert.That(blueRanged, Is.EqualTo(3));
    }

    private static ServerUpdateEnvelope WaitForUpdate(
        ISimulationClient client,
        Func<ServerUpdateEnvelope, bool> predicate)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.ElapsedMilliseconds < 2000)
        {
            if (client.TryRead(out var update) && predicate(update))
                return update;

            Thread.Sleep(5);
        }

        Assert.Fail("Timed out waiting for simulation update.");
        return default;
    }

}
}
