---
name: rpg-simulation-framework
description: Extend, review, or integrate the project’s local-authoritative simulation framework, including its engine-free contracts, fixed-tick host, OOP gameplay modules, and Unity view bridge boundary.
---

# RPG simulation framework

Use this skill when changing the local simulation host, simulation contracts,
RPG simulation rules, or the Unity bridge that consumes simulation updates. Do
not use it for an unrelated Unity feature that does not cross this boundary.

## Read first

- `Docs/Simulation-Architecture.md` for the public boundary and delivery
  rules.
- `Assets/Scripts/Simulation/Contracts/Protocol/` for protocol contracts.
- `Assets/Scripts/Simulation/Runtime/Host/` for thread and tick ownership.
- `Assets/Scripts/Core/Actors/` for the current RPG vertical slice and
  snapshot projection.

## Invariants

- `RPG.Simulation.Contracts`, `RPG.Simulation.Runtime`, and `RPG.Core` remain
  engine independent and have `noEngineReferences`.
- Only the simulation thread mutates simulation/RPG state. Unity sends
  `ClientCommandEnvelope`s and renders `ServerUpdateEnvelope`s only.
- Commands carry intent and immutable data. Player/session identity belongs to
  the host session, never to an untrusted command payload.
- Keep `ProtocolVersion`, stable command/update `TypeId`, client sequence,
  server tick, and processed-sequence acknowledgement populated. They are the
  migration seam for future remote transport and reconciliation.
- Structural changes during iteration go through
  `SimulationContext<TState>.Defer`; do not modify a registry while iterating.
- Gameplay uses OOP aggregates and focused resolvers. Do not introduce ECS,
  reflection-based components, a global event bus, or a service locator unless
  a measured use case requires it.
- Core events are typed `ISimulationEvent`s processed by the host phase. Keep
  MessagePipe, VContainer, UniTask, LitMotion, MonoBehaviour, and Unity types
  in the Unity adapter/composition layer.
- Keep one declared type per `.cs` file, named after that type. Use the
  Contracts, Runtime and Actors folders to express the relationship instead of
  accumulating protocol or gameplay types in one source file.
- Implement interface members explicitly (`InterfaceName.Member`) so concrete
  APIs and contract APIs remain visually distinct at call sites.
- Gameplay view objects are authored in prefabs/scenes. The Unity bridge may
  instantiate serialized prefab references and bind existing components, but
  must not create GameObjects, add view components, or build UI/rendering
  hierarchies at runtime. Missing authoring is a configuration error.
- Prefer a reusable base view prefab with nested component prefabs and prefab
  variants for content/faction differences. Do not create duplicate actor/UI
  hierarchies when an existing prefab composition or serialized configuration
  can express the variation.

## Extending gameplay

1. Define a stable command/update/event type in the correct contract or RPG
   module; its payload must contain only data and IDs.
2. Add the domain aggregate operation or resolver in `RPG.Core`.
3. Register/route it through `ISimulationApplication<TState>` without direct
   system-to-system calls.
4. Project resulting authoritative state into a detached snapshot. Presentation
   signals may augment a frame but must never be the only representation of
   authoritative state.
5. Add one focused Edit Mode test that fails for the intended regression.

## Lifetime and validation

- A host owns its thread. Dispose must stop it and join it before dependencies
  are released.
- Inbound commands are bounded and must report rejection rather than silently
  dropping input. Outbound full frames may coalesce to the newest frame.
- Entity IDs use generation; test stale IDs whenever lifecycle/reuse changes.
- For core changes, run focused Edit Mode tests. For Unity bridge/pooling/view
  changes, also run a Play Mode lifecycle check and confirm all subscriptions
  and motions are canceled on unbind/pool return.
- Prefer plain-text serialized scene/prefab edits when their GUID/reference
  integrity is understood; otherwise use the configured Unity MCP/CLI and
  inspect the resulting asset. Any temporary editor/static C# helper must be
  explicitly AI-agent/editor-only, isolated from runtime assemblies, reviewed
  before execution, and retained until the user approves its removal.
- Do not add networking, authentication, prediction, delta replication, or
  empty adapter abstractions until their concrete implementation is authorized.
