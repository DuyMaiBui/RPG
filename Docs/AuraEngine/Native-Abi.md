# AuraEngine native ABI

The C ABI in `Native/AuraEngine/include/aura/aura_abi.h` is the only public
native boundary. Rules:

- No C++ class, STL type or exception crosses the ABI.
- Fixed-width integer and float types only; explicit struct layout with padding.
- Stable ids are `{index, generation}` pairs; no native pointer is an identity.
- Every entry point returns `AuraResultCode` (except version helpers) and
  validates handles, definitions and ABI compatibility.
- `AURA_ENGINE_ABI_VERSION` is checked by `Aura_CheckAbi`; the managed interop
  layer must surface `AuraResult.AbiMismatch` when it differs.

`aura::IPhysicsBackend` (`aura_physics_backend.h`) mirrors
`AuraEngine.Physics.IPhysicsBackend` and is implemented by the Jolt and Box2D
adapters. Adding a backend must not change the ABI or the managed contract.

Batch transfer is a requirement, not an optimization: state and events cross the
boundary as contiguous arrays in a single call (`Aura_CopyBodyStates`,
`Aura_CopyEvents`), not per-object getters.

Backend-independent shape geometry crosses as primitive data. `AuraShapeDesc`
(ABI v4) carries optional `vertices`/`vertexCount` (convex hull) and
`indices`/`indexCount` (triangle mesh) as flat `float[3*N]` / `uint32[M]`
buffers, plus `planeNormal` (plane) and `topRadius` (tapered shapes). The fixed
fields are declared before the pointers so the layout is identical under
different compilers and managed runtimes. Buffers are owned by the caller for
the duration of `Aura_AttachBody`; the managed binding pins the `AuraVector3[]`
/ `int[]` arrays while the native body is created.

Joints cross as `AuraJointDesc` (ABI v5) plus `Aura_CreateJoint`,
`Aura_DestroyJoint` and `Aura_HasJoint`. Joint handles are opaque `uint64_t`
values encoded as `(generation << 32) | index`, and the descriptor carries the
type, both `AuraBodyHandle`s, world anchors, local axes, limits, motor and
spring settings. `AuraJointType` mirrors `AuraEngine.Core.AuraJointType`; the
Jolt adapter supports fixed, point, distance, spring, hinge, slider (with
limits and motors), cone, swing-twist and pulley, while the Box2D adapter
supports the subset expressible in 2D (distance, spring, point, hinge, fixed,
slider). `AuraPhysicsCapabilities.Joints` advertises the capability.

Contact manifolds cross as `AuraContact` (ABI v5) plus `Aura_CopyContacts`,
which writes the current contact points (body handles, world point, world
normal, penetration) into a caller-owned contiguous buffer. Jolt reports the
latest manifold per body pair while the pair is active, and keeps the last
manifold when the pair deactivates (sleeps) so resting contacts stay queryable.
Box2D reports its per-body `b2Body_GetContactData` manifolds. Impulse is zero on
the native path (Jolt does not expose solver impulses through the listener); the
managed reference backend fills a real normal impulse.

Characters cross as `AuraCharacterDesc` (capsule pose, radius, height, mass,
max slope angle, layer, collision mask) and `AuraCharacterState` (position,
velocity, grounded) via `Aura_CreateCharacter`, `Aura_DestroyCharacter`,
`Aura_GetCharacterState` and `Aura_MoveCharacter`. The Jolt adapter wraps
`CharacterVirtual`; each move seeds the horizontal velocity from the desired
translation and integrates gravity vertically (reset while grounded) before
`ExtendedUpdate`. The reference and Box2D backends do not implement characters
and return `AuraResultCode.AURA_UNSUPPORTED_QUERY` / invalid handles. The
capability is advertised as `AuraPhysicsCapabilities.Characters` on the 3D
native backend only.

