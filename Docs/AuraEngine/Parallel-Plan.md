# AuraEngine parallel implementation plan

Goal: let several tracks (agents) implement Jolt features concurrently without
touching each other's files. The only serial phase is freezing the contracts
and the extension seams; after that every track owns its own files and
assemblies.

## 0. Principles

1. **Freeze contracts first.** All shared interfaces, ABI structs and enums that
   more than one track needs are written once by the integrator (Phase 0). After
   that, no track edits a shared file.
2. **One writer per file.** Every file has exactly one owning track. See the
   reserved-file registry in section 5.
3. **Per-feature files.** Each feature lives in its own file(s) and its own
   translation unit, discovered through a seam, never by editing a shared
   switch.
4. **Separate ABI headers/functions per feature.** A feature never edits
   `aura_types.h`/`aura_capi.cpp` after Phase 0; it adds `aura_<feature>.h` and
   `aura_capi_<feature>.cpp`.
5. **One Unity Editor mutator.** Only one agent drives the live Editor at a
   time; reviewers/QAs inspect read-only. Editor tooling track runs last per
   wave.
6. **Green-gate per track.** A track may merge only when its focused tests pass
   and the whole solution still compiles.

## 1. Extension seams frozen in Phase 0 (serial)

These are declared once so tracks never change shared files again. They may be
no-op initially.

Managed (`AuraEngine.Physics`):
- `IPhysicsWorld` gains feature accessors:
  `IPhysicsJoints Joints { get; }`, `IPhysicsCharacters Characters { get; }`,
  `IPhysicsSerialization Serialization { get; }`,
  `IPhysicsContacts Contacts { get; }`.
- New interfaces: `IPhysicsJoints` (create/destroy typed joints),
  `IPhysicsCharacters` (create/move/step a character),
  `IPhysicsSerialization` (save/restore/hash), `IPhysicsContacts`
  (read manifolds).
- `NullPhysicsBackend`, `ManagedPhysicsBackend` and `NativePhysicsBackend`
  return no-op implementations.

Native ABI:
- Split headers: `aura/aura_joints.h`, `aura/aura_character.h`,
  `aura/aura_snapshot.h`, `aura/aura_contacts.h` (own version constants).
- `aura_backend_select.cpp` unchanged; each feature gets a cpp
  (`aura_capi_joints.cpp`, ...) that includes the shared internal
  `aura_world.h`.
- Jolt features live in `src/physics/jolt/aura_jolt_joints.cpp`, etc., sharing
  the `JoltWorld::Impl` through a small internal header
  `aura_jolt_internal.h` (created in Phase 0) instead of editing
  `aura_jolt_world.cpp`.

Core (`AuraEngine.Core`, owned by integrator):
- `AuraShapeType` extended with the final shape set; `AuraShapeGeometry` gains
  optional wrapper fields. One-time change.

## 2. Shared files (integrator only)

`aura_types.h`, `aura_world.h`, `aura_capi.cpp`, `aura_jolt_world.cpp`,
`aura_jolt_internal.h`, `AuraShapeGeometry.*`, `AuraShapeType.cs`,
`asmdef` files, `CMakeLists.txt`, `build_editor.sh`.

Any change here is a serial "contract" commit done before a wave starts, never
during a track.

## 3. Tracks

