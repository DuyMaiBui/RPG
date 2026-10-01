# Jolt Samples feature map, brainstorm and plan

Research source: `Native/AuraEngine/third_party/JoltPhysics/Samples/Tests`
(Jolt 5.6, sparse checkout). This maps every authored feature Jolt ships as a
sample to an AuraEngine feature, then proposes a dependency-ordered plan.

## 1. Feature map (Jolt Samples -> AuraEngine)

### Shapes (`Samples/Tests/Shapes`, `ScaledShapes`, `ConvexCollision`)
- Primitives: Box, Sphere, Capsule, Cylinder, TaperedCapsule, TaperedCylinder,
  Plane, Triangle, EmptyShape. **AuraEngine: done** (native Jolt).
- ConvexHull, MeshShape, HeightField (Terrain), StaticCompound,
  MutableCompound. **AuraEngine: ConvexHull + Mesh done; HeightField + mutable
  compound missing.**
- Shape wrappers: `RotatedTranslatedShape`, `OffsetCenterOfMassShape`,
  `ScaledShape`. **Missing** — needed for authoring offsets/rotation/scale and
  custom center of mass.
- Queries: ClosestPoint, EPA, random ray, convex-hull shrink.
  **AuraEngine: raycast/overlap done; closest-point + shape-distance missing.**

### General body (`Samples/Tests/General`)
- `AllowedDOFs` (freeze position/rotation), `CenterOfMass`, `Damping`,
  `GravityFactor`, `ChangeMotionQuality`, `ChangeMotionType`, `ChangeShape`,
  `ChangeObjectLayer`, `ModifyMass`, `Kinematic`, `HighSpeed`,
  `BigVsSmall`, `HeavyOnLight`, `ContactListener`, `ContactManifold`,
  `ConveyorBelt`, `FrictionPerTriangle`, `ShapeFilter`, `Sensor`,
  `Restitution`, `Multithreaded`, `Island`/sleeping, `ActiveEdges`,
  `EnhancedInternalEdgeRemoval`.
- **AuraEngine now**: mass, gravity factor, damping (linear/angular), freeze,
  centre of mass, inertia multiplier, collision detection mode, sleep, sensors,
  collision matrix. **Missing**: runtime change of motion type/shape/layer,
  conveyor surface velocity, per-triangle friction, shape filter, contact
  manifold access, active-edge / internal-edge options, island introspection.

### Constraints (`Samples/Tests/Constraints`)
- Fixed, Point, Distance, Hinge (+Powered/motor), Slider (+Powered/motor),
  SixDOF, Cone, SwingTwist (+motor/friction), Pulley, Gear, RackAndPinion,
  Path, Spring, ConstraintPriority, ConstraintVsCOMChange, Singularity.
- **AuraEngine now**: Distance, Fixed, Hinge (basic, managed-only). **Missing**:
  the full Jolt set, motors, limits, springs, priority, and native joint ABI.

### Vehicle (`Samples/Tests/Vehicle`)
- WheeledVehicle (car), Motorcycle, Tank (tracked), SixDOF vehicle, stress.
  **Missing.**

### Character (`Samples/Tests/Character`)
- Character (capsule controller), CharacterVirtual, variable gravity direction
  (planet/spaceship), gathering. **Missing.**

### SoftBody (`Samples/Tests/SoftBody`)
- Bend/LRA/CosseratRod constraints, pressure, skinned, force, sensor,
  contact listener, kinematic, friction/restitution, gravity factor, custom
  update, stress, fast-moving collisions. **Missing.**

### Rig / ragdoll (`Samples/Tests/Rig`)
- CreateRig, PoweredRig, KinematicRig, SoftKeyframedRig, SkeletonMapper,
  Save/LoadRig, BigWorld. **Missing.**

### Water (`Samples/Tests/Water`)
- Boat (buoyancy + water shape). **Missing.**

### BroadPhase / tools
- CastRay, insertion tests, `Tools/LoadSnapshot` (save/load world state).
  **AuraEngine**: raycast done; snapshot save/load missing (needed for replay
  and server determinism).

### Hair (`Samples/Tests/Hair`)
- Strand simulation. Optional / far future.

