using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    public sealed class OrderQueueTests
    {
        [Test]
        public void FirstOrder_BecomesCurrent()
        {
            var queue = new OrderQueueComponent();

            Assert.That(queue.Enqueue(ActorOrder.Move(new SimulationVector2(1f, 2f)), replace: false), Is.True);

            Assert.That(queue.HasOrder, Is.True);
            Assert.That(queue.Current.Kind, Is.EqualTo(OrderKind.Move));
            Assert.That(queue.QueuedCount, Is.EqualTo(0));
        }

        [Test]
        public void Replace_DropsThePreviousOrders()
        {
            var queue = new OrderQueueComponent();
            queue.Enqueue(ActorOrder.Move(new SimulationVector2(1f, 0f)), replace: false);
            queue.Enqueue(ActorOrder.Move(new SimulationVector2(2f, 0f)), replace: false);

            queue.Enqueue(ActorOrder.Hold(), replace: true);

            Assert.That(queue.Current.Kind, Is.EqualTo(OrderKind.Hold));
            Assert.That(queue.QueuedCount, Is.EqualTo(0));
            Assert.That(queue.Advance(), Is.False);
        }

        [Test]
        public void Append_RunsInOrderAfterTheCurrentOne()
        {
            var queue = new OrderQueueComponent();
            queue.Enqueue(ActorOrder.Move(new SimulationVector2(1f, 0f)), replace: false);
            queue.Enqueue(ActorOrder.Move(new SimulationVector2(2f, 0f)), replace: false);
            queue.Enqueue(ActorOrder.Move(new SimulationVector2(3f, 0f)), replace: false);

            Assert.That(queue.Current.Destination.X, Is.EqualTo(1f));
            Assert.That(queue.Advance(), Is.True);
            Assert.That(queue.Current.Destination.X, Is.EqualTo(2f));
            Assert.That(queue.Advance(), Is.True);
            Assert.That(queue.Current.Destination.X, Is.EqualTo(3f));
            Assert.That(queue.Advance(), Is.False);
            Assert.That(queue.HasOrder, Is.False);
        }

        [Test]
        public void Append_RefusesWhenTheQueueIsFull()
        {
            var queue = new OrderQueueComponent();
            queue.Enqueue(ActorOrder.Hold(), replace: false);
            for (var index = 0; index < OrderQueueComponent.MaximumQueuedOrders; index++)
                Assert.That(queue.Enqueue(ActorOrder.Hold(), replace: false), Is.True);

            Assert.That(queue.QueuedCount, Is.EqualTo(OrderQueueComponent.MaximumQueuedOrders));
            Assert.That(queue.Enqueue(ActorOrder.Hold(), replace: false), Is.False);
        }

        [Test]
        public void Advance_OnAnEmptyQueueClearsIt()
        {
            var queue = new OrderQueueComponent();
            queue.Enqueue(ActorOrder.Hold(), replace: true);

            Assert.That(queue.Advance(), Is.False);

            Assert.That(queue.HasOrder, Is.False);
            Assert.That(queue.QueuedCount, Is.EqualTo(0));
        }

        [Test]
        public void Clear_DropsCurrentAndQueuedOrders()
        {
            var queue = new OrderQueueComponent();
            queue.Enqueue(ActorOrder.Move(new SimulationVector2(1f, 0f)), replace: false);
            queue.Enqueue(ActorOrder.Hold(), replace: false);

            queue.Clear();

            Assert.That(queue.HasOrder, Is.False);
            Assert.That(queue.QueuedCount, Is.EqualTo(0));
        }

        [Test]
        public void ActorOrder_ReportsWhichFieldsItsKindUses()
        {
            Assert.That(ActorOrder.Move(new SimulationVector2(1f, 0f)).HasDestination, Is.True);
            Assert.That(ActorOrder.AttackMove(new SimulationVector2(1f, 0f)).HasDestination, Is.True);
            Assert.That(ActorOrder.AttackTarget(new EntityId(1, 0)).HasLockedTarget, Is.True);
            Assert.That(ActorOrder.CastAbility(7, new EntityId(1, 0)).HasLockedTarget, Is.True);
            Assert.That(ActorOrder.Hold().HasDestination, Is.False);
            Assert.That(ActorOrder.Hold().HasLockedTarget, Is.False);

            var cast = ActorOrder.CastAbility(7, new EntityId(2, 0));
            Assert.That(cast.AbilityId, Is.EqualTo(7));
            Assert.That(cast.Target, Is.EqualTo(new EntityId(2, 0)));
        }
    }
}
