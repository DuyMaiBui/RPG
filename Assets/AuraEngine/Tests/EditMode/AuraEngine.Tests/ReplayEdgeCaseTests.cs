using System;
using AuraEngine.Core;
using AuraEngine.Serialization;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ReplayEdgeCaseTests
    {
        private static (AuraSimulationWorld World, TestCommandHandler Handler) Build()
        {
            var backend = new FakePhysicsBackend();
            var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 4));
            var handler = new TestCommandHandler();
            world.RegisterCommandHandler(handler);
            return (world, handler);
        }

        [Test]
        public void BuildWithoutCapture_Throws()
        {
            var recorder = new AuraReplayRecorder();
            Assert.Throws<InvalidOperationException>(() => recorder.Build());
        }

        [Test]
        public void Validate_NullInputs_ReturnFalse()
        {
            var recorder = new AuraReplayRecorder();
            var build = Build();
            using (build.World)
            {
                recorder.CaptureInitialState(build.World);
                var replay = recorder.Build();

                Assert.IsFalse(AuraReplayPlayer.TryValidate(null, replay, null, 0.01f, out _));
                Assert.IsFalse(AuraReplayPlayer.TryValidate(build.World, null, null, 0.01f, out _));
            }
        }

        [Test]
        public void Reset_ClearsFramesAndInitialState()
        {
            var recorder = new AuraReplayRecorder();
            var build = Build();
            using (build.World)
            {
                recorder.CaptureInitialState(build.World);
                build.World.Step(new SimulationStep(new SimulationTick(1), 0.01f));
                recorder.Record(build.World);
                Assert.AreEqual(1, recorder.FrameCount);
            }

            recorder.Reset();
            Assert.AreEqual(0, recorder.FrameCount);
            Assert.Throws<InvalidOperationException>(() => recorder.Build());
        }

        [Test]
        public void ReplayFile_RoundTrips()
        {
            var recorder = new AuraReplayRecorder();
            var build = Build();
            using (build.World)
            {
                recorder.CaptureInitialState(build.World);
                for (var tick = 1; tick <= 4; tick++)
                {
                    build.World.Step(new SimulationStep(new SimulationTick((uint)tick), 0.01f));
                    recorder.Record(build.World);
                }
            }

            var replay = recorder.Build();
            var restored = AuraReplayFile.Deserialize(AuraReplayFile.Serialize(replay));

            Assert.AreEqual(replay.FrameCount, restored.FrameCount);
            Assert.AreEqual(replay.InitialState.Tick, restored.InitialState.Tick);
            Assert.AreEqual(replay.InitialState.ComputeHash(), restored.InitialState.ComputeHash());
            for (var index = 0; index < replay.FrameCount; index++)
                Assert.AreEqual(replay[index], restored[index]);
        }

        [Test]
        public void Validate_DetectsInitialStateMismatch()
        {
            var recorder = new AuraReplayRecorder();
            var recorded = Build();
            using (recorded.World)
            {
                recorder.CaptureInitialState(recorded.World);
                recorded.World.Step(new SimulationStep(new SimulationTick(1), 0.01f));
                recorder.Record(recorded.World);
            }

            var replay = recorder.Build();

            var divergent = Build();
            using (divergent.World)
            {
                divergent.World.AttachBody(
                    divergent.World.CreateEntity(),
                    AuraPhysicsBodyDefinition.CreateStatic(
                        AuraPose.Identity,
                        AuraPhysicsLayer.Default,
                        AuraPhysicsLayerMask.All,
                        AuraPhysicsShapeDefinition.Sphere(1f)));

                Assert.IsFalse(AuraReplayPlayer.TryValidate(divergent.World, replay, null, 0.01f, out _));
            }
        }

        [Test]
        public void ReplayWithCommands_ReproducesStateHashes()
        {
            const float deltaTime = 1f / 60f;
            var recorder = new AuraReplayRecorder();

            var recorded = Build();
            using (recorded.World)
            {
                recorder.CaptureInitialState(recorded.World);
                for (var tick = 1; tick <= 6; tick++)
                {
                    recorded.World.EnqueueCommand(new TestCommand(tick));
                    recorded.World.Step(new SimulationStep(new SimulationTick((uint)tick), deltaTime));
                    recorder.Record(recorded.World);
                }
            }

            var replay = recorder.Build();

            var replayed = Build();
            using (replayed.World)
            {
                AuraReplayCommandSource source =
                    (world, tick) => world.EnqueueCommand(new TestCommand((int)tick.Value));

                var matched = AuraReplayPlayer.TryValidate(replayed.World, replay, source, deltaTime, out var mismatchTick);
                Assert.IsTrue(matched, $"Mismatch at tick {mismatchTick}");
                Assert.AreEqual(6, replayed.Handler.HandleCount);
            }
        }
    }
}
