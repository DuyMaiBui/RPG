# Local-authoritative simulation architecture

## Status and scope

This document defines the first production-quality simulation slice. It runs
locally today, while preserving a strict client/server boundary so that a
remote transport can replace the local transport later.

In scope now:

- An engine-independent fixed-tick simulation host on a dedicated thread.
- A local client/server endpoint connected with bounded, thread-safe queues.
- Immutable, versioned command/update contracts.
- An OOP/hybrid RPG vertical slice: actors, attack, damage, death, and a
  full authoritative frame.
- A Unity bridge that renders updates without exposing Unity references to
  the simulation.

Explicitly out of scope now:

- Sockets, authentication, encryption, matchmaking, persistence, prediction,
  reconciliation, delta replication, interest management, and reconnect.
- ECS/archetype storage and generic component reflection.

Those features must be added behind the contracts in this document. They must
not require a Unity-facing gameplay rule to change.

## Module graph

```text
RPG.Unity
  -> RPG.Core
  -> RPG.Simulation.Runtime
  -> RPG.Simulation.Contracts

RPG.Core
  -> RPG.Simulation.Runtime
  -> RPG.Simulation.Contracts

RPG.Simulation.Runtime
  -> RPG.Simulation.Contracts
```

`RPG.Simulation.Contracts`, `RPG.Simulation.Runtime`, and `RPG.Core` are
`noEngineReferences` assemblies. `RPG.Unity` is the only Unity-facing layer.
VContainer registrations, UniTask lifecycle work, MessagePipe, and LitMotion
belong only in `RPG.Unity` and composition.

## Contract rules

### Identity

- `EntityId` contains an index and generation. An ID whose generation differs
  from the live slot is invalid and must be ignored/rejected.
- The server owns `PlayerId` and `SessionId`. A client command never claims a
  player identity; local mode binds an endpoint to a local session.
- `ClientSequence` is monotonically increasing per endpoint.
- `SimulationTick` is generated only by the host.

### Commands

Every command is immutable, contains only data and IDs, and has a stable,
manually assigned `TypeId`. Commands express intent, never authoritative
state. For example, `AttackCommand(Target)` is valid; `SetHealthCommand` is
not.

```csharp
public interface ISimulationCommand
{
    ushort TypeId { get; }
}

public readonly record struct ClientCommandEnvelope(
    ProtocolVersion Protocol,
    ClientSequence Sequence,
    SimulationTick ClientTick,
    ISimulationCommand Payload);
```

### Updates

The host emits immutable updates only. The first implementation sends a full
frame, which is authoritative and safe to coalesce for rendering.

```csharp
public interface ISimulationUpdate
{
    ushort TypeId { get; }
}

public readonly record struct ServerUpdateEnvelope(
    ProtocolVersion Protocol,
    SimulationTick ServerTick,
    ClientSequence LastProcessedClientSequence,
    ISimulationUpdate Payload);
```

The fields already support a future prediction/reconciliation implementation.
The local implementation must populate them even though it does not predict.

### Endpoint

```csharp
public interface ISimulationClient : IDisposable
{
    bool TrySend(in ClientCommandEnvelope command);
    bool TryRead(out ServerUpdateEnvelope update);
}
```

This is the only gameplay-facing client boundary. It intentionally has no
`World`, `Actor`, resolver, event callback, or service-resolution API.

## Runtime ownership and tick pipeline

`SimulationHost` owns exactly one simulation thread and all mutable simulation
state. Unity can enqueue commands and drain updates, but never reads or writes
the world.

The host uses a fixed rate, initially 30 Hz. Its options include tick rate,
maximum catch-up ticks, and bounded queue capacities; those are operational
calibration values, not gameplay configuration.

Each tick executes in this order:

1. Drain inbound commands.
2. Validate protocol, sequence, session, ownership, and command shape.
3. Dispatch accepted commands to their single authority handler.
4. Run registered gameplay steps in fixed phase/order.
5. Deliver typed internal events in a later phase.
6. Commit deferred structural changes: spawn, destroy, or tick registration.
7. Project a detached, immutable world frame.
8. Enqueue the server update and wait until the next tick.

No structural mutation may happen while a registry/list is being iterated.
Systems can mutate state they own, for example `Health.Current`, but must put
spawn/destroy/register/unregister work in `EntityCommandBuffer`.

## Application and feature composition

The generic runtime has one application root and explicit features:

