# AuraEngine feature test plan

Status after P0 (ABI 12). Counts come from the repository: 172 kernel cases,
223 EditMode tests (including 20 in-Editor native backend tests and 8 authoring fixtures),
23 demo scenes with 45+ assertions, a 25-cycle lifecycle check. Nothing here is a claim that a layer
is already covered; the "Gap" column says what is missing.

## 1. Test layers

| Layer | What it proves | How to run | Today |
|---|---|---|---|
| L1 Kernel suite | C ABI, Jolt and Box2D behavior, determinism, handles | `Native/AuraEngine/run_kernel_tests.sh`, or build to a private dir (it copies the dylib into `Assets/`), plus `AURA_GMALLOC=1` | 172 cases, pass normally and under Guard Malloc |
| L2 EditMode | Engine-free Core, managed contracts, pure demo helpers | `unity command run_tests --mode editor --filter AuraEngine.Tests --filter_type assembly` | 223 pass; native backend tests re-enabled and stable over 10 runs |
| L3 Authoring | Each `Aura*Authoring` builds the right definition and registers/unregisters cleanly | EditMode tests that instantiate the component under an `AuraSimulationInstance` (explicit `CreateWorld()`, `OnEnable` called by reflection) | 8 fixtures; `Start`/`FixedUpdate`/view sync still untested |
| L4 Scene assertions | Each demo scene does what its name says | `Tools/AuraSmoke/aura_smoke.sh` evaluates `expectations.json` | 23 scenes pass; rules for rest scenes are generic |
| L5 Lifecycle and soak | Repeated play/stop, enable/disable, scene reload, long runs | `Tools/AuraSmoke/aura_lifecycle.sh` (25 cycles, `Aura_LiveWorldCount`) | lifecycle done; soak/fuzz still open |
| L6 Platform and perf | Builds on every target, frame-time and body-count budgets | CI and `run_bench.sh` | macOS dylib only; Android/iOS scripts unverified |

Rules for every layer: a new test must fail on the code it guards (prove it by running it
against the old behavior once), and anything touching native memory also runs under Guard
Malloc. The display must stay awake during L4/L5 runs, otherwise the Editor is throttled and
CLI calls hang.

## 2. Regression guards that must never be removed

Each of these was a real defect found in this work; the guard is the test that fails on the old code.

| Defect | Guard |
|---|---|
| Jolt joint use-after-free (`Release()` after `RemoveConstraint`) | `joint_destroy_paths_are_memory_safe` under Guard Malloc |
| Conveyor surface-velocity data race | gap: no test; add a TSan or stress case (see 4.1) |
| Vehicle authoring passed pitch/roll 0 | `vehicle_rejects_zero_pitch_roll` |
| Cloth triangles culled (winding) | gap: only a screenshot; add a mesh-winding unit test on `BuildTriangles` |
| Box2D ignored requested mass | `control_impulse_2d` and `control_force_torque_2d` with mass 1 |
| Kinematic target overshoot at low frame rate | `kinematic_target_is_consumed_by_one_step_3d/2d` |
| 2D mover lost its body reference after migration | gap: scene assertion (platform moves) |

## 3. Feature matrix and missing tests

