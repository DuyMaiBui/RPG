# AuraEngine implementation status

## Implemented and validated

Managed, engine-independent assemblies under `Assets/Scripts/AuraEngine`:

- `AuraEngine.Core` (M0): identity, tick/step, math, body/shape/material/layer
  definitions, collision matrix, query filter/hit, events, body state, result
  codes, deterministic FNV-1a state hasher.
- `AuraEngine.Physics` (M2): `IPhysicsBackend`, `IPhysicsWorld`, `IPhysicsQuery`,
  `IPhysicsEventSource`, capabilities, the `NullPhysicsBackend` fallback, and
  `ManagedPhysicsBackend` — a deterministic, engine-independent rigid-body
  engine for 3D and `Plane2D`:
  - Bodies: static/dynamic/kinematic with mass + diagonal inertia tensor,
    angular velocity and quaternion orientation integration.
  - Shapes: sphere/circle, oriented box (OBB), capsule, cylinder (capsule
    approximation) and compound bodies.
  - Broadphase: sweep-and-prune over union AABBs.
  - Narrowphase: sphere/OBB/capsule pairs plus OBB-OBB SAT (15 axes in 3D, 4 in
    2D) with multi-point manifolds; box SAT uses a small contact margin so
    exactly-touching bodies still collide.
  - Solver: sequential impulses with warm starting, per-contact friction cone
    (two tangents in 3D), restitution threshold, Baumgarte position correction
    and configurable `AuraSolverSettings` (iteration counts, slop, sleep).
  - Joints: Distance, Fixed and Hinge.
  - Sleeping/wake with per-body thresholds.
  - Collision matrix and per-body layer/mask filtering; collision/trigger
    enter/exit events.
  - Queries: raycast against sphere/OBB/capsule, and the full cast/overlap
    family.
- `AuraEngine.Simulation` (M1/M5/M6/M8): `AuraSimulationWorld`, `EntityRegistry`,
  systems, command handlers, event capture/remap, query remap, state hash.
- `AuraEngine.Serialization` (M15): snapshot, binary state serializer
  (versioned, little-endian), replay recorder/player, versioned replay file
  format, and initial-state validation before replay (`AuraReplayPlayer`
  rejects a world whose initial snapshot hash differs from the recording).
- `AuraEngine.Server` (M9): bounded-queue headless fixed-tick host.
- `AuraEngine.Physics.Native` (M7): P/Invoke binding over the frozen C ABI
  (`NativeMethods`, `NativePhysicsWorld`, `NativePhysicsBackend`) that validates
  the ABI version and runs the same body/step/query flow through `libaura`.
  Correctness of the ABI itself is proven by
  `Native/AuraEngine/src/headless` (`AURA_HEADLESS_OK`).
- Jolt backend (M3): `Native/AuraEngine/src/physics/jolt` implements the shared
  native `IWorld` over Jolt Physics (bodies, gravity, contacts, sensors,
  raycast, overlap, state). Shapes: box, sphere, native **cylinder**
  (`JPH::CylinderShape`), capsule, **plane** (`JPH::PlaneShape`),
  **tapered capsule** (`JPH::TaperedCapsuleShape`), **tapered cylinder**
  (`JPH::TaperedCylinderShape`), **convex hull** (`JPH::ConvexHullShape`) and
  **triangle mesh** (`JPH::MeshShape`, static bodies only), plus compound
  bodies. Convex hull / mesh vertex+index data and plane normal / tapered top
  radius cross the C ABI (`AuraShapeDesc.vertices/indices/planeNormal/topRadius`,
  ABI v3) and are marshalled by `NativePhysicsWorld` with pinned arrays.
  `./Native/AuraEngine/build_jolt.sh` fetches nothing
  (run `fetch_jolt.sh` once), builds `libaura.dylib` + headless with
  `-DAURA_USE_JOLT=ON`, and copies the plugin to
  `Assets/Plugins/AuraEngine/macOS`. `AuraSimulationInstance` selects it with
  `AuraBackendKind.Native` (falls back to the managed engine if the plugin is
  missing).
- Box2D backend (M4): `Native/AuraEngine/src/physics/box2d` implements the same
  `IWorld` over Box2D 3.1 for `Plane2D` (bodies, circle/polygon/capsule, sensors,
  contacts, raycast, overlap, state). `./Native/AuraEngine/build_editor.sh`
  compiles both backends into one `libaura`; `CreateWorldImpl` dispatches by
  mode (3D -> Jolt, 2D -> Box2D), so a single plugin serves both demo scenes.

