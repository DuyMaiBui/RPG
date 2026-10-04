using AuraEngine.Core;
using AuraEngine.Serialization;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ReplayTests
    {
        private static (AuraSimulationWorld World, TestMoveSystem System) Build()
        {
            var backend = new FakePhysicsBackend();
            var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 4));
            var entity = world.CreateEntity();
            world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateKinematic(
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(1f)));

            var system = new TestMoveSystem(entity, new AuraVector3(0.25f, 0f, 0f));
            world.AddSystem(system);
            return (world, system);
        }

        [Test]
        public void RecordedReplay_ReproducesStateHashes()
        {
            const float deltaTime = 1f / 60f;
            var recorder = new AuraReplayRecorder();

            var recorded = Build();
            using (recorded.World)
            {
                recorder.CaptureInitialState(recorded.World);
                for (var tick = 1; tick <= 8; tick++)
                {
                    recorded.World.Step(new SimulationStep(new SimulationTick((uint)tick), deltaTime));
                    recorder.Record(recorded.World);
                }
            }

            var replay = recorder.Build();

            var replayed = Build();
            using (replayed.World)
            {
                var matched = AuraReplayPlayer.TryValidate(replayed.World, replay, null, deltaTime, out var mismatchTick);
                Assert.IsTrue(matched, $"Mismatch at tick {mismatchTick}");
            }
        }

        [Test]
        public void Replay_DetectsDivergence()
        {
            const float deltaTime = 1f / 60f;
            var recorder = new AuraReplayRecorder();

            var recorded = Build();
            using (recorded.World)
            {
                recorder.CaptureInitialState(recorded.World);
                for (var tick = 1; tick <= 4; tick++)
                {
                    recorded.World.Step(new SimulationStep(new SimulationTick((uint)tick), deltaTime));
                    recorder.Record(recorded.World);
                }
            }

            var replay = recorder.Build();

            var divergent = Build();
            using (divergent.World)
            {
                divergent.System.Step = new AuraVector3(0.5f, 0f, 0f);
                Assert.IsFalse(AuraReplayPlayer.TryValidate(divergent.World, replay, null, deltaTime, out var mismatchTick));
                Assert.AreNotEqual(default, mismatchTick);
            }
        }
    }
}