| Feature | Kernel | EditMode / authoring | Scene | Gap to close |
|---|---|---|---|---|
| Rigid bodies, shapes, materials | yes | contracts only | Core, Demo 2D/3D | per-shape rest height assertions in scenes; convex/mesh/height-field have no demo |
| Body control (velocity, impulse, teleport, motion type, layer, enable) | 40 cases | none | Sandbox2D, RagdollHit3D | stale-handle behavior through authoring; disable/enable of an authored body mid-play |
| Joints 3D (fixed, hinge, slider, distance, spring) | yes | none | Articulation3D | authoring build tests (anchors with non-unit scale: a 2D scene already broke on this) |
| Joints 3D extended (SixDof, cone, swing-twist, pulley, gear, rack) | 17 `d_` cases | none | Constraints3D | SixDof soft limits, per-axis rotation position motors beyond X; Path is unimplemented |
| Joints 2D (wheel, mouse, rope, hinge, slider) | 17 `b2_` + joint cases | none | Sandbox2D | authoring validation (rope needs distance > 0), mouse joint with a static anchor body |
| Breakable joints and feedback | yes | none | Sandbox2D (monitor) | the monitor's UnityEvent firing; feedback while bodies sleep |
| Queries 3D | yes | yes | none | filter and capacity cases through `AuraSimulationWorld` |
| Queries 2D (overlap, casts) | 17 `b2_` | none | none | Box2D raycast `categoryBits` miss (known); managed `OverlapShape/ShapeCast` ignore local pose |
| Character 3D | yes | none | Character3D | slope, step, jump, platform-carry thresholds as scene assertions |
| Character 2D and one-way | 17 `char2d`/`oneway` | `AuraJumpAssist` | Platformer2D | hero reaching each platform; drop-through; ledge peak falls back through a one-way (known) |
| Vehicles 3D | 2 cases | none | Articulation3D | drive-and-stay-in-arena assertion; tuning ranges; no tank/motorcycle |
| Soft body, water | yes | none | AdvancedSoftBody3D, Water | buoyancy equilibrium height assertion; water in 2D uses mass: re-check after the Box2D mass fix |
| Ragdoll (kernel) | yes | none | Humanoid3D, AdvancedRagdoll3D | scenes rest and stay still; kernel ragdoll cannot take impulses (document or fix) |
| Force fields, world gravity | 43 `E` cases | none | Space3D | orbit radius stays within a tolerance (measured: 0.2 m); wind drag approaches target |
| CCD | yes | none | none | a projectile scene proving no tunneling |
| Time scale and hit-stop | yes | `AuraTimeStepper`, `AuraHitStop` | Space3D | scene assertion that the world scale dips and recovers |
| Cloth and hair | n/a (Core) | solver + collider tests | Cloth/Hair scenes | mesh bounds assertions in the probe (it only sees transforms) |
| Snapshot, rollback, determinism | yes | yes | none | fields and force fields are not snapshotted: add a test that documents the reset-after-rollback contract |
| Networking | n/a | yes | none | out of scope here |

## 4. New test work, in priority order

### P0, do first (small, high value)
1. **Re-enable the in-Editor native tests.** `NativeBackendTests` is behind `AURA_NATIVE` because the Editor
   aborted in the JIT; that was heap corruption in libaura, now fixed. Remove the define guard, run the suite
   in the Editor 10 times in a row, and keep it only if it never aborts.
2. **Per-scene assertions in `Tools/AuraSmoke`.** Replace MOVING/STATIC with a small expectations file per scene
   (for example Platformer2D: hero y reaches > 3.5; Space3D: all six orbit radii within 0.5 of start;
   Constraints3D: gear B rotates opposite A at half rate; Sandbox2D: crate leaves its start; RagdollHit3D:
   pelvis moves after the first blast). Include mesh bounds for cloth and hair.
3. **Authoring build tests (L3).** One EditMode/PlayMode fixture that creates an `AuraSimulationInstance` with the
   native backend and instantiates each authoring component with a non-unit scale and a rotated parent;
   assert the created definition (anchors in world space, axes, limits in radians) and that disable/enable
   leaves no handle behind. This is where the two scale bugs found in this work would have been caught.
4. **Lifecycle test (L5).** Play Mode is entered with domain reload disabled, so static state survives between
   sessions. Enter and exit Play Mode 25 times across three scenes, then assert the native world and handle
   counts return to zero and the console stays clean.

