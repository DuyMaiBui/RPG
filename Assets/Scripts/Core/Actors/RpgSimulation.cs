using System;
using System.Collections.Generic;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Actors
{

public enum ActorKind : byte
{
    Player = 1,
    Monster = 2,
}

public enum ActorVisualState : byte
{
    Idle = 0,
    Dead = 1,
}

public sealed class Actor
{
    public Actor(EntityId id, ActorKind kind, int maximumHealth, int attackPower)
    {
        if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        if (attackPower < 0) throw new ArgumentOutOfRangeException(nameof(attackPower));
        Id = id;
        Kind = kind;
        MaximumHealth = maximumHealth;
        CurrentHealth = maximumHealth;
        AttackPower = attackPower;
    }

    public EntityId Id { get; }
    public ActorKind Kind { get; }
    public int MaximumHealth { get; }
    public int CurrentHealth { get; private set; }
    public int AttackPower { get; }
    public bool IsDead => CurrentHealth == 0;

    public int ReceiveDamage(int damage)
    {
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        if (IsDead) return 0;

        var applied = Math.Min(damage, CurrentHealth);
        CurrentHealth -= applied;
        return applied;
    }
}

public sealed class ActorRegistry
{
    private readonly List<Actor> _actors = new();
    private readonly List<int> _generations = new();
    private readonly Stack<int> _freeIndices = new();

    public EntityId Spawn(ActorKind kind, int maximumHealth, int attackPower)
    {
        var index = _freeIndices.Count > 0 ? _freeIndices.Pop() : _actors.Count;
        if (index == _actors.Count)
        {
            _actors.Add(null);
            _generations.Add(0);
        }

        var id = new EntityId(index, _generations[index]);
        _actors[index] = new Actor(id, kind, maximumHealth, attackPower);
        return id;
    }

    public bool TryGet(EntityId id, out Actor actor)
    {
        if (id.Index < 0 || id.Index >= _actors.Count || _generations[id.Index] != id.Generation || _actors[id.Index] == null)
        {
            actor = null!;
            return false;
        }

        actor = _actors[id.Index]!;
        return true;
    }

    public bool Destroy(EntityId id)
    {
        if (!TryGet(id, out _)) return false;
        _actors[id.Index] = null;
        _generations[id.Index]++;
        _freeIndices.Push(id.Index);
        return true;
    }

    public ActorSnapshot[] CreateSnapshot()
    {
        var snapshots = new List<ActorSnapshot>(_actors.Count);
        foreach (var actor in _actors)
        {
            if (actor == null) continue;
            snapshots.Add(new ActorSnapshot(actor.Id, actor.Kind, actor.CurrentHealth, actor.MaximumHealth,
                actor.IsDead ? ActorVisualState.Dead : ActorVisualState.Idle));
        }

        return snapshots.ToArray();
    }
}

public sealed class RpgSimulationState
{
    public ActorRegistry Actors { get; } = new();
    public Dictionary<PlayerId, EntityId> PlayerActors { get; } = new();
    public List<PresentationSignal> Signals { get; } = new();

    public void BeginTick() => Signals.Clear();
}

public sealed class AttackCommand : ISimulationCommand
{
    public AttackCommand(EntityId target) => Target = target;
    public EntityId Target { get; }
    public ushort TypeId => 1;
}

public readonly struct ActorSnapshot
{
    public ActorSnapshot(EntityId entity, ActorKind kind, int currentHealth, int maximumHealth, ActorVisualState visualState)
    {
        Entity = entity;
        Kind = kind;
        CurrentHealth = currentHealth;
        MaximumHealth = maximumHealth;
        VisualState = visualState;
    }

    public EntityId Entity { get; }
    public ActorKind Kind { get; }
    public int CurrentHealth { get; }
    public int MaximumHealth { get; }
    public ActorVisualState VisualState { get; }
}

public enum PresentationSignalKind : byte
{
    Damaged = 1,
    Died = 2,
}

public readonly struct PresentationSignal
{
    public PresentationSignal(PresentationSignalKind kind, EntityId entity, EntityId source, int value)
    {
        Kind = kind;
        Entity = entity;
        Source = source;
        Value = value;
    }

    public PresentationSignalKind Kind { get; }
    public EntityId Entity { get; }
    public EntityId Source { get; }
    public int Value { get; }
}

public sealed class WorldFrameUpdate : ISimulationUpdate
{
    public const ushort MessageTypeId = 1;
    public WorldFrameUpdate(ActorSnapshot[] actors, PresentationSignal[] signals)
    {
        if (actors == null) throw new ArgumentNullException(nameof(actors));
        if (signals == null) throw new ArgumentNullException(nameof(signals));
        Actors = ((ActorSnapshot[])actors.Clone()).AsMemory();
        Signals = ((PresentationSignal[])signals.Clone()).AsMemory();
    }

    public ushort TypeId => MessageTypeId;
    public ReadOnlyMemory<ActorSnapshot> Actors { get; }
    public ReadOnlyMemory<PresentationSignal> Signals { get; }
}

public sealed class ActorDamaged : ISimulationEvent
{
    public ActorDamaged(EntityId source, EntityId target, int damage)
    {
        Source = source;
        Target = target;
        Damage = damage;
    }

    public EntityId Source { get; }
    public EntityId Target { get; }
    public int Damage { get; }
}

public sealed class ActorDied : ISimulationEvent
{
    public ActorDied(EntityId source, EntityId target)
    {
        Source = source;
        Target = target;
    }

    public EntityId Source { get; }
    public EntityId Target { get; }
}

public sealed class RpgSimulationApplication : ISimulationApplication<RpgSimulationState>
{
    public void BeginTick(SimulationContext<RpgSimulationState> context, SimulationTick tick) => context.State.BeginTick();

    public void HandleCommand(SimulationContext<RpgSimulationState> context, SessionContext session, in ClientCommandEnvelope command)
    {
        if (command.Payload is not AttackCommand attack ||
            !context.State.PlayerActors.TryGetValue(session.Player, out var attackerId) ||
            !context.State.Actors.TryGet(attackerId, out var attacker) ||
            !context.State.Actors.TryGet(attack.Target, out var target) ||
            attacker.IsDead || target.IsDead || attacker.Id == target.Id)
            return;

        var damage = target.ReceiveDamage(attacker.AttackPower);
        if (damage == 0) return;

        context.Publish(new ActorDamaged(attacker.Id, target.Id, damage));
        if (!target.IsDead) return;

        context.Publish(new ActorDied(attacker.Id, target.Id));
        context.Defer(state => state.Actors.Destroy(target.Id));
    }

    public void Tick(SimulationContext<RpgSimulationState> context, SimulationTick tick) { }

    public void HandleEvents(SimulationContext<RpgSimulationState> context, IReadOnlyList<ISimulationEvent> events)
    {
        foreach (var simulationEvent in events)
        {
            switch (simulationEvent)
            {
                case ActorDamaged damaged:
                    context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.Damaged, damaged.Target, damaged.Source, damaged.Damage));
                    break;
                case ActorDied died:
                    context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.Died, died.Target, died.Source, 0));
                    break;
            }
        }
    }

    public ISimulationUpdate CreateUpdate(SimulationContext<RpgSimulationState> context, SimulationTick tick)
        => new WorldFrameUpdate(context.State.Actors.CreateSnapshot(), context.State.Signals.ToArray());
}
}
