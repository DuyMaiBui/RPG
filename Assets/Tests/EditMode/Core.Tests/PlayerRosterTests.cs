using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    public sealed class PlayerRosterTests
    {
        [Test]
        public void Assign_LetsOnePlayerOwnManyActors()
        {
            var roster = new PlayerRoster();
            var player = new PlayerId("p1");

            roster.Assign(player, new EntityId(0, 0));
            roster.Assign(player, new EntityId(1, 0));

            Assert.That(roster.Owns(player, new EntityId(0, 0)), Is.True);
            Assert.That(roster.Owns(player, new EntityId(1, 0)), Is.True);
            Assert.That(roster.ActorsOf(player).Count, Is.EqualTo(2));
            Assert.That(roster.PlayerCount, Is.EqualTo(1));
            Assert.That(roster.ActorCount, Is.EqualTo(2));
        }

        [Test]
        public void Owns_IsFalseForAnotherPlayersActor()
        {
            var roster = new PlayerRoster();
            var mine = new PlayerId("p1");
            var theirs = new PlayerId("p2");
            roster.Assign(theirs, new EntityId(0, 0));

            Assert.That(roster.Owns(mine, new EntityId(0, 0)), Is.False);
            Assert.That(roster.Owns(theirs, new EntityId(0, 0)), Is.True);
        }

        [Test]
        public void Owns_IsFalseForAnUnknownActor()
        {
            var roster = new PlayerRoster();

            Assert.That(roster.Owns(new PlayerId("p1"), new EntityId(7, 0)), Is.False);
            Assert.That(roster.ActorsOf(new PlayerId("p1")), Is.Empty);
        }

        [Test]
        public void Assign_MovesAnActorToItsNewOwner()
        {
            var roster = new PlayerRoster();
            var first = new PlayerId("p1");
            var second = new PlayerId("p2");
            var actor = new EntityId(3, 0);
            roster.Assign(first, actor);

            roster.Assign(second, actor);

            Assert.That(roster.Owns(first, actor), Is.False);
            Assert.That(roster.Owns(second, actor), Is.True);
            Assert.That(roster.ActorsOf(first), Is.Empty);
            Assert.That(roster.PlayerCount, Is.EqualTo(1));
        }

        [Test]
        public void Assign_IsIdempotentForTheSameOwner()
        {
            var roster = new PlayerRoster();
            var player = new PlayerId("p1");
            var actor = new EntityId(3, 0);

            roster.Assign(player, actor);
            roster.Assign(player, actor);

            Assert.That(roster.ActorsOf(player).Count, Is.EqualTo(1));
            Assert.That(roster.ActorCount, Is.EqualTo(1));
        }

        [Test]
        public void Remove_DropsTheActorAndForgetsAnEmptyPlayer()
        {
            var roster = new PlayerRoster();
            var player = new PlayerId("p1");
            roster.Assign(player, new EntityId(0, 0));
            roster.Assign(player, new EntityId(1, 0));

            Assert.That(roster.Remove(new EntityId(0, 0)), Is.True);
            Assert.That(roster.Owns(player, new EntityId(0, 0)), Is.False);
            Assert.That(roster.PlayerCount, Is.EqualTo(1));

            Assert.That(roster.Remove(new EntityId(1, 0)), Is.True);
            Assert.That(roster.PlayerCount, Is.EqualTo(0));
            Assert.That(roster.Remove(new EntityId(1, 0)), Is.False);
        }

        [Test]
        public void TryGetOwner_ReportsTheOwningPlayer()
        {
            var roster = new PlayerRoster();
            var player = new PlayerId("p1");
            roster.Assign(player, new EntityId(2, 0));

            Assert.That(roster.TryGetOwner(new EntityId(2, 0), out var owner), Is.True);
            Assert.That(owner, Is.EqualTo(player));
            Assert.That(roster.TryGetOwner(new EntityId(3, 0), out _), Is.False);
        }
    }
}