### P1
5. **Soak and fuzz under Guard Malloc.** A kernel driver (extend the agent's `stress.cpp` idea) that applies random
   create/destroy/step/mutate sequences on both backends for several minutes, with joints, force fields,
   characters and snapshots in the mix; fail on any abort. Add it to `run_kernel_tests.sh` as an opt-in mode.
6. **Thread-safety check.** ASan never started on this machine (dyld shared-cache scan takes over 10 minutes),
   so use ThreadSanitizer or UBSan builds of the Jolt contact path with a conveyor and many bodies.
7. **Frame-rate independence.** Run every kinematic and character scene with a forced low frame rate
   (`Application.targetFrameRate = 8`) and assert the same end state as at 60 fps within a tolerance.
8. **Determinism across runs** for each new feature: same inputs give the same state hash (force fields,
   characters 2D, joints with motors).

### P2
9. **Visual regression.** Store one reference screenshot per scene and compare with a loose tolerance;
   catches culling and framing regressions like the invisible cloth.
10. **Performance budget.** `run_bench.sh` plus a Play Mode scene with 1,000 bodies; fail when the step time
    exceeds an agreed budget.
11. **Platform builds.** CI job for the macOS dylib with the kernel suite under Guard Malloc; build-only jobs for
    Android and iOS (their scripts are only lint-checked today); verify the Windows/Linux build scripts.
12. **Snapshot completeness.** Decide whether force fields, motors and gravity scale should be part of snapshots;
    until then the contract test in the matrix pins the current behavior.

## 5. Exit criteria for "features verified"

- L1: all kernel cases pass, normally and under Guard Malloc; no case disabled.
- L2/L3: all EditMode tests pass; every authoring component has a build test.
- L4: every scene has an assertion file and passes; zero console errors.
- L5: 25 enter/exit cycles and a 10-minute soak with no abort and no handle leak.
- L6: kernel suite green in CI; frame and step budgets documented and met.

## 6. How to run the current checks

```text
# Kernel (private build dir, never overwrites the Assets dylib)
cmake -S Native/AuraEngine -B /private/tmp/aura_build -G Ninja -DCMAKE_BUILD_TYPE=Release \
  -DAURA_USE_JOLT=ON -DAURA_USE_BOX2D=ON -DAURA_BUILD_PLUGIN=ON -DAURA_BUILD_TESTS=ON
cmake --build /private/tmp/aura_build
dotnet build -c Release Native/AuraEngine/tests/ManagedKernel/ManagedKernelTests.csproj
cp /private/tmp/aura_build/libaura.dylib Native/AuraEngine/tests/ManagedKernel/bin/Release/net8.0/
dotnet Native/AuraEngine/tests/ManagedKernel/bin/Release/net8.0/managed_kernel_tests.dll
# Editor
unity command run_tests --mode editor --filter AuraEngine.Tests --filter_type assembly
# Scenes (Editor running, display awake)
Tools/AuraSmoke/aura_smoke.sh /tmp/aura_smoke
```

## 7. Progress log

### P0 (done)
1. Native backend tests re-enabled in the Editor: 20 tests, 10 consecutive runs, 0 failures, no abort.
2. Per-scene assertions: all 23 scenes pass. Writing them exposed two scene defects (balls leaving the
   Demo2D ground; Space3D dust crossing an orbit) and one harness lesson: editing C# or bumping the ABI while
   a smoke run is in progress invalidates it.
3. Authoring fixtures: exposed three missing mode guards (fixed), a zero-gravity bug in `AuraWorldDefinition`
   (a zero vector was replaced by -9.81; fixed, with tests) and pinned two behaviors to review:
   collider geometry ignores `lossyScale` while joint anchors apply it, and a joint enabled before its bodies
   after the world exists logs an error and is never retried.
4. Lifecycle: 25 Play/Stop cycles over four scenes: exactly one native world while playing, none after stopping,
   0 console errors, Editor memory flat (-72 MB). `Aura_LiveWorldCount` (ABI 12) makes this checkable.

### Open items found while doing P0
- Decide whether collider geometry should follow `lossyScale` like joint anchors do.
- Retry a joint whose bodies register after the world exists.
- `AuraSimulationInstance.Register` silently skips authoring when the backend lacks the capability.
