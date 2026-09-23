using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using RPG.Core.Actors;
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
    public void TurnState_RemovesDefeatedActorBeforeAdvancing()
    {
        var registry = new ActorRegistry();
        var redId = registry.Spawn(ActorKind.Player, FactionId.Red, 10, 1);
        var blueId = registry.Spawn(ActorKind.Monster, FactionId.Blue, 10, 1);
        var order = new[] { redId, blueId };
        var turns = new TurnState();
        turns.Initialize(order);

        Assert.That(registry.Destroy(blueId), Is.True);
        Assert.That(turns.Advance(registry), Is.True);
        Assert.That(turns.ActiveActorId, Is.EqualTo(redId));
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
        state.PlayerActors.Add(player, actorId);
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

    private static ServerUpdateEnvelope WaitForUpdate(
        ISimulationClient client,
        Func<ServerUpdateEnvelope, bool> predicate)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.ElapsedMilliseconds < 1000)
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
