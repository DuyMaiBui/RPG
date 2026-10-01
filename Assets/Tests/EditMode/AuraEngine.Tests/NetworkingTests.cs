using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Networking;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class NetworkingTests
    {
        private static AuraSimulationWorld CreateWorld(out AuraInputCommandHandler handler)
        {
            var world = new AuraSimulationWorld(ManagedPhysicsBackend.Instance, new AuraWorldDefinition(initialBodyCapacity: 8));
            handler = new AuraInputCommandHandler(1f);
            world.RegisterCommandHandler(handler);
            return world;
        }

        private static SimulationEntityId AddKinematicActor(AuraSimulationWorld world, AuraInputCommandHandler handler)
        {
            var entity = world.CreateEntity();
            world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateKinematic(
                    new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            handler.Entity = entity;
            return entity;
        }

        [Test]
        public void InputCodec_RoundTrips()
        {
            var payload = AuraNetCodec.EncodeInput(7u, 42u, -3, 5);
            Assert.IsTrue(AuraNetCodec.TryDecodeInput(payload, out var command));
            Assert.AreEqual(7u, command.ClientId);
            Assert.AreEqual(42u, command.Sequence);
            Assert.AreEqual(-3, command.MoveX);
            Assert.AreEqual(5, command.MoveY);
        }

        [Test]
        public void SnapshotCodec_RoundTrips()
        {
            var state = new byte[] { 1, 2, 3, 4, 5 };
            var payload = AuraNetCodec.EncodeSnapshot(99u, state);
            Assert.IsTrue(AuraNetCodec.TryDecodeSnapshot(payload, out var tick, out var decoded));
            Assert.AreEqual(99u, tick);
            CollectionAssert.AreEqual(state, decoded);
        }

        [Test]
        public void AckCodec_RoundTrips()
        {
            var payload = AuraNetCodec.EncodeAck(3u, 12u);
            Assert.IsTrue(AuraNetCodec.TryDecodeAck(payload, out var clientId, out var sequence));
            Assert.AreEqual(3u, clientId);
            Assert.AreEqual(12u, sequence);
        }

        [Test]
        public void LockstepBuffer_ReleasesWhenAllClientsSubmit()
        {
            var buffer = new AuraLockstepBuffer();
            buffer.Submit(10u, new AuraInputCommand(0u, 1u, 1, 0));
            Assert.IsFalse(buffer.IsTickComplete(10u, 2));

            buffer.Submit(10u, new AuraInputCommand(1u, 1u, -1, 0));
            Assert.IsTrue(buffer.IsTickComplete(10u, 2));

            var commands = buffer.Take(10u);
            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual(0, buffer.PendingTickCount);
        }

        [Test]
        public void Transport_DeliversBetweenPeers()
        {
            var (client, server) = InMemoryAuraTransport.CreatePair();
            client.Send(AuraNetCodec.EncodeInput(1u, 1u, 1, 1));

            Assert.IsTrue(server.TryReceive(out var payload));
            Assert.IsTrue(AuraNetCodec.TryDecodeInput(payload, out var command));
            Assert.AreEqual(1u, command.ClientId);
        }

        [Test]
        public void Prediction_MatchesServerSteadyState()
        {
            using var serverWorld = CreateWorld(out var serverHandler);
            using var clientWorld = CreateWorld(out var clientHandler);
            var serverEntity = AddKinematicActor(serverWorld, serverHandler);
            var clientEntity = AddKinematicActor(clientWorld, clientHandler);

            var (clientTransport, serverTransport) = InMemoryAuraTransport.CreatePair();
            var server = new AuraNetServer(serverWorld, serverTransport, 60);
            var replay = new AuraPredictionReplay(clientEntity, 1f);
            var predicted = new AuraPredictedWorld(clientWorld, replay);
            var client = new AuraNetClient(1u, clientTransport, predicted, 60);

            for (var frame = 0; frame < 60; frame++)
            {
                client.SubmitInput(1, 0);
                server.Pump();
                server.Step();
                client.Pump();
            }

            Assert.AreEqual(0, client.PendingInputCount, "all acknowledged inputs should be cleared.");
            Assert.IsTrue(serverWorld.TryGetBodyState(serverEntity, out var serverState));
            Assert.IsTrue(clientWorld.TryGetBodyState(clientEntity, out var clientState));
            Assert.AreEqual(serverState.Pose.Position.X, clientState.Pose.Position.X, 1e-3f);
            Assert.AreEqual(serverState.Pose.Position.Z, clientState.Pose.Position.Z, 1e-3f);
        }

        [Test]
        public void Reconcile_ReSimulatesUnacknowledgedInput()
        {
            using var clientWorld = CreateWorld(out var handler);
            var entity = AddKinematicActor(clientWorld, handler);
            var replay = new AuraPredictionReplay(entity, 1f);
            var predicted = new AuraPredictedWorld(clientWorld, replay);

            predicted.Predict(new AuraInputCommand(1u, 1u, 1, 0), new SimulationStep(new SimulationTick(1u), 1f / 60f));
            predicted.Predict(new AuraInputCommand(1u, 2u, 1, 0), new SimulationStep(new SimulationTick(2u), 1f / 60f));

            var serverState = clientWorld.SaveState();
            var serverPosition = clientWorld.TryGetBodyState(entity, out var s) ? s.Pose.Position.X : 0f;

            predicted.Reconcile(1u, serverState, 1u);

            Assert.AreEqual(1, predicted.PendingInputCount, "sequence 1 is acknowledged, sequence 2 remains.");
        }
    }
}
