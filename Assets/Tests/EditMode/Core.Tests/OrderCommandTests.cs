using System;
using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    public sealed class OrderCommandTests
    {
        private RpgSimulationState _state = null!;
        private SimulationContext<RpgSimulationState> _context = null!;
        private SessionContext _session;
        private ISimulationApplication<RpgSimulationState> _application = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new RpgSimulationState();
            _context = new SimulationContext<RpgSimulationState>(_state, 1f / 60f);
            _session = new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("local"));
            _application = new RpgSimulationApplication();
        }

        [Test]
        public void MoveOrder_FromTheOwningPlayer_BecomesTheActorsOrder()
        {
            var actorId = Spawn(SimulationVector2.Zero);

            Handle(new MoveOrderCommand(actorId, new SimulationVector2(2f, 1f)));

            Assert.That(_state.Signals, Is.Empty);
            var queue = QueueOf(actorId);
            Assert.That(queue.HasOrder, Is.True);
            Assert.That(queue.Current.Kind, Is.EqualTo(OrderKind.Move));
            Assert.That(queue.Current.Destination.X, Is.EqualTo(2f));
            Assert.That(queue.Current.Destination.Y, Is.EqualTo(1f));
        }

        [Test]
        public void MoveOrder_FromAnotherPlayer_IsRejected()
        {
            var actorId = Spawn(SimulationVector2.Zero, owned: false);
            _state.Players.Assign(new PlayerId("someone-else"), actorId);

            Handle(new MoveOrderCommand(actorId, new SimulationVector2(2f, 0f)));

            Assert.That(OnlyRejection(), Is.EqualTo(OrderRejection.PlayerDoesNotOwnActor));
            Assert.That(QueueOf(actorId).HasOrder, Is.False);
        }

        [Test]
        public void MoveOrder_ForAnUnknownActor_IsIndistinguishableFromAnUnownedOne()
        {
            Handle(new MoveOrderCommand(new EntityId(99, 0), new SimulationVector2(2f, 0f)));

            Assert.That(OnlyRejection(), Is.EqualTo(OrderRejection.PlayerDoesNotOwnActor));
        }

        [Test]
        public void MoveOrder_IntoAnUnwalkableCell_IsRejected()
        {
            var actorId = Spawn(SimulationVector2.Zero);

            Handle(new MoveOrderCommand(actorId, new SimulationVector2(5000f, 0f)));

            Assert.That(OnlyRejection(), Is.EqualTo(OrderRejection.DestinationNotWalkable));
            Assert.That(QueueOf(actorId).HasOrder, Is.False);
        }

        [Test]
        public void AttackOrder_ForAMissingOrDeadTarget_IsRejected()
        {
            var actorId = Spawn(SimulationVector2.Zero);

            Handle(new AttackOrderCommand(actorId, new EntityId(42, 0)));
            Assert.That(OnlyRejection(), Is.EqualTo(OrderRejection.TargetMissing));

            var deadId = Spawn(new SimulationVector2(1f, 0f), owned: false);
            _state.Actors.TryGet(deadId, out var dead);
            dead.Components.Get<HealthComponent>().ReceiveDamage(1000);
            _state.Signals.Clear();

            Handle(new AttackOrderCommand(actorId, deadId));
            Assert.That(OnlyRejection(), Is.EqualTo(OrderRejection.TargetMissing));
        }

        [Test]
        public void AttackOrder_ForALiveTarget_BecomesTheOrder()
        {
            var actorId = Spawn(SimulationVector2.Zero);
            var targetId = Spawn(new SimulationVector2(2f, 0f), owned: false);

            Handle(new AttackOrderCommand(actorId, targetId));

            var queue = QueueOf(actorId);
            Assert.That(queue.HasOrder, Is.True);
            Assert.That(queue.Current.Kind, Is.EqualTo(OrderKind.AttackTarget));
            Assert.That(queue.Current.Target, Is.EqualTo(targetId));
        }

        [Test]
        public void CastOrder_ForAnAbilityTheActorDoesNotOwn_IsRejected()
        {
            var actorId = Spawn(SimulationVector2.Zero, abilities: new[] { Ability(1) });
            var targetId = Spawn(new SimulationVector2(1f, 0f), owned: false);

            Handle(new CastAbilityOrderCommand(actorId, 9, targetId));

            Assert.That(OnlyRejection(), Is.EqualTo(OrderRejection.UnknownAbility));
            Assert.That(QueueOf(actorId).HasOrder, Is.False);
        }

        [Test]
        public void CastOrder_ForAnOwnedAbility_BecomesTheOrder()
        {
            var actorId = Spawn(SimulationVector2.Zero, abilities: new[] { Ability(1), Ability(2) });
            var targetId = Spawn(new SimulationVector2(1f, 0f), owned: false);

            Handle(new CastAbilityOrderCommand(actorId, 2, targetId));

            var queue = QueueOf(actorId);
            Assert.That(queue.Current.Kind, Is.EqualTo(OrderKind.CastAbility));
            Assert.That(queue.Current.AbilityId, Is.EqualTo(2));
            Assert.That(queue.Current.Target, Is.EqualTo(targetId));
        }

        [Test]
        public void HoldOrder_HoldsAndStopClearsTheQueue()
        {
            var actorId = Spawn(SimulationVector2.Zero);

            Handle(new HoldOrderCommand(actorId));
            Assert.That(QueueOf(actorId).Current.Kind, Is.EqualTo(OrderKind.Hold));

            Handle(new StopOrderCommand(actorId));
            Assert.That(QueueOf(actorId).HasOrder, Is.False);
        }

        [Test]
        public void QueuedOrder_AppendsBehindTheCurrentOne()
        {
            var actorId = Spawn(SimulationVector2.Zero);

            Handle(new MoveOrderCommand(actorId, new SimulationVector2(2f, 0f)));
            Handle(new HoldOrderCommand(actorId, queued: true));

            var queue = QueueOf(actorId);
            Assert.That(queue.Current.Kind, Is.EqualTo(OrderKind.Move));
            Assert.That(queue.QueuedCount, Is.EqualTo(1));
        }

        [Test]
        public void QueueFull_IsRejectedInsteadOfDroppingAnOrder()
        {
            var actorId = Spawn(SimulationVector2.Zero);
            Handle(new HoldOrderCommand(actorId));
            for (var index = 0; index < OrderQueueComponent.MaximumQueuedOrders; index++)
                Handle(new MoveOrderCommand(actorId, new SimulationVector2(1f, 0f), queued: true));

            _state.Signals.Clear();
            Handle(new MoveOrderCommand(actorId, new SimulationVector2(1f, 0f), queued: true));

            Assert.That(OnlyRejection(), Is.EqualTo(OrderRejection.QueueFull));
            Assert.That(QueueOf(actorId).QueuedCount, Is.EqualTo(OrderQueueComponent.MaximumQueuedOrders));
        }

        private EntityId Spawn(
            SimulationVector2 position,
            bool owned = true,
            AbilityDefinition[] abilities = null)
        {
            var id = _state.Actors.Spawn(
                owned ? ActorKind.Player : ActorKind.Monster,
                owned ? FactionId.Red : FactionId.Blue,
                new ActorSpawnData(10, 1, position, 0.3f, 1f, 6f, 0.5f, 1f, abilities: abilities));
            if (owned)
                _state.Players.Assign(_session.Player, id);
            return id;
        }

        private static AbilityDefinition Ability(int id) =>
            new AbilityDefinition(id, 30, 2f, AbilityTargetMode.CurrentTarget, new[] { new AbilityEffect(AbilityEffectType.Damage, 3) });

        private OrderQueueComponent QueueOf(EntityId actorId)
        {
            Assert.That(_state.Actors.TryGet(actorId, out var actor), Is.True);
            return actor.Components.Get<OrderQueueComponent>();
        }

        private void Handle(ISimulationCommand command)
        {
            var envelope = new ClientCommandEnvelope(
                ProtocolVersion.Current,
                new ClientSequence(1),
                new SimulationTick(0),
                command);
            _application.HandleCommand(_context, _session, in envelope);
        }

        private OrderRejection OnlyRejection()
        {
            Assert.That(_state.Signals.Count, Is.EqualTo(1), "expected exactly one order rejection");
            Assert.That(_state.Signals[0].Kind, Is.EqualTo(PresentationSignalKind.OrderRejected));
            return (OrderRejection)_state.Signals[0].Value;
        }
    }
}