Snapshot/restore crosses as the raw body-state stream via `Aura_SerializeState`
(size query with a null buffer, then a fill call) and `Aura_DeserializeState`,
which are backed by `CopyBodyStates` / `ApplyStates`. The managed binding
exposes it through `IPhysicsSerialization.SaveState` / `RestoreState` and
`AuraSimulationWorld.SaveState` / `RestoreState`. The byte format is
backend-specific (Jolt/Box2D body layouts differ), so snapshots are only
guaranteed to round-trip on the backend that produced them; the portable,
tick-tagged format lives in `AuraEngine.Serialization.AuraStateSerializer`.




## ABI v10: runtime body and joint control

`AURA_ENGINE_ABI_VERSION` is 10 (mirrored by `NativeMethods.ExpectedAbiVersion`,
checked by `NativePhysicsBackend.IsAvailable`). v10 is not backward compatible
with v9 binaries: `AuraBodyState::flags` gains `AURA_BODY_FLAG_DISABLED`, two
result codes are added and new structs/entry points are appended, so managed
and native must be rebuilt together.

Result codes: `AURA_BODY_DISABLED` (10, the body is removed from the
simulation) and `AURA_UNSUPPORTED_OPERATION` (11, valid request the body, joint
type or backend cannot perform; the kernel never silently ignores a control
call). Every entry returns `AURA_INVALID_HANDLE` for stale handles and
`AURA_INVALID_DEFINITION` for non-finite or out-of-range arguments and for
operations that do not apply to the motion type (force on a static body,
velocity on a static body). Backends without an override (the reference world)
return `AURA_UNSUPPORTED_OPERATION`. Managed capability bits:
`AuraPhysicsCapabilities.BodyControl` / `JointControl`.

Body control (`IPhysicsBodyControl`, `IPhysicsWorld.BodyControl`,
`AuraSimulationWorld.BodyControl`):

| Entry point | Notes |
| --- | --- |
| `Aura_SetLinearVelocity`, `Aura_SetAngularVelocity` | Dynamic and kinematic bodies. |
| `Aura_AddForce`, `Aura_AddTorque` | Dynamic bodies; act on the next step only. |
| `Aura_AddImpulse`, `Aura_AddAngularImpulse` | Dynamic bodies; `IPhysicsWorld.ApplyImpulse` now routes here. |
| `Aura_SetBodyPose(pose, zeroVelocity)` | Teleport; optionally clears both velocities. Allowed while disabled. |
| `Aura_SetGravityScale`, `Aura_SetFriction`, `Aura_SetRestitution` | Friction/restitution must be >= 0; all wake a resting body. |
| `Aura_SetMotionType` | Static/dynamic/kinematic. Bodies made only of triangle mesh, height field or plane shapes stay static (`UNSUPPORTED_OPERATION`); vehicle chassis are rejected. Jolt bodies keep motion properties so promotion works. |
| `Aura_SetBodyLayer(layer, collisionMask)` | Layer < 64. The mask is applied on top of the world collision matrix. Jolt enforces it in the sim shape filter (default `~0`; creation still ignores `AuraBodyDesc::collisionMask` as before); Box2D rewrites the shape filters (`mask & matrix[layer]`, as at creation). |
| `Aura_SetBodyEnabled`, `Aura_IsBodyEnabled` | Removes/adds the body to the simulation without destroying the handle; attached joints are disabled with it. Queries ignore disabled bodies. |

2D worlds use only x/y of linear vectors and z of angular vectors. Velocity
reads need no new entry: `AuraBodyState` already carries both velocities.

Joint control (`IPhysicsJointControl`, `IPhysicsWorld.JointControl`,
`AuraSimulationWorld.JointControl`):

| Entry point | Hinge/revolute | Slider/prismatic | Fixed | Point | Distance/spring | Others |
| --- | --- | --- | --- | --- | --- | --- |
| `Aura_SetJointMotor(AuraJointMotorDesc)` | velocity; position on Jolt only | velocity; position on Jolt only | - | - | - | unsupported |
| `Aura_SetJointLimits(enabled, min, max)` | yes | yes | - | - | - | unsupported |
| `Aura_SetJointBreakThreshold(force, torque)` | force + torque | force + torque | force + torque | force | force | unsupported |
| `Aura_IsJointBroken`, `Aura_GetJointFeedback` | yes | yes | yes | yes | yes | yes |

