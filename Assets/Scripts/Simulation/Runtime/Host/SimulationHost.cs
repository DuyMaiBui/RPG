using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class SimulationHost<TState> : IDisposable
    {
        private readonly ISimulationApplication<TState> _application;
        private readonly SimulationContext<TState> _context;
        private readonly SimulationOptions _options;
        private readonly ConcurrentQueue<QueuedCommand> _inbound = new();
        private readonly AutoResetEvent _wakeSignal = new(false);
        private readonly object _updateGate = new();
        private readonly ProtocolVersion _protocol;
        private readonly Thread _thread;
        private ServerUpdateEnvelope _latestUpdate;
        private bool _hasLatestUpdate;
        private long _queuedCommandCount;
        private long _lastProcessedSequence = -1;
        private long _tick;
        private int _running;
        private int _disposed;
        private Exception _fault;

        public SimulationHost(
            TState state,
            ISimulationApplication<TState> application,
            SimulationOptions options = null,
            ProtocolVersion? protocol = null)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _options = options ?? new SimulationOptions();
            _options.Validate();
            _protocol = protocol ?? ProtocolVersion.Current;
            _context = new SimulationContext<TState>(state, 1f / _options.TickRate);
            _thread = new Thread(Run) { IsBackground = true, Name = "RPG Simulation" };
        }

        public bool IsRunning => Volatile.Read(ref _running) == 1;
        public Exception Fault => Volatile.Read(ref _fault);

        public void Start()
        {
            ThrowIfDisposed();
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
                throw new InvalidOperationException("Simulation host is already running.");

            _thread.Start();
        }

        public bool TryEnqueue(SessionContext session, in ClientCommandEnvelope command)
        {
            ThrowIfDisposed();
            ThrowIfFaulted();
            if (!IsRunning || !TryReserveInboundSlot())
                return false;

            _inbound.Enqueue(new QueuedCommand(session, command));
            _wakeSignal.Set();
            return true;
        }

        public bool TryReadLatest(out ServerUpdateEnvelope update)
        {
            ThrowIfFaulted();
            lock (_updateGate)
            {
                if (!_hasLatestUpdate)
                {
                    update = default;
                    return false;
                }

                update = _latestUpdate;
                _hasLatestUpdate = false;
                return true;
            }
        }

        void IDisposable.Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            Interlocked.Exchange(ref _running, 0);
            _wakeSignal.Set();
            if (_thread.IsAlive)
                _thread.Join();

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
                        RunSingleTick();
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

        private void RunSingleTick()
        {
            var tick = new SimulationTick(_tick++);
            _context.ResetForNextTick();
            _application.BeginTick(_context, tick);

            while (_inbound.TryDequeue(out var queued))
            {
                Interlocked.Decrement(ref _queuedCommandCount);
                if (!queued.Command.Protocol.IsCompatibleWith(_protocol) ||
                    queued.Command.Sequence.Value <= _lastProcessedSequence)
                    continue;

                _application.HandleCommand(_context, queued.Session, queued.Command);
                _lastProcessedSequence = queued.Command.Sequence.Value;
            }

            _application.Tick(_context, tick);
            var events = _context.DrainEvents();
            if (events.Count > 0)
                _application.HandleEvents(_context, events);

            _context.CommitDeferredActions();
            var update = new ServerUpdateEnvelope(
                _protocol,
                tick,
                new ClientSequence(_lastProcessedSequence),
                _application.CreateUpdate(_context, tick));

            lock (_updateGate)
            {
                _latestUpdate = update;
                _hasLatestUpdate = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(SimulationHost<TState>));
        }

        private void ThrowIfFaulted()
        {
            var fault = Fault;
            if (fault != null)
                throw new InvalidOperationException("Simulation host faulted.", fault);
        }

        private bool TryReserveInboundSlot()
        {
            while (true)
            {
                var count = Volatile.Read(ref _queuedCommandCount);
                if (count >= _options.InboundQueueCapacity)
                    return false;

                if (Interlocked.CompareExchange(ref _queuedCommandCount, count + 1, count) == count)
                    return true;
            }
        }

    }
}
