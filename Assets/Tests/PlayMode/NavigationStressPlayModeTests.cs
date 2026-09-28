using System;
using System.Collections;
using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;
using UnityEngine.TestTools;

namespace RPG.PlayMode.Tests
{
    public sealed class NavigationStressPlayModeTests
    {
        [UnityTest]
        public IEnumerator FiveHundredActorsKeepProducingSimulationFrames()
        {
            var state = new RpgSimulationState(80, 40, 1f);
            for (var index = 0; index < 250; index++)
            {
                Spawn(state, FactionId.Red, index, -1f);
                Spawn(state, FactionId.Blue, index, 1f);
            }

            using var host = new SimulationHost<RpgSimulationState>(
                state,
                new RpgSimulationApplication(),
                new SimulationOptions(tickRate: 60));
            using ISimulationClient client = new LocalSimulationClient<RpgSimulationState>(
                host,
                new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("playmode-stress")));
            host.Start();

            var deadline = DateTime.UtcNow.AddSeconds(5);
            var latestTick = -1;
            while (DateTime.UtcNow < deadline && latestTick < 30)
            {
                if (client.TryRead(out var update) && update.Payload is WorldFrameUpdate)
                    latestTick = ((WorldFrameUpdate)update.Payload).TurnNumber;
                yield return null;
            }

            Assert.That(host.Fault, Is.Null);
            Assert.That(latestTick, Is.GreaterThanOrEqualTo(30));
            Assert.That(state.Actors.SlotCount, Is.EqualTo(500));
        }

        private static void Spawn(RpgSimulationState state, FactionId faction, int index, float side)
        {
            var columns = 20;
            var row = index / columns;
            var column = index % columns;
            var position = new SimulationVector2(
                side * (25f - column * 0.85f),
                (row - 6f) * 0.85f);
            state.Actors.Spawn(
                ActorKind.Monster,
                faction,
                new ActorSpawnData(
                    100,
                    0,
                    position,
                    0.3f,
                    1f,
                    40f,
                    0.5f,
                    10f,
                    attackType: AttackType.Melee,
                    colliderShapes: new[]
                    {
                        new ColliderShapeData(
                            CollisionShape.Circle(0.3f),
                            SimulationVector2.Zero,
                            0f,
                            ColliderMode.Solid,
                            new ColliderFilter(0, -1)),
                    }));
        }
    }
}