Unity host layer `AuraEngine.Unity` (M10/M11/M13): `AuraSimulationInstance`
(fixed-tick host), `AuraViewRegistry`, `AuraPhysicsView`, `AuraTransformSync`,
`AuraEventDispatcher`, `IAuraPhysicsEventReceiver`, authoring components
(`AuraPhysicsBodyAuthoring` plus sphere/box/capsule/cylinder/convex/mesh
collider authoring), collider gizmos, `AuraPhysicsMaterialAsset`, and custom
inspectors that show validation and live simulation stats. Authoring validates
each shape against the active backend capabilities and reports unsupported
shapes instead of silently dropping them.

Editor tooling: draggable scene handles for collider center/size/radius, a
layer dropdown backed by `AuraPhysicsLayers`, and an inline collision-matrix
editor drawn directly in the `AuraPhysicsLayers` inspector and in the
`AuraSimulationInstance` inspector (no separate window, raw data fields hidden).
`AuraPhysicsLayers` uses fixed layer slots with index 0 fixed to `Default`.
Only slots with a non-empty name exist: unnamed slots are forced to mask `0`
(cannot interact), are excluded from the collision matrix, and cannot be
selected on body authoring. Naming a slot adds it to the matrix with default
interactions; clearing the name removes it. Editing names or the pair grid
feeds `AuraWorldDefinition.CollisionMatrix` at runtime. Authoring inspectors derive
from Odin's `OdinEditor` and use Odin attributes when the package is present
(`#if ODIN_INSPECTOR`), with a standard Unity `Editor` fallback otherwise.

Unity demo (see `Demo.md`): `AuraEngine.Demo` sample assembly, an editor scene
builder, and two authored scenes (`AuraDemo3D`, `AuraDemo2D`) demonstrating 3D
and 2D bodies, collisions, a kinematic mover, triggers, raycast queries and a
live HUD, all on the managed reference backend without Unity physics.

Native contract (M7): `Native/AuraEngine/include/aura/*.h` freeze the C ABI and
the backend interface. See `Native-Abi.md`.

## Production hardening (edge-case pass)

Bugs found by the edge-case suite and fixed:

- `EntityRegistry.AttachBody`: replacing a body with an invalid definition left a
  stale body id in the slot. Now the old body is cleared before the new body is
  created, so a failed attach leaves no dangling handle.
- `AuraStateSerializer.Deserialize`: a hostile/oversized body count overflowed
  the int length check and attempted a huge allocation. The bound check now uses
  64-bit arithmetic and rejects the buffer as `AuraException`.
- `AuraServerSimulationHost.Start`: starting twice or restarting after `Stop`
  leaked a `ThreadStateException`. Restart is now rejected with
  `InvalidOperationException`; a new host is required.
- `AuraSimulationWorld`: mutating/query/state operations after `Dispose` now
  throw `ObjectDisposedException` instead of touching cleared storage.
- Command dispatch and system iteration now use snapshots, so a handler/system
  that enqueues commands or mutates the system list cannot corrupt iteration or
  cause reentrant dispatch.
- `EntityRegistry` keeps an O(1) body-to-entity map instead of scanning slots on
  every event/query remap.
- `AuraEventDispatcher` sized its read from `PendingEventCount`; previously more
  than 64 events in a tick were silently truncated.
- State hash and snapshot capture sort body states by entity id so ordering is
  independent of backend internal allocation order.
- Jolt contact callbacks run on its worker threads; the native event buffer and
  trigger map are now guarded by a mutex (`aura_jolt_world.cpp`), fixing a data
  race that corrupted the heap and crashed the Editor on Play.