Limits are relative to the creation pose with `min <= 0 <= max` (hinge also
within +-pi); disabling restores the unlimited range. Box2D has no position
motor, so `AURA_JOINT_MOTOR_POSITION` returns `UNSUPPORTED_OPERATION` there.
A threshold of 0 means unbreakable. After each step a joint whose reaction
force or torque exceeds a threshold is removed from the simulation and flagged
broken; its handle stays valid (`Aura_IsJointBroken`, feedback reports the
breaking load) until `Aura_DestroyJoint`, and `Aura_HasJoint` reports false.
Feedback loads come from the last step; a sleeping joint reports the load of
the step it fell asleep in. `AuraJointFeedback` excludes motor drive from
force/torque and reports it as `motorLoad`. Constraints stay owned by Jolt's
constraint manager (never `Release()` after `RemoveConstraint`).

Snapshots: `AuraBodyState::flags` carries the disabled bit through
`Aura_SerializeState` / `Aura_DeserializeState`. Gravity scale, motion type,
material, layer/mask, motors, limits and break thresholds are configuration,
not simulation state, and are not part of the byte stream; callers re-apply
them after a restore. The state hash is unchanged.


## ABI 11 (packages B-E)

`AURA_ENGINE_ABI_VERSION` is 11. Layout changes: `AuraCharacterDesc` grew from 64 to 72 bytes
(`stepHeight`), `AuraJointDesc` grew from 140 to 280 bytes (appended fields; ragdoll part
descriptors embed it), `AuraShapeDesc.isOneWay` reuses a former pad byte, and
`AuraForceFieldDesc` is new (104 bytes). Managed mirrors assert these sizes.

### Box2D queries and joints (B)
- `OverlapPoint/Box/Capsule/Sphere/Shape` and `SphereCast/CapsuleCast/BoxCast/ShapeCast` work in
  Plane2D. Overlaps return one hit per body (`distance = 0`, closest point on the hit shape, normal
  from that point toward the query centre, `shape` = body index); casts return the closest hit.
- New joint types: `AURA_JOINT_WHEEL = 10` (axisA = suspension axis, `springFrequency` 0 = rigid,
  motor target in rad/s), `AURA_JOINT_MOUSE = 11` (`anchorB` is the target, moved with
  `Aura_SetJointTarget`), `AURA_JOINT_ROPE = 12` (`distance` = max length). All support break
  thresholds and feedback; wheel also supports `SetMotor`/`SetLimits`. 3D rejects them.

### Characters and one-way platforms (C)
- Plane2D now implements the character ABI as a virtual capsule: ground detection, slopes up to the
  max angle, step-up, wall sliding, moving-platform carry, pushing light bodies, jump (positive
  vertical translation), snapshot round-trip. Dynamic bodies do not collide with the virtual capsule.
- `isOneWay` shapes are solid only when the contact normal is within 60 degrees of the shape's local
  +Y and the other body is not rising through it (Box2D pre-solve; the mover uses its own filter).
  Jolt ignores `isOneWay` and `stepHeight`.

### Jolt joints (D)
| Type | Motor / limits | Break threshold |
|---|---|---|
| SixDof | per axis (`Aura_SetJointAxisLimits/Motor`) | force, torque |
| Cone | none | force, torque |
| SwingTwist | twist via `SetLimits`; per-axis motor | force, torque |
| Pulley | `SetLimits` changes the rope length | force |
| Gear (13), RackAndPinion (14) | none | torque |
| Path (15) | not implemented (`UNSUPPORTED_OPERATION`) | none |

Gear and RackAndPinion reference two existing joints; destroying or breaking either removes the
dependent joint first and flags it broken. SixDof axes 0-5 are translation XYZ then rotation XYZ;
SwingTwist axes are 0 twist, 1 normal swing, 2 plane swing. Gear: A = -ratio * B; rack ratio is rad/m.