| Track | Owner role | Owns (files) | Produces | Depends on |
|---|---|---|---|---|
| T1 Body config (R1) | gameplay | `AuraPhysicsBodyDefinition`, `AuraBodyFreezeFlags`, `AuraBodyCollisionDetection`, `NativeBodyDesc`, Jolt body block, `AuraPhysicsBodyAuthoring` | body tuning API + authoring | Phase 0 |
| T2 Shapes (R2) | gameplay | `AuraShapeGeometry` shape set*, `Aura*ColliderAuthoring`, `aura_jolt_shapes.cpp`, Jolt `MakeShape` hook | wrappers + HeightField + MutableCompound | Phase 0 |
| T3 Constraints (R3/R4) | gameplay | `IPhysicsJoints`, `AuraJointDescription`, `Aura*JointAuthoring`, `aura_joints.h`, `aura_capi_joints.cpp`, `aura_jolt_joints.cpp` | full joint set + motors/limits | Phase 0 |
| T4 Queries/contacts (R6) | gameplay | `IPhysicsContacts`, `aura_contacts.h`, `aura_capi_contacts.cpp`, `aura_jolt_contacts.cpp`, closest-point query | manifolds, conveyor, filters | Phase 0 |
| T5 Character (R5) | gameplay | `IPhysicsCharacters`, `aura_character.h`, `aura_capi_character.cpp`, `aura_jolt_character.cpp`, `AuraCharacterController` | capsule controller | Phase 0 |
| T6 Serialization (R7) | gameplay | `AuraEngine.Serialization` additions, `aura_snapshot.h`, `aura_capi_snapshot.cpp`, `aura_jolt_snapshot.cpp` | snapshot/replay/determinism | Phase 0 |
| T7 Vehicles (R8) | gameplay | `AuraEngine.Vehicles` (new asm), `aura_vehicle.h`, `aura_jolt_vehicle.cpp`, authoring | wheeled/tank/motorcycle | T3 |
| T8 SoftBody (R9) | gameplay | `AuraEngine.SoftBody`, `aura_softbody.h`, `aura_jolt_softbody.cpp` | strands/rods | T4 |
| T9 Ragdoll (R10) | gameplay | `AuraEngine.Ragdoll`, `aura_rig.h`, `aura_jolt_rig.cpp` | rigs | T3, T9 |
| T10 Water (R11) | gameplay | `AuraEngine.Water`, `aura_water.h`, `aura_jolt_water.cpp` | buoyancy | T4 |
| T11 Editor tooling (R13) | architect | `AuraEngine.Editor` only | inspectors, gizmos, handles, debug views | T1..T4 |
| T12 Tests/QA | qa | `AuraEngine.Tests` per-feature files | contract/backends/edge tests | each track |
| T13 Review | reviewer | none (read-only) | review findings | each track |

\* `AuraShapeGeometry` header is a shared file; the shape *set* addition is a
Phase-0 contract change, the per-shape behaviour lives in T2's own files.

### T3 status (implemented)

T3 landed without the extra split files named above; the code lives in the
existing modules:

- Core: `AuraJointType` (distance, fixed, hinge, point, slider, cone,
  swing-twist, pulley, spring, six-dof), `AuraJointDefinition` with factories,
  `AuraJointId`, `AuraPhysicsCapabilities.Joints`.
- Managed: `ManagedJoint` + `ManagedPhysicsWorld` solve distance, spring,
  point, fixed, hinge, slider (limits + motor) and cone.
- Native: `AuraJointDesc`/`AuraJointType` in `aura_types.h`, `Aura_CreateJoint`
  / `Aura_DestroyJoint` / `Aura_HasJoint` in `aura_capi.cpp`, Jolt constraints
  in `aura_jolt_world.cpp`, Box2D constraints in `aura_box2d_world.cpp`,
  `NativeJointDesc`/`NativeMethods`/`NativePhysicsWorld` on the managed side.
- Unity: `AuraJointAuthoring` registers with `AuraSimulationInstance`, which
  creates joints after bodies when the backend advertises `Joints`.

### T4 status (partial)

Contact manifolds are implemented for all backends: `AuraContact` +
`Aura_CopyContacts` in the ABI, Jolt/Box2D/Reference implementations, the
managed `AuraSimulationWorld.CopyContacts`, and `NativePhysicsWorld.Contacts`.
Conveyor surface velocity is implemented via `Aura_SetSurfaceVelocity`
(Jolt `mRelativeLinearSurfaceVelocity` with awake maintenance, Box2D linear
velocity, managed tangential friction target) exposed as
`AuraSimulationWorld.SetSurfaceVelocity`. The managed backend now derives body
friction/restitution from shape materials when the body material is unset (the
body-level material was previously always zero, silently disabling friction).
Query filtering now matches the reference backend: Jolt's raycast/raycast-all/
overlap pass an `ObjectLayerFilter` built from `AuraQueryFilter.layerMask` and an
`IgnoreSingleBodyFilter` for the flags-bit-2 ignored body (headless `[query]`
check passes on both backends: layer-0 hit at 10.0, layer-1 hit at 6.5, ignored
hit back to ground, overlap 0/1 by layer). Still open in T4: per-triangle
friction (the shape material friction is now resolved from shapes on both
backends), shape filters, active-edge options and a closest-point query (the
pinned Jolt version has no `NarrowPhaseQuery::GetClosestPoint`).

### T5 status (native)

