using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Server
{
    public sealed class AuraServerSimulationHost : IDisposable
    {
        private readonly AuraSimulationWorld _world;
        private readonly AuraServerOptions _options;
        private readonly ConcurrentQueue<IAuraCommand> _inbound = new ConcurrentQueue<IAuraCommand>();
        private readonly AutoResetEvent _wakeSignal = new AutoResetEvent(false);
        private readonly Thread _thread;
        private long _queuedCommands;
        private uint _nextTick;
        private int _running;
        private int _started;
        private int _disposed;
        private Exception _fault;

        public AuraServerSimulationHost(AuraSimulationWorld world, AuraServerOptions options = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _options = options ?? new AuraServerOptions();
            _options.Validate();
            _thread = new Thread(Run) { IsBackground = true, Name = "AuraEngine Server" };
        }

        public AuraSimulationWorld World => _world;

        public SimulationTick CurrentTick => _world.CurrentTick;

        public bool IsRunning => Volatile.Read(ref _running) == 1;

        public Exception Fault => Volatile.Read(ref _fault);

        public long QueuedCommandCount => Volatile.Read(ref _queuedCommands);

        public void Start()
        {
            ThrowIfDisposed();
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("The server simulation host can only be started once; create a new host to restart.");

            Interlocked.Exchange(ref _running, 1);
            _thread.Start();
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _running, 0) != 0)
                _wakeSignal.Set();

            if (_thread.IsAlive)
                _thread.Join();
        }

        public bool TryEnqueueCommand(IAuraCommand command)
        {
            ThrowIfDisposed();
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (!TryReserveCommandSlot())
                return false;

            _inbound.Enqueue(command);
            _wakeSignal.Set();
            return true;
        }

        public void StepOnce()
        {
            ThrowIfDisposed();
            ThrowIfFaulted();

            DrainAndStep();
        }

        void IDisposable.Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            Stop();
            _wakeSignal.Dispose();
        }

        private void Run()
        {
            try
            {
                var tickDuration = Stopwatch.Frequency / (double)_options.TickRate;
                double nextTickAt = Stopwatch.GetTimestamp();

                while (IsRunning)
                {
                    var now = Stopwatch.GetTimestamp();
                    if (now < nextTickAt)
                    {
                        var waitMilliseconds = (int)Math.Max(1, (nextTickAt - now) * 1000d / Stopwatch.Frequency);
                        _wakeSignal.WaitOne(waitMilliseconds);
                        continue;
                    }

                    var ticksRun = 0;
                    while (IsRunning && now >= nextTickAt && ticksRun < _options.MaximumCatchUpTicks)
                    {
                        DrainAndStep();
                        nextTickAt += tickDuration;
                        ticksRun++;
                        now = Stopwatch.GetTimestamp();
                    }

                    if (now >= nextTickAt)
                        nextTickAt = now + tickDuration;
                }
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _fault, exception);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
                _wakeSignal.Set();
            }
        }

        private void DrainAndStep()
        {
            while (_inbound.TryDequeue(out var command))
            {
                Interlocked.Decrement(ref _queuedCommands);
                _world.EnqueueCommand(command);
            }

            var tick = new SimulationTick(_nextTick++);
            _world.Step(new SimulationStep(tick, _options.FixedDeltaTime));
        }

        private bool TryReserveCommandSlot()
        {
            while (true)
            {
                var count = Volatile.Read(ref _queuedCommands);
                if (count >= _options.CommandQueueCapacity)
                    return false;

                if (Interlocked.CompareExchange(ref _queuedCommands, count + 1, count) == count)
                    return true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(AuraServerSimulationHost));
        }

        private void ThrowIfFaulted()
        {
            var fault = Fault;
            if (fault != null)
                throw new InvalidOperationException("The server simulation host faulted.", fault);
        }
    }
}
