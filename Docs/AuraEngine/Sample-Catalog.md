# Unity sample catalog (Jolt Samples parity)

Tracks which JoltPhysics `Samples/Tests` categories have an authored Unity scene
in `Assets/AuraEngine/Demo/Scenes`. Feature mapping lives in
`Jolt-Features-Plan.md`; this file records scene coverage and explicit gaps.
Jolt-internal benchmarks (multithreading stress, broad-phase insertion timing)
have no Unity scene equivalent and are not tracked here. 2D scenes are Box2D
scenarios, not Jolt samples, and use the dedicated `Aura*2DAuthoring` components.

| Category | Scene(s) | Status |
|---|---|---|
| Character | `AuraDemoCharacter3D` | Done: stairs, 25° ramp, 65° blocked ramp, hurdle jump, moving platform. |
| General body (basics) | `AuraDemoCore3D`, `AuraDemoCore2D`, `AuraDemo3D`, `AuraDemo2D` | Partial: stacking, kinematic, trigger, raycast. |
| Constraints | `AuraDemoArticulation3D`, `AuraDemoArticulation2D` | Partial: distance, fixed, hinge only. |
| Vehicle | `AuraDemoArticulation3D` (car), `AuraDemoVehicle2D` | Partial: wheeled car; no tank/motorcycle. |
| SoftBody | `AuraDemoAdvancedSoftBody3D`, `AuraDemoCloth3D`, `AuraDemoCloth2D` | Partial. |
| Rig / ragdoll | `AuraDemoHumanoid3D`, `AuraDemoAdvancedRagdoll3D`, `AuraDemoChainRagdoll2D` | Partial: no powered/kinematic rig. |
| Water | `AuraDemoAdvancedWater3D`, `AuraDemoWater2D` | Done (buoyancy). |
| Hair | `AuraDemoHair3D`, `AuraDemoHair2D` | Done (CPU Verlet). |
| Shapes | — | Pending scene (ConvexHull, Mesh, HeightField, tapered kernels already tested). |
| General body (conveyor, sensor, restitution, freeze DOF) | — | Pending scene (kernel tests exist). |

## Character scene notes

`AuraDemoCharacter3D` runs five `AuraCharacterAuthoring` walkers driven by
`AuraDemoCharacterDriver` (`_jumpInterval` > 0 enables periodic jumps). The Editor
must be focused for the simulation to advance in Play Mode. Measured in Play
Mode: the stairs and gentle ramp lanes gain height, the 65° lane stays at ground
level, the jumper clears the 0.8 m hurdle, and the platform walker rides the
moving platform.

## Known gaps (not demoed, not faked)

- Constraints beyond distance/fixed/hinge: slider, six-DOF, cone, swing-twist
  motors, pulley, gear, rack-and-pinion, path (no native joint ABI yet).
- Runtime change of motion type, shape or layer; per-triangle friction; shape
  filters; contact manifold access.
- Tank and motorcycle vehicles; powered and soft-keyframed rigs.
- GPU hair/cloth backend (`IVerletSolverBackend` seam exists, CPU only).