The capsule character controller is implemented on the Jolt backend:
`AuraCharacterDesc`/`AuraCharacterState` and `Aura_CreateCharacter` /
`Aura_DestroyCharacter` / `Aura_GetCharacterState` / `Aura_MoveCharacter` in the
ABI, `JoltWorld` wrapping `CharacterVirtual`, `NativePhysicsWorld.Characters`,
and `AuraSimulationWorld` character accessors. The reference and Box2D backends
report characters as unsupported.

Movement now supports jump and stair climbing: a positive vertical component in
`desiredTranslation` is an explicit up command (overrides gravity that step),
otherwise gravity integrates while airborne and downward motion is clamped when
supported. `ExtendedUpdate` runs with `mWalkStairsStepUp = 0.6`,
`mWalkStairsMinStepForward = 0.35` and `mWalkStairsStepForwardTest = 0.2`, which
lets the capsule mount a 0.3 m step (headless `[char]` check: rest 0.9 -> walk
peak 1.202 -> jump peak 2.216). Arguments now live in
`aura_jolt_character.cpp`.

Unity authoring landed: `AuraCharacterAuthoring` (radius/height/mass/max slope
degrees, optional transform sync, `DesiredVelocity`) registers with
`AuraSimulationInstance`, which builds characters after bodies and ticks them
before `Step`. `AuraCharacterDefinition` gained optional `mass`/`maxSlopeAngle`
(default 0 keeps the native defaults) marshalled through `NativeCharacterDesc`.
Still open in T5: inner-body push, stair-step settings authoring UI and a native
enable flag for the (currently `#if AURA_NATIVE`-gated) Unity test assembly.

### T6 status (native + managed)

`IPhysicsSerialization.SaveState` / `RestoreState` are implemented for the Jolt
and managed backends (the Jolt path wraps `Aura_SerializeState` /
`Aura_DeserializeState`, the managed path writes body pose/velocity/sleep and
recomputes AABBs), and `AuraSimulationWorld.SaveState` / `RestoreState` expose
them at the world level. `AuraEngine.Serialization` already provides
`AuraSimulationSnapshot`, `AuraStateSerializer`, `AuraReplay` and
`AuraReplayPlayer`. Still open in T6: joint/character state in the native
stream, scene/entity resync after restore, and cross-backend snapshot portability.

### T2 status (partial)

Shape scaling is implemented as a baked `AuraShapeGeometry.Scaled` transform: the
half-extents/radius/height are multiplied and mesh vertices are copied and
multiplied (the source array is not mutated), so no ABI change is needed. Every
`AuraColliderAuthoring` gains a serialized `_scale` consumed by `ScaledGeometry`
(rendering, gizmos, handles and `BuildShape` all agree), and the editor handle
shows a scale gizmo for convex and mesh colliders. Still open in T2:
`HeightFieldShape`, runtime `ScaledShape`/`MutableCompoundShape`, per-triangle
materials and shape-level filters.






## 4. Waves

```text
WAVE 0 (serial, integrator)
  freeze interfaces + ABI split headers + Jolt internal header + asmdef stubs
        |
WAVE 1 (parallel)
  T1 Body config      T2 Shapes          T3 Constraints
  T4 Queries/contacts T5 Character       T6 Serialization
        |
WAVE 2 (parallel, needs T3/T4)
  T7 Vehicles   T8 SoftBody   T9 Ragdoll   T10 Water
        |
WAVE 3 (serial-ish)
  T11 Editor tooling for everything landed
  T12 QA/edge cases   T13 Review    cross-platform build (M16)
```

Within Wave 1 no track edits another track's files or the shared registry, so
they can run truly concurrently. Each track keeps the solution compiling by
returning no-op feature objects until its Jolt implementation lands.

## 5. Reserved-file registry (single writer)