- The Jolt world lowers `mPenetrationSlop` to 0.002 (from Jolt's 0.02) so resting
  bodies do not visibly sink into the ground; measured tapered-cylinder rest
  offset dropped from 0.02 to 0.002.
- The project's URP quality levels now use the 3D renderer (`Renderer3D.asset`)
  instead of the 2D renderer, so 3D Lit materials receive lighting and shadows
  instead of rendering unlit.

## Not yet implemented

- Platform builds of `libaura`: `build_plugin.sh` covers the desktop hosts;
  `build_android.sh` (NDK) and `build_ios.sh` (Xcode) add Android/iOS with
  CMake toolchains, and CI builds them on `macos-latest` with uploaded
  artifacts. The mobile scripts are lint/parse verified here but not built
  locally (no NDK / Xcode SDK on this machine).
- Runtime triangle-mesh collision on the managed backend (M12, documented
  limitation): the managed engine has no mesh narrowphase, so it rejects
  `AuraShapeType.TriangleMesh`; the Jolt backend implements `MeshShape`
  (static bodies only) and `ConvexHullShape`. The Unity baking pipeline
  (`AuraMeshColliderBaker` writes `AuraPhysicsMeshData`) and the physics
  material assets exist and feed the shape geometry.
- Networking/prediction (M18) ships `AuraEngine.Networking` (engine-free): a
  length-framed input/snapshot/ack codec, `IAuraTransport` with an in-memory
  loopback pair, `AuraLockstepBuffer`, an authoritative `AuraNetServer`, a
  predicting `AuraNetClient`/`AuraPredictedWorld` with re-simulation
  reconciliation, and `AuraPredictionReplay`. Real transports
  (`UdpAuraTransport` over `System.Net.Sockets`, `WebSocketAuraTransport` over
  `System.Net.WebSockets`) are implemented and tested on loopback.
  `AuraSnapshotDelta` sends quantised per-entity deltas against a baseline, and
  `AuraLagCompensator` keeps a bounded authoritative history for server-side
  rewind. Still open: per-entity rollback, snapshot baseline negotiation and a
  production UDP reliability layer (MTU fragmentation, congestion).
- `AuraEngine.MCP` (§33) ships a minimal stdio JSON-RPC server
  (`initialize`, `tools/list`, `tools/call`) with tools
  `world_create`, `body_create_box`, `body_create_sphere`, `world_step`,
  `world_raycast`, `world_state`. It drives the managed backend and is verified
  end-to-end from a standalone .NET probe; structured content, resources,
  prompts and transport beyond stdio are not implemented.

## Known environment issues

- **Correction (heap corruption, fixed):** the Editor aborts described below were
  attributed to mono's JIT, but libaura really corrupted the heap. `DestroyJoint`
  and `DestroyBody` released a Jolt constraint that `RemoveConstraint` had already
  freed (use-after-free, `aura_jolt_joints.cpp` / `aura_jolt_world.cpp`), and
  `ApplySurfaceVelocity` mutated shared state from Jolt worker threads without the
  event mutex. The damage is silent and surfaces in a later unrelated `malloc`
  (often the JIT). Both are fixed; `AURA_GMALLOC=1 ./run_kernel_tests.sh` runs the
  suite under Guard Malloc, where the old code aborts. Re-test the in-Editor native
  suite before treating the JIT explanation below as still valid.

- Running the native P/Invoke test suite inside the Editor aborts the Editor.
  The isolated failing test is `NativeBackend_SaveState_RestoresBodyState`,
  which calls `AuraSimulationWorld.SaveState` -> `NativePhysicsWorld.SaveState`.
  The captured native stack is `abort -> malloc_vreport ->
  ___BUG_IN_CLIENT_OF_LIBMALLOC_POINTER_BEING_FREED_WAS_NOT_ALLOCATED ->
  monoeg_g_ptr_array_free -> mono_save_seq_point_info -> mini_method_compile`:
  the crash is inside mono's JIT (freeing the sequence-point array while
  compiling the method), not inside `libaura` (the identical serialize/snapshot
  flow passes in the headless harness). It is an Editor/JIT environment issue,
  so `NativeBackendTests` stays behind `#if AURA_NATIVE` and the asmdef does not
  reference `AuraEngine.Physics.Native`; the native path is validated through
  the headless ABI harness and standalone .NET probes. Compile-time and
  load-time issues were hardened regardless (`IsAvailable` also catches
  `BadImageFormatException`; `SaveState` documents the native-free probe path).

## Validation evidence

- Unity Editor recompile of all `AuraEngine.*` assemblies: succeeded
  (`compilationFailed: false`, 0 console errors).
- Unity Test Runner (EditMode), assembly `AuraEngine.Tests`: **119 passed, 0
  failed** after the T2/T3/T4/M17 work (the extra test is the server-host soak
  test).
