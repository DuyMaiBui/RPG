using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{

public sealed class SimulationOptions
{
    public SimulationOptions(int tickRate = 30, int maximumCatchUpTicks = 5, int inboundQueueCapacity = 1024)
    {
        TickRate = tickRate;
        MaximumCatchUpTicks = maximumCatchUpTicks;
        InboundQueueCapacity = inboundQueueCapacity;
    }

    public int TickRate { get; }
    public int MaximumCatchUpTicks { get; }
    public int InboundQueueCapacity { get; }

    public void Validate()
    {
        if (TickRate <= 0) throw new ArgumentOutOfRangeException(nameof(TickRate));
        if (MaximumCatchUpTicks <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumCatchUpTicks));
        if (InboundQueueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(InboundQueueCapacity));
    }
}

public readonly struct SessionContext
{
    public SessionContext(SessionId id, PlayerId player)
    {
        Id = id;
        Player = player;
    }

    public SessionId Id { get; }
    public PlayerId Player { get; }
}

public interface ISimulationApplication<TState>
{
    void BeginTick(SimulationContext<TState> context, SimulationTick tick);
    void HandleCommand(SimulationContext<TState> context, SessionContext session, in ClientCommandEnvelope command);
    void Tick(SimulationContext<TState> context, SimulationTick tick);
    void HandleEvents(SimulationContext<TState> context, IReadOnlyList<ISimulationEvent> events);
    ISimulationUpdate CreateUpdate(SimulationContext<TState> context, SimulationTick tick);
}

public sealed class SimulationContext<TState>
{
    private readonly List<ISimulationEvent> _events = new();
    private readonly List<Action<TState>> _deferredActions = new();

    internal SimulationContext(TState state, float fixedDeltaTime)
    {
        State = state;
        FixedDeltaTime = fixedDeltaTime;
    }

    public TState State { get; }
    public float FixedDeltaTime { get; }

    public void Publish(ISimulationEvent simulationEvent)
    {
        if (simulationEvent == null) throw new ArgumentNullException(nameof(simulationEvent));
        _events.Add(simulationEvent);
    }

    public void Defer(Action<TState> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        _deferredActions.Add(action);
    }

    internal IReadOnlyList<ISimulationEvent> DrainEvents() => _events;

    internal void ResetForNextTick()
    {
        _events.Clear();
        _deferredActions.Clear();
    }

    internal void CommitDeferredActions()
    {
        foreach (var action in _deferredActions)
            action(State);

        _deferredActions.Clear();
    }
}

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
        if (!IsRunning || !TryReserveInboundSlot())
            return false;

        _inbound.Enqueue(new QueuedCommand(session, command));
        _wakeSignal.Set();
        return true;
    }

    public bool TryReadLatest(out ServerUpdateEnvelope update)
    {
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

    public void Dispose()
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

    private readonly struct QueuedCommand
    {
        public QueuedCommand(SessionContext session, ClientCommandEnvelope command)
        {
            Session = session;
            Command = command;
        }

        public SessionContext Session { get; }
        public ClientCommandEnvelope Command { get; }
    }
}

public sealed class LocalSimulationClient<TState> : ISimulationClient
{
    private readonly SimulationHost<TState> _host;
    private readonly SessionContext _session;

    public LocalSimulationClient(SimulationHost<TState> host, SessionContext session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _session = session;
    }

    public bool TrySend(in ClientCommandEnvelope command) => _host.TryEnqueue(_session, command);
    public bool TryRead(out ServerUpdateEnvelope update) => _host.TryReadLatest(out update);
    public void Dispose() { }
}
}
