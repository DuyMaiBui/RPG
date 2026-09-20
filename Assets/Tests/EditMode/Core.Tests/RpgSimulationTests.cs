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
        using var client = new LocalSimulationClient<RpgSimulationState>(
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
