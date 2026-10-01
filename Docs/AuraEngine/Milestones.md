# AuraEngine milestone gap analysis

Status against the implementation plan. "Done" means implemented and validated
in this repository; "Native" means the piece requires the Jolt/Box2D or a
platform build that is not available here.

| Milestone | Status | Evidence / remaining |
|---|---|---|
| M0 Contracts | Done | `AuraEngine.Core` identity/math/definitions/query/event/state/result |
| M1 Native Core | Partial | Kernel exists as the engine-independent C# `AuraEngine.Simulation` (`AuraSimulationWorld`, `EntityRegistry`, command/event buffers, state hash). A C++ kernel mirrors it in `Native/AuraEngine`. |
| M2 Physics Abstraction | Done | `AuraEngine.Physics` interfaces + capabilities; `NullPhysicsBackend`; and `ManagedPhysicsBackend`, a full deterministic 3D/2D rigid-body engine (OBB SAT, sequential-impulse solver, joints, sleeping) |
| M3 Jolt Backend | Done | `src/physics/jolt` adapter over the shared `aura::IWorld`; built with `AURA_USE_JOLT=ON`; headless passes on Jolt and the Unity 3D demo runs on Jolt in the Editor. |
| M4 Box2D Backend | Done | `src/physics/box2d` adapter over the shared `aura::IWorld` (Box2D 3.1, Plane2D); built into the same `libaura` and verified running the 2D demo in the Editor. |
| M5 Query System | Done | Full family in `IPhysicsQuery` (raycast, sphere/capsule/box/shape cast, point/sphere/box/capsule/shape overlap) |
| M6 Physics Events | Done | Collision/trigger enter/exit, stable ordering, entity/body/shape ids |
| M7 Native C ABI | Done (reference) | `Native/AuraEngine` compiles to `libaura` and `aura_headless`; headless runs only through the C ABI and prints `AURA_HEADLESS_OK`. `build.sh` is the build. Managed P/Invoke binding `AuraEngine.Physics.Native` is implemented and validated from the standalone runner (`NativeBackendTests`). |
| M8 AuraEngine.Managed | Done | Simulation/physics/query/events/state exposed without Unity or P/Invoke |
| M9 C# Server Host | Done | `AuraEngine.Server` bounded-queue fixed-tick host |
| M10 Unity Bridge | Done | `AuraSimulationInstance`, view registry, transform sync, event dispatcher |
| M11 Unity Authoring | Done | Body + sphere/box/capsule/cylinder/convex/mesh collider authoring, materials, capability validation |
| M12 Baking Pipeline | Partial | `AuraPhysicsMeshData` asset + `AuraMeshColliderBaker` menu bake mesh vertices/indices to a backend-independent asset with a stable asset id; material assets baked via `AuraPhysicsMaterialAsset`. Convex hull cooking and runtime mesh collision need native backends. |
| M13 Unity Presentation | Done | `AuraPhysicsView` + sample HUD/ray probe/kinematic mover |
| M14 Debug & Editor Tooling | Done | Collider gizmos + scene handles, `AuraPhysicsDebugDraw`, `AuraSimulationInstanceEditor` (runtime stats + inline collision matrix), layer dropdown |
| M15 Serialization/Replay/Determinism | Done | Binary snapshot, sorted deterministic hashing, replay recorder/player, versioned replay file, initial-state validation |
| M16 Cross-platform Build & CI | Partial | `.github/workflows/ci.yml` builds and runs the native ABI test on Linux/macOS and adds a `bench` job that builds and runs `aura_bench` (§28). The Unity EditMode job is documented but needs a licensed runner; the Jolt+Box2D plugin path is built locally, not in CI. Platform matrix (Android/iOS/ARM64) not built. |
| M17 Production Server | Native/Partial | Headless C# host exists; native server runtime and load/soak testing pending. |
| M18 Networking/Prediction | Partial | `AuraEngine.Networking`: input/snapshot/ack codec, `IAuraTransport` + in-memory loopback, `AuraLockstepBuffer`, authoritative `AuraNetServer`, predicting `AuraNetClient`/`AuraPredictedWorld` with re-simulation reconciliation. No real (UDP/WebSocket) transport or rollback yet. |

## Plan sections beyond the milestone list

- §22 Batch data transfer — done on both managed (`CopyBodyStates`/`CopyEvents`) and native (`Aura_CopyBodyStates`/`Aura_CopyEvents`).
- §27 Test architecture — managed and Unity layers covered (97 tests); contract/native/cross-runtime layers are partial (headless native smoke test only).
- §28 Performance tests — `Native/AuraEngine/src/bench` (`aura_bench`, built by `AURA_BUILD_BENCHMARKS=ON` / `run_bench.sh`) reports stepping throughput and raycast rates over a scalable pyramid; the CI `bench` job runs it on Linux/macOS.
- §29 Memory rules — documented in `Architecture.md`; managed/native ownership respected.
- §30 Error handling — managed `AuraResult`/`AuraException`; native returns `AuraResultCode`; no exceptions cross the ABI.
- §31 Backend validation — authoring + inspectors report unsupported shapes and invalid definitions.
- §32 AI-agent docs — `Architecture.md`, `Native-Abi.md`, `Demo.md`, `Status.md`, this file.
- §33 `AuraEngine.MCP` — **missing** (planned last, after the API freezes).
- §34 Sample projects — 3D and 2D Unity scenes; no server-shared or replay sample scene yet.
- §35 Major acceptance gate (same Domain on Unity + server) — partially: Unity and the headless host already run the same `AuraEngine.Simulation` + `ManagedPhysicsBackend`; a shared sample Domain and native-parity run are not yet demonstrated.
- §36 Release strategy / §39 Definition of Done — tracked here and in `Status.md`.

## Highest-value remaining work

1. Convex hull cooking + runtime triangle-mesh collision in the managed backend
   (M12) once the managed solver supports them (Jolt already provides both).
2. Platform build/CI matrix (M16): Windows/Linux/Android/iOS plugin builds and a
   licensed Unity runner. The native ABI and benchmark jobs already run in CI.
3. A real network transport + snapshot delta/lag compensation for M18; the
   in-memory prediction/reconciliation core is already in place and tested.
