# RPG agent instructions

## Project facts and architecture

This is a Unity project. Read `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` for current versions and dependencies. Project code currently starts under `Assets/Scripts/Core` and `Assets/Scripts/Combat`, with assemblies `RPG.Core` and `RPG.Combat`. Inspect their actual references before changing dependencies; do not assume Combat already references Core.

## Required coding direction

Use dependency injection, UniTask for Unity-facing asynchronous work, and LitMotion for presentation tweens. VContainer is the preferred DI container for this project. These are chosen conventions, not proof of installed packages: verify the manifest and package source before writing calls; add a missing package only as part of an authorized implementation that needs it. Custom ECS and ZLinq are not automatically required by this decision. Project instructions take precedence over the older global `unity-ecs-stack` convention.

Gameplay core is plain C#: no MonoBehaviour inheritance, scene lookup, Unity lifecycle, Transform, GameObject, LitMotion or container resolution. Constructor injection expresses dependencies; consumer-owned interfaces are appropriate at real module/engine boundaries even with one current implementation. Keep VContainer registrations, entry points and LifetimeScope in the composition/integration layer. MonoBehaviours are thin bridges for serialized references, input/physics callbacks and rendering/lifecycle handoff. Do not remove engine components needed by Unity or create a manager MonoBehaviour for each service.

Physics simulation runs in the C++ kernel (`Native/AuraEngine`, Jolt 3D / Box2D 2D) exposed through the versioned C ABI, on every platform including the Editor. C# owns gameplay orchestration, entity/command/system flow, authoring and views, and drives the kernel through `IPhysicsWorld`; there is no separate C# solver to duplicate kernel behaviour. Validate kernel changes with the standalone `Native/AuraEngine/run_kernel_tests.sh` suite (the equivalent in-Editor native suite crashes mono's JIT at the P/Invoke boundary); keep the C ABI structs and the managed `Native*` mirrors in lockstep.

Gameplay views must be authored into prefabs and scenes. Serialized prefab and
scene references are the source of truth for view hierarchy, components,
materials, UI, anchors and required child objects. Runtime gameplay code must
not create view GameObjects, add view components, build Canvas/UI hierarchies,
or create sprites/materials/prefabs to repair missing authoring. Runtime may
only instantiate a serialized prefab reference and bind simulation state to its
existing components. Missing references are authoring/configuration errors and
must be reported rather than silently repaired at runtime.

Prefer a small reusable prefab composition: establish a base actor/view prefab,
extract reusable component prefabs for shared UI/effects, and use prefab
variants or nested prefab instances for faction/content differences. Do not
duplicate nearly identical prefab hierarchies; before creating a new prefab,
search for an existing base, component prefab or variant extension point.
Keep overrides intentional and localized so prefab changes propagate safely.

Reusable modules own cohesive rules and public contracts, with explicit acyclic assembly dependencies. Core contracts do not reference bridges or composition. Cross-feature access uses narrow contracts, not concrete peer services. Use Unity-free assembly definitions (`noEngineReferences`) when extracting truly engine-independent modules. Do not create empty layers or interfaces for every class. Preserve existing paths and metadata; extract reusable packages when an actual consumer exists.

UniTask belongs in asynchronous application/integration services; deterministic core steps stay synchronous with explicit delta time where needed. Pass CancellationToken from the owning operation/scope; cancellation on pool return/disable is distinct from destruction. Await operations or deliberately observe errors at the event boundary. LitMotion owns visual transitions in presenters/adapters; cancel motion on replacement, scope end or pool return. Visual completion must not own authoritative damage, rewards or saved state.

Read [the module policy](Docs/Architecture.md) for dependency direction and lifetime rules. The selected stack is intentional: Ponytail's stdlib-first heuristic must not replace it with coroutines, service locators or handmade tween loops.

## Coding conventions

- Put exactly one class, struct, interface, enum, record, or delegate in each
  production `.cs` file. The filename must match that type (generic parameters
  are omitted from the filename); do not nest an additional declared type.
- Organize related types with folders, namespaces and assemblies rather than
  grouping declarations into a catch-all source file. This keeps Git history,
  reviews and ownership precise.
- Apply the same convention to test sources and helpers.
- When a class or struct implements an interface, implement every interface
  member explicitly (`InterfaceName.Member`). Keep the concrete type's public
  surface limited to behavior that belongs to the concrete abstraction; use
  explicit casts at interface boundaries so call sites reveal which contract
  they consume. Marker interfaces have no members to implement.