### Gravity, force fields, CCD (E)
- `Aura_SetWorldGravity/GetWorldGravity`, `Aura_CreateForceField/UpdateForceField/DestroyForceField`
  (directional, radial with none/linear/inverse-square falloff, drag/wind; applied in slot order
  before each step), `Aura_SetBodyCollisionDetection`. Fields are configuration, not part of
  snapshots or the state hash: recreate them after a rollback. Box2D bodies now honor the bullet flag.
- Time control is managed-level (`AuraTimeStepper`, `AuraHitStop`): the kernel's fixed dt never
  changes; time scale 0.5 runs a step every second tick.


## Contracts added after the soak work

### Events and world handles
- **Event order.** Pending events are sorted after every step by (type, bodyA.index, bodyA.generation, bodyB.index,
  bodyB.generation), so `Aura_CopyEvents` returns the same order for every run and every Jolt thread count
  (`AURA_JOLT_THREADS`, 0 to 64, overrides the worker count for testing). `Aura_CopyContacts` is ordered by body pair.
- **World handles** are `(generation << 32) | (slot + 1)` in a fixed table of 4096 slots; the public struct is
  unchanged. Zero, garbage, stale and double-destroyed handles return `AURA_INVALID_WORLD`; creating beyond 4096 live
  worlds returns `AURA_CAPACITY_EXCEEDED`. `Aura_CreateWorld`/`Aura_DestroyWorld` are serialized by one mutex (Box2D keeps
  a process-global world array). Calling a world function while another thread destroys that same world is still a
  caller error. `Aura_LiveWorldCount` reports worlds created and not destroyed.

### Joints
- **Broken joints keep their handle** until `Aura_DestroyJoint`: `HasJoint` stays true, `Aura_IsJointBroken` and feedback
  keep working, and motor/limit/target/break-threshold calls return `AURA_UNSUPPORTED_OPERATION`.
  `AURA_INVALID_HANDLE` means destroyed, stale or garbage.
- **Rejected at creation:** a joint on a disabled body (`AURA_BODY_DISABLED`); a joint between two non-dynamic bodies and a
  Box2D mouse joint whose body B is not dynamic (`AURA_INVALID_DEFINITION`); in Box2D also negative distance or spring
  parameters, limit min > max, a hinge limit outside +-pi, a slider axis with no in-plane component, and anchors too
  far from a light body (lever ratio r^2*m/I above 16 for distance, spring and rope, above 500 for slider and wheel).
  Authoring a rope or spring anchor several metres from a small body therefore fails with an error: move the anchor
  closer or enlarge the body.
- **Box2D stability.** Limits and lengths are eased toward their targets (at most 8 m/s * dt per step) because
  Box2D v3.1 corrects violations with an uncapped soft bias; spring, wheel and mouse hertz are capped to 0.5/dt; limits are
  relative to the creation pose (`referenceAngle`). Joints whose anchors drift more than 6 m apart are torn off and
  reported as broken.
- `SetKinematicTarget` requires a kinematic body (`AURA_UNSUPPORTED_OPERATION` otherwise) and a target is consumed by the
  step that reaches it; a zero-length step no longer erases the last valid delta. Box2D honors `allowSleeping`.

### Snapshots (format version 3)
- `Aura_DeserializeState` validates the whole buffer before applying anything: size, magic, version (2 or 3), exact
  length, body and character counts, finite and bounded values (position 1e6, velocity 1e5, unit quaternions), flag and enum
  ranges, live matching handles, no duplicates. `AURA_INVALID_WORLD`, `AURA_INVALID_DEFINITION`,
  `AURA_CAPACITY_EXCEEDED` or `AURA_INVALID_HANDLE` is returned and nothing changes. The managed
  `RestoreState` now throws `AuraException` for a rejected snapshot.
- Version 3 adds a 16-byte `BodyExtra` per body: sleep state, motion type, kinematic-target-pending and the exact `b2Rot`.
  Version 2 buffers still restore (without extras). Free flight, kinematic 3D and most 2D scenarios, and both
  character movers restore bit-exactly; piles, joints with warm starting and Jolt sleep timers do not (contact and joint
  warm-start caches cannot be captured through the public API).