```csharp
public interface ISimulationApplication
{
    void HandleCommand(
        SimulationContext context,
        SessionContext session,
        in ClientCommandEnvelope command);

    void Tick(SimulationContext context, SimulationTick tick);

    ServerUpdateEnvelope CreateUpdate(
        SimulationContext context,
        SimulationTick tick,
        ClientSequence lastProcessedClientSequence);
}

public interface ISimulationFeature
{
    void Register(SimulationApplicationBuilder builder);
}
```

`SimulationApplicationBuilder` is used once at startup. It rejects duplicate
command `TypeId`s and duplicate phase/order pairs. It is not a DI container and
does not resolve services. Composition constructs features using constructor
injection before registration.

The phase order is fixed:

```text
Command -> Simulation -> PostSimulation -> StructuralCommit -> Projection
```

Cross-feature interactions use either a domain operation with clear ownership
(`Actor.ReceiveDamage`) or a typed internal event (`ActorDied`). They do not
call peer systems directly and do not use MessagePipe in the domain.

## RPG OOP/hybrid model

`RPG.Core` uses aggregates and resolvers, not a pure ECS:

```text
Actor
  Health
  Position
  CombatState
  AbilityBook
  StatusEffectCollection

ActorRegistry
AttackResolver
StatusEffectTicker
SnapshotProjector
```

`Actor` protects state owned by the actor. `AttackResolver` coordinates two
actors. Features expose those operations through command handlers and tick
steps. Periodic work uses registered tick lists; no initial system scans every
actor unnecessarily. If profiling later identifies a data-heavy hotspot such
as thousands of projectiles, only that storage may become packed/data-oriented.

## Local transport and lifecycle

`LocalSimulationClient` and `SimulationHost` communicate through two bounded
thread-safe queues:

```text
Unity/client thread -> command queue -> simulation thread
simulation thread  -> update queue  -> Unity/client thread
```

- A full inbound queue makes `TrySend` return `false`; it never silently drops
  a command.
- Outbound full-frame updates may retain only the newest frame. Authoritative
  spawn/despawn state is always represented by the complete frame.
- Presentation signals are never authoritative; losing one cannot change game
  state.
- Disposing the host stops accepting work, cancels the loop, joins its thread,
  and clears owned queues. Unity must await/observe shutdown at its bridge.

## Unity bridge

The Unity bridge drains updates on the Unity main thread and then publishes
concrete update types through MessagePipe. `EntityViewRegistry` maps
`EntityId` to a pooled `ActorView`.

```text
Unity input -> command factory -> ISimulationClient.TrySend
server update -> Unity main-thread bridge -> MessagePipe -> ActorView
```

`ActorView` receives snapshots/signals and owns only rendering, view binding,
motion handles, and subscription cleanup. It never holds an `Actor` reference
or changes authoritative health/cooldowns. On unbind/pool return it cancels
motion, disposes subscriptions, and removes its entity mapping.

## Remote migration commitments

Later code replaces only `LocalSimulationClient` with a remote implementation.
The following are intentionally already present: protocol version, stable type
IDs, client sequence, client tick, server tick, processed-input acknowledgement,
server-owned session identity, and detached DTO-only payloads.

Prediction/reconciliation belongs in a future client layer. Replication
baseline/delta/interest management belongs in a future server layer. Socket,
authentication, encryption, rate limits, and reconnect belong in a transport
and session gateway. None belongs in `RPG.Core` or a MonoBehaviour.

## Delivery order and gates

1. Contracts: identity, envelopes, endpoint, version/sequence validation.
2. Runtime: host, fixed loop, queues, local endpoint, deferred commands.
3. Core vertical slice: two actors, attack, damage, death, full frame.
4. Unity bridge: composition, lifecycle, view registry, minimal actor view.
5. Hardening: cancellation, pooling, exception and assembly-boundary checks.

Every stage requires focused tests before the next stage begins. No socket,
authentication, prediction, replication delta, generic ECS, or speculative
abstraction is added before a concrete acceptance scenario requires it.

## Acceptance scenario

1. The scene starts a local simulation host and creates two authoritative
   actors.
2. Unity binds a view to each entity ID from the full frame.
3. Unity sends one `AttackCommand` through `ISimulationClient`.
4. The simulation thread validates and applies authoritative damage.
5. Unity receives a later frame and renders the changed health.
6. On death, the actor disappears from the frame and its view unbinds safely.
7. Scene unload stops the simulation thread; no subscription, tween, or pooled
   view remains bound to the entity.