## Reuse before implementation

Search existing code, tests and installed package APIs before adding a subsystem. For substantial missing capabilities, inspect maintained upstream solutions and official documentation; compare fit, license, Unity/IL2CPP/AOT compatibility, allocations, dependencies and maintenance. Prefer adapting an existing API to duplicating it. Record the chosen reuse source or a short reason custom code is needed; trivial fixes do not need a research report.

Ponytail is a coding discipline inside Codex, not a replacement execution engine. Minimize unnecessary code while fully satisfying the task. Required DI boundaries, module separation, cancellation, error handling and meaningful tests are not optional abstractions. Never omit requirements or validation merely to shorten a diff. Keep project-specific conventions authoritative over imported generic examples.

## Editor and assets

Use only the configured MCP server named `unity`, with Unity CLI as a fallback to the same Pipeline integration. Do not introduce another Unity MCP or bridge. Target this project explicitly when driving an Editor. Read the installed Unity CLI skill for command syntax and caller labels.

Check `unity status --format json` before Editor automation. `com.unity.pipeline` provides the integration. No ready instance is a connection blocker, not evidence that code compiles or that a scene is correct. Inspect Pipeline status and compiler logs before diagnosing the cause.

Use Editor APIs for serialized scene, prefab and asset changes. Preserve `.meta` files and GUIDs; let Unity generate metadata for new assets. Do not edit generated `Library`, `Temp`, `.csproj` or `.sln` files. Package changes must remain explicit and relevant to the task.

For scene and prefab setup, prefer a minimal plain-text change to the
serialized asset when the format and GUID/reference integrity are understood;
otherwise use the configured Unity MCP/CLI. Inspect the resulting asset and
references after either path. Do not call MCP merely for edits that can be
safely and completely represented as text.

When authoring a new view, first identify the reusable base prefab and nested
component prefabs it should compose. Treat repeated serialized hierarchies as
a review finding unless the differences cannot be represented by a variant,
serialized configuration, or a separate nested component prefab.

An editor-only C# static helper may be created for a narrowly scoped MCP
authoring operation when text editing cannot preserve Unity serialization. It
must be clearly named and documented as an AI-agent/editor tool, have no
runtime assembly dependency, and be removed when no longer needed. Removing a
one-shot helper requires asking the user first; never delete it implicitly.
Inspect what such a helper changes before running it and validate the produced
asset afterward.

## Work and validation

Inspect git status and preserve existing user changes. Follow the object's scene/prefab references to the actual runtime owner before fixing visible behavior. Write a short design for cross-module or persistent-data changes; routine fixes can proceed directly. Use the project skills selectively, not all at once.

### Required delivery loop

For every implementation task, complete this loop in order:

```text
Brainstorm -> plan -> implement -> test -> review -> handoff
```

- Brainstorm and plan enough to identify ownership, acceptance criteria,
  dependencies, failure modes and the smallest complete slice. Ask the user a
  concise clarification question before implementation when a missing decision
  or requirement would materially change the result.
- Implement the planned behavior in the correct code boundary. Do not use a
  tip, workaround, fake result, test-only bypass, swallowed error, or
  presentation-only patch to make output appear correct while the underlying
  implementation remains wrong.
- Test proportionately, then review the actual diff and runtime/compiler
  evidence against the acceptance criteria.
- A failing compile, test, review finding, lifecycle check, or unmet acceptance
  criterion restarts the loop at diagnosis and implementation. Fix the root
  cause in code, re-run the relevant validation, and repeat review until the
  requested outcome is genuinely complete.
- Report passed checks and any explicitly unverified scope. Never represent
  static analysis, a partial check, or a workaround as successful completion.

For logic changes, run focused Edit Mode tests when useful; use Play Mode for lifecycle/scene behavior and manual visual checks for visible output. Report exactly which checks ran and what remains unverified. Static validation is not a Unity compile or runtime test. Avoid test files that merely repeat implementation details.

## Agent roles

Project role definitions are in `.codex/config.toml`: `unity_architect`, `unity_gameplay`, `unity_reviewer`, and `unity_qa`. Roles inherit the session model. For simple tasks work locally. Delegate only when the user asks for parallel/agent work and useful independent tasks exist. Assign explicit file ownership before concurrent editing. Only one agent should mutate the live Unity Editor at a time; reviewers and architects inspect without changing it. Main agent integrates and validates results.

Use the user's language for explanations. Continue authorized work without repetitive confirmation; ask only for missing decisions that materially affect the result.
