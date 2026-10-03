# AuraEngine remaining work — plan and handoff

This file tracks the agreed direction (physics kernel in C++, gameplay in C#)
and the concrete remaining work, so a fresh session can pick it up.

## Agreed architecture

- **Physics simulation runs in the C++ kernel** `Native/AuraEngine` (Jolt for
  3D, Box2D for 2D) behind the versioned C ABI, on every platform including the
  Editor and local play.
- **Gameplay stays C#**: `AuraSimulationWorld`, `EntityRegistry`, systems,
  commands, `IPhysicsWorld` contract, authoring and views.
- The C# `ManagedPhysicsBackend` and its `Managed*` solver are **removed**; C#
  only configures the kernel and reads state back.
- Kernel changes are validated with `Native/AuraEngine/run_kernel_tests.sh`
  (the equivalent in-Editor native suite crashes mono's JIT at the P/Invoke
  boundary, so it stays gated behind `#if AURA_NATIVE`).

## Done

### Phase 0 — test foundation (commit `5609f6f`)
- `Native/AuraEngine/tests/ManagedKernel` (SDK-style console project that
  compiles the engine-free `AuraEngine.*` sources and runs against `libaura`)
  plus `run_kernel_tests.sh`. **31/31 kernel tests pass**, covering bodies,
  shapes (box/sphere/capsule/cylinder/tapered/convex/mesh/height-field),
  joints (distance/fixed/hinge, 2D+3D), queries (raycast/all/overlap/sphere
  cast), triggers, contacts, conveyor surface velocity, snapshot/serializer,
  state hash, kinematic mover and determinism.
- Real bug fixed: Jolt `MeshShape` is winding-sensitive — a floor built with
  clockwise triangles silently ignored collisions. Fixed in headless + tests;
  the reference backend now rejects mesh/convex/height-field cleanly.
- `AGENTS.md` and `Docs/AuraEngine/Architecture.md` now state the C++ physics
  kernel / C# gameplay split.

### Phase 1.1 — native is the default
- `AuraSimulationInstance` defaults to the native backend and throws a clear
  error when `libaura` is missing (no silent managed fallback).
- `AuraMcpSession` uses the native backend.

## In progress (uncommitted)

### Phase 1.2 — remove the C# solver
- Deleted `Assets/Scripts/AuraEngine/Physics/Managed/**` (14 files) and 10
  `Managed*Tests.cs` files (coverage migrated into the kernel suite).
- `NetworkingTests` / `NetworkingTransportTests` switched from
  `ManagedPhysicsBackend` to `FakePhysicsBackend`.
- `AuraBackendKind` reduced to `Native` only.
- **Not yet verified**: Unity has not been recompiled since the deletion; the
  Editor has not run the EditMode suite. Compile + `run_tests` are the next
  step. Also confirm no prefab/scene referenced a deleted script GUID.

## Remaining work

### Phase 1.3 — parity and coverage
- Recompile in Unity, fix any dangling references (scenes/prefabs/asmdefs).
- Run EditMode + PlayMode suites green.
- Port any remaining managed-only behaviour gaps into the kernel suite.

### Phase 2 — kernel features (behind the C ABI + Jolt)
- `OverlapBox` / `OverlapCapsule` / `OverlapPoint` (currently return 0 on
  native) and the `*CastAll` family (currently stubs).
- Per-triangle material index in the baking pipeline (`AuraPhysicsMeshData` →
  ABI → Jolt `MeshShapeSettings`).
- Per-shape material and shape filters; active-edge options.
- Shape wrappers: `RotatedTranslatedShape`, `ScaledShape`,
  `OffsetCenterOfMassShape`, `MutableCompoundShape`.
- `AuraEngine.MCP`: resources + prompts (tools already exist).

### Phase 3 — networking on the kernel
- Snapshot baseline negotiation.
- UDP reliability layer (fragmentation/MTU, ordered/acked events, congestion).
- Per-entity rollback.

### Phase 4 — extended physics (Jolt samples → AuraEngine)
- Vehicles (wheeled first), SoftBody (strands/rods), Ragdoll/Rig,
  Water/buoyancy, Hair. Each: C ABI + Jolt adapter + C# contracts/authoring +
  kernel tests.

### Phase 5 — samples, server, tooling
- Server-shared / replay sample scene.
- §35 acceptance: one shared Domain run on Unity + server with native parity.
- Native server runtime packaging; long-run profiling/soak.
- Real MCP transport (beyond stdio).

### Phase 6 — external infrastructure (last)
- Licensed Unity CI runner for EditMode/PlayMode.
- Real Android (NDK) / iOS (Xcode) plugin builds (scripts + CI already exist,
  lint-verified only).

## Known constraints

- **Editor crash**: running the native P/Invoke test suite inside the Editor
  aborts the Editor in mono's JIT (`mono_save_seq_point_info`), unrelated to
  `libaura`. Kernel validation lives in `run_kernel_tests.sh`; the in-Editor
  suite stays `#if AURA_NATIVE` and is not referenced by the test asmdef.
- **Editor fragility**: long Pipeline test runs plus repeated commands can wedge
  the Editor main thread; restart the Editor when `editor_status` times out.
- **No NDK / Xcode SDK** on this machine: mobile builds are script-verified
  only.

## Verify loops

- Kernel: `./Native/AuraEngine/run_kernel_tests.sh` (`KERNEL_TESTS_OK`).
- Native ABI: `./Native/AuraEngine/build.sh` and `build_plugin.sh`
  (`AURA_HEADLESS_OK`).
- Unity: `unity command recompile` then
  `unity command run_tests --mode EditMode --filter AuraEngine.Tests --filter_type assembly`.
- PlayMode: `run_tests --mode PlayMode --async_tests true` then poll
  `test_status`.
