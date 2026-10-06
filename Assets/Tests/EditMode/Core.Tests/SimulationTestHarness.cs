using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    /// <summary>Drives a real simulation host for a test: it owns the host and one local client, sends commands with
    /// a monotonic sequence and runs the host until a frame arrives. Tests that assert on simulation state read it
    /// directly after the host has produced the frames they asked for.</summary>
    internal sealed class SimulationTestHarness : IDisposable
    {
        private readonly SimulationHost<RpgSimulationState> _host;
        private readonly ISimulationClient _client;
        private long _sequence;
        private long _lastServerTick;

        public SimulationTestHarness(RpgSimulationState state, PlayerId player, int tickRate = 60)
        {
            State = state;
            Player = player;
            _host = new SimulationHost<RpgSimulationState>(state, new RpgSimulationApplication(), new SimulationOptions(tickRate));
            _client = new LocalSimulationClient<RpgSimulationState>(
                _host,
                new SessionContext(new SessionId(Guid.NewGuid()), player));
            _host.Start();
        }

        public RpgSimulationState State { get; }

        public PlayerId Player { get; }

        public bool Send(ISimulationCommand command) =>
            _client.TrySend(new ClientCommandEnvelope(
                ProtocolVersion.Current,
                new ClientSequence(_sequence++),
                new SimulationTick(_lastServerTick),
                command));

        /// <summary>Runs the host until it has produced <paramref name="ticks"/> further frames and returns the last
        /// one. Use it to let orders resolve.</summary>
        public WorldFrameUpdate RunTicks(long ticks)
        {
            var target = _lastServerTick + ticks;
            var update = WaitFor(envelope =>
            {
                Read(envelope);
                return _lastServerTick >= target;
            });
            return (WorldFrameUpdate)update.Payload;
        }

        /// <summary>Runs the host frame by frame until <paramref name="predicate"/> holds over the state or the tick
        /// budget runs out. Use it to assert on a state transition the moment it happens instead of after a fixed
        /// number of ticks.</summary>
        public bool RunUntil(Func<bool> predicate, long maxTicks = 600)
        {
            for (long tick = 0; tick < maxTicks; tick++)
            {
                if (predicate())
                    return true;

                WaitFor(_ => true);
            }

            return predicate();
        }

        public ServerUpdateEnvelope WaitFor(Func<ServerUpdateEnvelope, bool> predicate)
        {
            var timeout = Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < 5000)
            {
                if (_client.TryRead(out var update))
                {
                    Read(update);
                    if (predicate(update))
                        return update;
                }

                Thread.Sleep(2);
            }

            Assert.Fail("Timed out waiting for a simulation update.");
            return default;
        }

        public void Dispose()
        {
            _client.Dispose();
            (_host as IDisposable).Dispose();
        }

        private void Read(in ServerUpdateEnvelope update) => _lastServerTick = update.ServerTick.Value;
    }
}
