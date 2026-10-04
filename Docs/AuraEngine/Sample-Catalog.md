# Unity sample catalog (Jolt Samples parity)

Tracks which JoltPhysics `Samples/Tests` categories have an authored Unity scene
in `Assets/AuraEngine/Demo/Scenes`. Feature mapping lives in
`Jolt-Features-Plan.md`; this file records scene coverage and explicit gaps.
Jolt-internal benchmarks (multithreading stress, broad-phase insertion timing)
have no Unity scene equivalent and are not tracked here. 2D scenes are Box2D
scenarios, not Jolt samples, and use the dedicated `Aura*2DAuthoring` components.

| Category | Scene(s) | Status |
|---|---|---|
| Character 3D | `AuraDemoCharacter3D` | Done: stairs, 25° ramp, 65° blocked ramp, hurdle jump, moving platform. |
| Character 2D / platformer | `AuraDemoPlatformer2D` | Done: ramp, one-way platforms, pushable crate, moving platform, updraft zone, scripted hero. |
| General body (basics) | `AuraDemoCore3D`, `AuraDemoCore2D`, `AuraDemo3D`, `AuraDemo2D` | Partial: stacking, kinematic, trigger, raycast. |
| Constraints 3D | `AuraDemoConstraints3D`, `AuraDemoArticulation3D` | Done for gear, rack-and-pinion, pulley, SixDof door, swing-twist arm; Path is a gap. |
| Constraints / sandbox 2D | `AuraDemoSandbox2D`, `AuraDemoArticulation2D` | Done: motorised wheel joints, rope bridge with a breakable joint, mouse-drag joint, impulse kicker. |
| Vehicle | `AuraDemoArticulation3D` (car), `AuraDemoSandbox2D` (wheel car), `AuraDemoVehicle2D` | Partial: wheeled car; no tank/motorcycle. `AuraDemoVehicle2D` is not driven yet. |
| Gravity / space | `AuraDemoSpace3D` | Done: radial inverse-square planet field, six circular orbits, wind tunnel, slow-motion pulse. |
| Ragdoll / hit reaction | `AuraDemoRagdollHit3D`, `AuraDemoHumanoid3D`, `AuraDemoAdvancedRagdoll3D`, `AuraDemoChainRagdoll2D` | Partial: swing-twist humanoid blown over by blasts; the kernel ragdoll prefabs cannot take impulses. |
| SoftBody / cloth | `AuraDemoAdvancedSoftBody3D`, `AuraDemoCloth3D`, `AuraDemoCloth2D` | Partial: cloth and hair collide with scene colliders. |
| Water | `AuraDemoAdvancedWater3D`, `AuraDemoWater2D` | Done (buoyancy). |
| Hair | `AuraDemoHair3D`, `AuraDemoHair2D` | Done (CPU Verlet with collision). |
| Shapes | none | Pending scene (ConvexHull, Mesh, HeightField, tapered kernels already tested). |
| General body (conveyor, sensor, restitution, freeze DOF) | none | Pending scene (kernel tests exist). |
| Top-down 2D | none | Pending: needs a driver that moves a dynamic body with velocity (the 2D character treats +Y as jump). |

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

## Demo validation

`Tools/AuraSmoke/aura_smoke.sh` plays every scene: 23 scenes, 0 console errors. Hair and cloth
report STATIC because the probe measures transforms; their meshes were verified from screenshots.
Coming to rest and staying still is expected for `AuraDemoCore3D`, `AuraDemoAdvancedRagdoll3D`,
`AuraDemoChainRagdoll2D` and `AuraDemoVehicle2D`. Run it with the display awake: when the
display sleeps macOS throttles the Editor and the CLI hangs.
