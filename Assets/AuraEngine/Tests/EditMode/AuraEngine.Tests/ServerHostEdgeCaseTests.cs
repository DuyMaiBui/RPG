using System;
using AuraEngine.Core;
using AuraEngine.Server;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ServerHostEdgeCaseTests
    {
        private static AuraServerSimulationHost CreateHost(out AuraSimulationWorld world)
        {
            var backend = new FakePhysicsBackend();
            world = new AuraSimulationWorld(backend, new AuraWorldDefinition());
            return new AuraServerSimulationHost(world, new AuraServerOptions(tickRate: 60, commandQueueCapacity: 4));
        }

        [Test]
        public void StartTwice_Throws()
        {
            var host = CreateHost(out _);
            using (host)
            {
                host.Start();
                Assert.Throws<InvalidOperationException>(() => host.Start());
                host.Stop();
            }
        }

        [Test]
        public void RestartAfterStop_Throws()
        {
            var host = CreateHost(out _);
            using (host)
            {
                host.Start();
                host.Stop();
                Assert.Throws<InvalidOperationException>(() => host.Start());
            }
        }

        [Test]
        public void StepOnceAfterDispose_Throws()
        {
            var host = CreateHost(out _);
            ((IDisposable)host).Dispose();

            Assert.Throws<ObjectDisposedException>(() => host.StepOnce());
        }

        [Test]
        public void EnqueueCommand_Null_Throws()
        {
            var host = CreateHost(out _);
            using (host)
            {
                Assert.Throws<ArgumentNullException>(() => host.TryEnqueueCommand(null));
            }
        }

        [Test]
        public void Queue_DrainsToZeroAfterStep()
        {
            var host = CreateHost(out _);
            using (host)
            {
                host.TryEnqueueCommand(new TestCommand(1));
                host.TryEnqueueCommand(new TestCommand(2));

                Assert.AreEqual(2, host.QueuedCommandCount);

                host.StepOnce();
                Assert.AreEqual(0, host.QueuedCommandCount);
            }
        }

        [Test]
        public void Stop_WithoutStart_IsSafe()
        {
            var host = CreateHost(out _);
            using (host)
            {
                Assert.DoesNotThrow(() => host.Stop());
                Assert.DoesNotThrow(() => host.Stop());
            }
        }
    }
}