## 2. Brainstorm: what is worth building, in order of value

1. **Finish rigidbody authoring** (already in progress): mass, centre of mass,
   freeze position/rotation, linear/angular damping, gravity factor, inertia
   multiplier, collision detection mode, sleep, max velocities. Highest
   day-to-day value.
2. **Shape wrappers + remaining shapes**: OffsetCenterOfMass, RotatedTranslated,
   Scaled, HeightField/Terrain, MutableCompound. Makes authoring express the
   real geometry and lets level/hair/vehicle work later.
3. **Full constraint set with motors/limits** behind one typed joint contract +
   per-joint authoring components (like Unity's joints). Big gameplay value.
4. **Character controller** (capsule) — movement, slopes, steps, moving
   platforms, variable gravity.
5. **Contact/query enrichment**: expose contact manifolds, per-triangle
   friction, conveyor surface velocity, shape filters, active-edge options.
6. **Snapshot/serialization** (Jolt `SaveState`/`RestoreState`): server +
   replay + determinism, ties into `AuraEngine.Serialization`.
7. **Vehicles** (wheeled first), then **SoftBody**, **Ragdoll/Rig**,
   **Water/buoyancy**, **Hair**.
8. **Editor tooling** for each: authoring inspectors (Odin), accurate gizmos,
   joint handles, character/vehicle debug views.

## 3. Plan (dependency-ordered milestones)

```text
R1 Body config           (finish)   -> managed + authoring + tests
R2 Shape wrappers/shapes            -> OffsetCenterOfMass, RotatedTranslated,
                                       Scaled, HeightField, MutableCompound
R3 Constraint contract + authoring  -> IPhysicsJointDefinition + components
R4 Constraint backends              -> Jolt joints + motors/limits/springs
R5 Character controller             -> IPhysicsCharacter + Unity component
R6 Contacts/queries enrichment      -> manifolds, conveyor, shape filters
R7 Snapshot/serialization           -> Jolt SaveState + AuraEngine.Replay
R8 Vehicles                         -> wheeled, then tank/motorcycle
R9 SoftBody                         -> strands/rods/cloth
R10 Ragdoll/Rig                     -> create/powered/skinned
R11 Water/buoyancy                  -> water shape + boat helper
R12 Hair                            -> optional
R13 Editor tooling per milestone    -> inspectors, gizmos, handles, debug
```

### R1 acceptance (this slice)
- `AuraPhysicsBodyDefinition` + ABI v5 + Jolt mapping for mass, gravity factor,
  damping, freeze (DOF), centre of mass, inertia multiplier, collision
  detection, sleep, max velocities; managed backend mirrors what applies.
- `AuraPhysicsBodyAuthoring` exposes the same fields (Odin groups).
- Tests: freeze position stops falling, zero gravity floats, freeze rotation
  zeroes angular velocity, friction slows sliding, taper rest height.
- Gate: penetrations ≤ 0.002 (Jolt `mPenetrationSlop = 0`), no console errors.

### R3/R4 sketch (constraints)
- One managed contract `IPhysicsJointDefinition` with a `Type` + typed settings
  (anchors, axes, limits, motors, spring). Mirror in the C ABI
  (`AuraJointDesc`) and Jolt (`*ConstraintSettings`). Per-joint Unity
  components: `AuraFixedJoint`, `AuraHingeJoint`, `AuraSliderJoint`,
  `AuraSixDofJoint`, `AuraConeJoint`, `AuraSwingTwistJoint`, `AuraPulleyJoint`,
  `AuraGearJoint`, `AuraRackAndPinionJoint`, `AuraPathJoint`, `AuraSpringJoint`.
- Gate: generic constraint tests (anchor coincidence, limit range, motor speed
  reaches target, break force) run on Jolt.

### Cross-cutting requirements
- ABI stays versioned; each struct change bumps `AURA_ENGINE_ABI_VERSION`.
- Gameplay core stays engine-free; authoring stays in `AuraEngine.Unity`.
- Determinism: fixed tick, sorted iteration, snapshot round-trip hash.
- Every feature ships with managed tests, a Jolt test, a Unity authoring
  component, accurate gizmos and docs.