- Standalone execution against the engine sources using Unity's bundled .NET
  SDK, including the native P/Invoke binding test against a locally built
  `libaura`: **108 passed, 0 failed**. Coverage now includes box stacking,
  rotated-OBB raycast, distance/fixed/hinge joints, 2D joints, sleeping and
  deterministic box stacking.
- Native split (Phase 0.5): the Jolt backend and C ABI are split into per-feature
  translation units (see `Parallel-Plan.md` §7b). Reference and Jolt/Box2D
  builds both print `AURA_HEADLESS_OK`; the 3D state hash is unchanged
  (`0x0f005e29d728e0ff`).
- Native C ABI (reference): `./Native/AuraEngine/build.sh` builds `libaura` +
  `aura_headless` and the headless run prints `AURA_HEADLESS_OK` (ball rests at
  y=0.5, raycast hit, 2 bodies, state hash). On the reference backend the
  character check is skipped (`[char] unsupported (code=4) skipped`).
- Height field (Jolt): headless prints `[hf] box_y=1.5000 ok=1` (a box rests on
  a flat 4x4 field at y = 1); other backends print `[hf] unsupported (code=3) skipped`.
- Snapshot v2 (Jolt): headless prints `snapshot size=168 written=168 serialize=0
  deserialize=0`; the character check round-trips a snapshot
  (`[char] ... snapshot=1`), covering both body and character state.
- `AuraEngine.MCP` (§33): a standalone .NET probe drives `initialize`,
  `tools/list`, `world_create`, `body_create_box`/`body_create_sphere`,
  `world_step` (tick 120, hash `6ba59e090144c33d`) and `world_state`.
- Character controller (Jolt): headless prints
  `[char] rest_y=0.9000 walk_peak_y=1.2020 stepped_y=0.9000 jump_peak_y=2.2162
  climbed=1 jumped=1`, i.e. the capsule mounts a 0.3 m step and jumps on command.
- Performance benchmark (§28): `./Native/AuraEngine/run_bench.sh` builds
  `aura_bench` and prints the step/raycast scaling summary ending
  `AURA_BENCH_OK`.
- Character Unity authoring: `AuraCharacterAuthoring` + `AuraSimulationInstance`
  character lifecycle compile into `AuraEngine.Unity`; Unity EditMode
  `AuraEngine.Tests` **118 passed, 0 failed** after the change. Runtime
  PlayMode use of the authoring component is not yet exercised by a demo scene.
- Query filters (Jolt + reference): headless prints
  `[query] ground_d=10.000 platform_d=6.500 ignored_d=10.000 layer0=1 layer1=1
  ignore=1 overlap0=0 overlap1=1`, i.e. `AuraQueryFilter.layerMask` and the
  ignored-body flag select the right body on raycast and overlap.
- Native C ABI (Jolt): `./Native/AuraEngine/build_jolt.sh` builds the same ABI
  over Jolt Physics and prints `AURA_HEADLESS_OK` (ball y=0.48, raycast hit
  9.02, 2 bodies).
- Jolt in the Editor: the 3D `AuraDemo3D` scene runs on Jolt
  (`AuraSimulationInstance` backend = Native, HUD shows `Backend:
  AuraEngine.Native`), 20 entities/bodies with stacking, a kinematic platform,
  a trigger and a raycast hit — verified from a captured Game View frame.
- Box2D in the Editor: the 2D `AuraDemo2D` scene runs on Box2D (same
  `Backend: AuraEngine.Native`), 10 entities/bodies on the plane with a
  kinematic platform and a raycast hit — verified from a captured Game View
  frame. Headless also prints `[2d] ball y=0.4999`, `[2d] raycast hit=1`.
- Unity Play Mode with the generated demo scenes: 3D reached the expected
  entity/body count (15) and showed dynamic/kinematic collisions plus a raycast
  hit on the ground; 2D (10 entities) showed the same on the plane with a
  raycast hit on a body. Verified from captured Game View frames (HUD tick,
  counts, state hash, raycast hit).

Coverage includes identity/generation reuse, 1k-2k entity and body churn,
capacity/empty/truncated buffers, serializer overflow/version/truncation,
disposal guards, host lifecycle, command reentrancy, event ordering and volume,
query truncation and filters, replay with commands, and insertion-order
independent state hashing.
