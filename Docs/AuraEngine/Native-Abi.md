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



