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
    public void AttackCommand_ProducesAuthoritativeDeathFrameAndAcknowledgement()
    {
        var state = new RpgSimulationState();
        var player = new PlayerId("test-player");
        var attacker = state.Actors.Spawn(ActorKind.Player, 10, 5);
        var target = state.Actors.Spawn(ActorKind.Monster, 5, 0);
        state.PlayerActors.Add(player, attacker);

        using var host = new SimulationHost<RpgSimulationState>(
            state,
            new RpgSimulationApplication(),
            new SimulationOptions(tickRate: 120));
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
            host,
            new SessionContext(new SessionId(Guid.NewGuid()), player));

        host.Start();
        var command = new ClientCommandEnvelope(
            ProtocolVersion.Current,
            new ClientSequence(0),
            new SimulationTick(0),
            new AttackCommand(target));
        Assert.That(client.TrySend(command), Is.True);

        var update = WaitForUpdate(client, frame =>
            frame.LastProcessedClientSequence.Value == 0 &&
            frame.Payload is WorldFrameUpdate world &&
            !world.Actors.Span.ToArray().Any(actor => actor.Entity == target));

        Assert.That(update.Payload, Is.TypeOf<WorldFrameUpdate>());
        var worldFrame = (WorldFrameUpdate)update.Payload;
        Assert.That(worldFrame.Signals.Span.ToArray().Any(
            signal => signal.Kind == PresentationSignalKind.Died && signal.Entity == target), Is.True);
        Assert.That(state.Actors.TryGet(target, out _), Is.False);
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
    public void ZeroDamageAttack_StillAdvancesTurn()
    {
        var state = new RpgSimulationState();
        var application = new RpgSimulationApplication();
        using var host = new SimulationHost<RpgSimulationState>(state, application, new SimulationOptions(10));
        var redId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, 10, 0);
        var blueId = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, 10, 1);
        var red = new PlayerId("red");
        state.PlayerActors.Add(red, redId);
        state.Turns.Initialize(new[] { redId, blueId });
        using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(host, new SessionContext(new SessionId(Guid.NewGuid()), red));

        host.Start();
        Assert.That(client.TrySend(new ClientCommandEnvelope(
            ProtocolVersion.Current,
            new ClientSequence(0),
            new SimulationTick(0),
            new AttackCommand(blueId))), Is.True);

        WaitForUpdate(client, update => update.Payload is WorldFrameUpdate frame && frame.TurnNumber > 0);
        Assert.That(state.Turns.ActiveActorId, Is.EqualTo(blueId));
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