| File / glob | Writer |
|---|---|
| `Native/.../include/aura/aura_types.h`, `aura_world.h`, `aura_capi.cpp` | integrator |
| `Native/.../src/physics/jolt/aura_jolt_world.*`, `aura_jolt_internal.h` | integrator (feature hooks) |
| `Native/.../src/physics/jolt/aura_jolt_shapes.cpp` | T2 |
| `aura_jolt_joints.cpp` / `aura_jolt_character.cpp` / `aura_jolt_contacts.cpp` / `aura_jolt_snapshot.cpp` | T3/T5/T4/T6 |
| `Assets/Scripts/AuraEngine/Core/Physics/AuraShapeGeometry.cs`, `AuraShapeType.cs` | integrator |
| `Assets/Scripts/AuraEngine/Physics/IPhysicsWorld.cs` | integrator |
| `Assets/Scripts/AuraEngine/Physics/Native/NativePhysicsWorld.cs`, `NativeMethods.cs` | integrator (feature no-ops) |
| `Assets/Scripts/AuraEngine/Unity/Aura*ColliderAuthoring.cs` | T2 |
| `Assets/Scripts/AuraEngine/Unity/Aura*JointAuthoring.cs` | T3 |
| `Assets/Scripts/AuraEngine/Editor/**` | T11 |
| `Assets/Tests/EditMode/AuraEngine.Tests/<Feature>*Tests.cs` | T12 |
| `CMakeLists.txt`, `build_editor.sh` | integrator |

## 6. Agent assignment and protocol

- **Integrator** (main agent) runs Wave 0, owns shared files, integrates merges,
  rebuilds `libaura`, and is the only one who mutates the live Unity Editor.
- **unity_gameplay** agents take one track each (T1..T10) with the file
  ownership above. They may not touch shared files; if a contract change is
  needed they file it to the integrator.
- **unity_architect** owns T11 (editor tooling) and reviews the seam design.
- **unity_qa** owns T12; **unity_reviewer** owns T13.
- Merge protocol: track branch -> focused tests green -> integrator rebuilds
  native + Unity and runs the full EditMode suite -> merge. A failing gate
  restarts that track only; other tracks continue.

## 7. Wave 0 status (done)

Managed seam is frozen and green; tracks can start:

- `IPhysicsWorld` now exposes `Joints`, `Characters`, `Contacts`, `Serialization`;
  the four feature interfaces exist (`IPhysicsJoints`, `IPhysicsCharacters`,
  `IPhysicsContacts`, `IPhysicsSerialization`).
- Core contracts added: `AuraContact`, `AuraCharacterId`,
  `AuraCharacterDefinition`, `AuraCharacterState`.
- All backends (`Null`, `Managed`, `Native`, test `Fake`) implement the four
  features; managed `Contacts.CopyContacts` and `Serialization.ComputeStateHash`
  are real, the rest are no-ops until their track lands.
- `AuraSimulationWorld` creates/destroys joints through `IPhysicsWorld.Joints`.
- Verified: standalone 116/116, Unity EditMode 107/107, compile clean.

Remaining per track (new files, no shared edits): the native split headers
(`aura_joints.h`, `aura_character.h`, `aura_contacts.h`, `aura_snapshot.h`) and
their `aura_capi_*`/`aura_jolt_*` translation units, plus one CMake line each
(integrator-owned, serial).

## 7b. Wave 0.5 native split (done)

The single-file backends are now split so Wave 1 tracks can own disjoint files
without editing a shared switch:

- `src/physics/jolt/aura_jolt_internal.h` exposes the one `JoltWorld::Impl`,
  the Jolt/Aura conversion helpers and the shared collectors/filters.
- `aura_jolt_world.cpp` keeps world/body/step/state; `aura_jolt_shapes.cpp`
  (`MakeShape` + `BuildShape`), `aura_jolt_queries.cpp`, `aura_jolt_contacts.cpp`,
  `aura_jolt_joints.cpp` and `aura_jolt_character.cpp` hold the feature methods.
- `src/capi/aura_capi_internal.h` shares `ToWorld`; the ABI is split into
  `aura_capi.cpp`, `aura_capi_events.cpp`, `aura_capi_queries.cpp`,
  `aura_capi_contacts.cpp`, `aura_capi_joints.cpp`, `aura_capi_character.cpp`
  and `aura_capi_snapshot.cpp`.
- `CMakeLists.txt` and `build.sh` list the new units. Behaviour is unchanged:
  reference and Jolt/Box2D headless both print `AURA_HEADLESS_OK` and the 3D
  state hash is identical (`0x0f005e29d728e0ff`).

## 8. Validation gates

Per track:
1. `dotnet`/standalone compile of engine assemblies.
2. Focused native tests (Jolt) for the feature.
3. Unity EditMode compile + tests.
4. Console 0 error on a short Play of the demo scene touching the feature.

Wave gate: all Wave-N tracks green, then Wave N+1 starts.

Cross-cutting: ABI version bumped per contract change; determinism test
(snapshot hash round-trip) after T6; platform build matrix after all waves.
