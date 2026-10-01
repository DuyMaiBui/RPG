using AuraEngine.Core;
using AuraEngine.Server;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ServerHostTests
    {
        [Test]
        public void StepOnce_AdvancesTickAndHandlesCommands()
        {
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition());
            var handler = new TestCommandHandler();
            world.RegisterCommandHandler(handler);

            using var host = new AuraServerSimulationHost(world, new AuraServerOptions(tickRate: 60));

            host.StepOnce();
            Assert.AreEqual(0u, world.CurrentTick.Value);

            Assert.IsTrue(host.TryEnqueueCommand(new TestCommand(5)));
            host.StepOnce();

            Assert.AreEqual(1u, world.CurrentTick.Value);
            Assert.AreEqual(1, handler.HandleCount);
            Assert.AreEqual(5, handler.Sum);
        }

        [Test]
        public void StepOnce_Soak_ProcessesQueueWithoutFault()
        {
            const int ticks = 5000;
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition());
            var handler = new TestCommandHandler();
            world.RegisterCommandHandler(handler);

            using var host = new AuraServerSimulationHost(
                world,
                new AuraServerOptions(tickRate: 60, commandQueueCapacity: ticks + 1));

            for (var index = 0; index < ticks; index++)
            {
                Assert.IsTrue(host.TryEnqueueCommand(new TestCommand(1)));
                host.StepOnce();
            }

            Assert.IsNull(host.Fault);
            Assert.AreEqual(ticks, handler.HandleCount);
            Assert.AreEqual(ticks, handler.Sum);
            Assert.AreEqual(ticks - 1, (int)world.CurrentTick.Value);
            Assert.AreEqual(0, host.QueuedCommandCount);
        }

        [Test]
        public void EnqueueCommand_RespectsCapacity()
        {
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition());
            using var host = new AuraServerSimulationHost(world, new AuraServerOptions(commandQueueCapacity: 2));

            Assert.IsTrue(host.TryEnqueueCommand(new TestCommand(1)));
            Assert.IsTrue(host.TryEnqueueCommand(new TestCommand(1)));
            Assert.IsFalse(host.TryEnqueueCommand(new TestCommand(1)));

            host.StepOnce();
            Assert.AreEqual(0, host.QueuedCommandCount);
        }

        [Test]
        public void StartAndStop_RunsBackgroundLoop()
        {
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition());
            using var host = new AuraServerSimulationHost(world, new AuraServerOptions(tickRate: 120));

            host.Start();
            Assert.IsTrue(host.IsRunning);

            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (world.CurrentTick.Value == 0 && deadline.ElapsedMilliseconds < 2000)
                System.Threading.Thread.Sleep(5);

            host.Stop();
            Assert.IsFalse(host.IsRunning);
            Assert.Greater(world.CurrentTick.Value, 0u);
        }
    }
}
