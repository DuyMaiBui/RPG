# RPG collision architecture

## Scope

The RPG simulates movement, projectiles and combat with a **lightweight 2D collision layer**, not a
rigid-body physics engine. Units are moved by gameplay code (navigation, steering, avoidance); collision
only answers overlap, ray and sweep queries and separates bodies that end up too close.

## Why not a full physics engine

- **Deterministic and cheap**: the whole simulation stays in engine-free C# (`noEngineReferences`), so the
  same code runs on a headless server and a client and feeds the `RPG.Simulation` transport seam.
- **Control**: RPG movement is authored, not solver-driven. Rigid-body dynamics would fight pathfinding,
  formation and crowd avoidance rather than help them.
- **Cost**: no native plugin, no large contact caches, no mobile build risk, no third-party determinism
  assumptions.

`AuraEngine` (Jolt/Box2D) is therefore **not** the RPG physics engine. It stays a separate experiment and
may later back a single heavyweight feature (ragdoll, destructible, vehicle) behind its own interface; it
must not become the RPG core.

## Determinism

The simulation uses `float` (`SimulationVector2`) and is deterministic **on one platform**, which is what
the authoritative (`RPG.Simulation`) architecture needs: the host owns the truth, clients render updates.
Bit-exact cross-platform replay is not a current requirement.

All simulation math goes through `SimulationMath` (in `RPG.Simulation.Contracts`), which currently delegates
to `System.MathF`. Keeping the numeric backend behind one facade makes a switch to fixed-point — for
cross-platform replay — a contained change: implement the fixed-point backend and update the facade, not the
gameplay code. New simulation math must use `SimulationMath`, not `MathF` directly.

## Module layout

- `RPG.Core.Physics`: `CollisionShape` (box/circle/polygon), `ColliderCompound`/`ColliderShapeData`
  (offset shapes, modes, filters), `CollisionShapeQueries` (overlap, raycast, circle sweep),
  `CollisionRay`/`CollisionHit`, and (next) a shared resolution API.
- `RPG.Core.Navigation`: `NavigationGrid`, spatial hash, A* / flow field, ORCA avoidance, obstacle list and
  `HasLineOfSight`.
- `RPG.Core.Projectiles`: swept projectile movement.

## Query API (implemented)

- `CollisionShapeQueries.Overlaps` — SAT overlap of two shapes.
- `CollisionShapeQueries.Raycast(ray, maxDistance, shape, center, out hit)` — ray vs one shape.
- `CollisionShapeQueries.SweepCircle(radius, from, to, shape, center, out hit)` — swept circle vs one shape,
  no tunnelling at high speed.
- `NavigationGrid.HasLineOfSight(from, to, radius)` — swept query against the static obstacle set.

## Non-goals

Rigid-body dynamics, joints, constraints, ragdoll, vehicles, soft body, cloth, stacking stability, 3D
mesh/height-field collision. Add a heavy engine only for a concrete feature that needs one.
