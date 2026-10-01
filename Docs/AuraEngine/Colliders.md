# AuraEngine colliders

One authoring component per shape. Add it to the same GameObject (or a child)
as `AuraPhysicsBodyAuthoring`; the body aggregates every
`AuraColliderAuthoring` in its children, so **compound** bodies are made by
adding several collider components. Each component draws scene gizmos and
editable scene handles (center, size/radius).

| Authoring component | `AuraShapeType` | Jolt (native 3D) | Box2D (native 2D) | Managed |
|---|---|:---:|:---:|:---:|
| `AuraSphereColliderAuthoring` | Sphere | `SphereShape` | circle | yes |
| `AuraBoxColliderAuthoring` | Box | `BoxShape` | polygon | yes (AABB) |
| `AuraCapsuleColliderAuthoring` | Capsule | `CapsuleShape` | capsule | yes |
| `AuraCylinderColliderAuthoring` | Cylinder | `CylinderShape` | capsule approximation | capsule approximation |
| `AuraTaperedCapsuleColliderAuthoring` | TaperedCapsule | `TaperedCapsuleShape` | no | no |
| `AuraTaperedCylinderColliderAuthoring` | TaperedCylinder | `TaperedCylinderShape` | no | no |
| `AuraPlaneColliderAuthoring` | Plane | `PlaneShape` | no | no |
| `AuraConvexColliderAuthoring` | ConvexMesh | `ConvexHullShape` | no | no |
| `AuraMeshColliderAuthoring` | TriangleMesh | `MeshShape` (static only) | no | no |

Authoring validates each shape against the active backend capabilities and
logs an error for unsupported shapes; it never drops a collider silently.
Convex hull and triangle mesh geometry is produced by the editor baker
(`AuraMeshColliderBaker`) into an `AuraPhysicsMeshData` asset and crosses the C
ABI (`AuraShapeDesc`, ABI v4) as flat vertex/index buffers.

## Fields

- Common (`AuraColliderAuthoring`): `Center` (local), `Is Trigger`, optional
  `AuraPhysicsMaterialAsset`.
- Sphere: `Radius`. Box: `Size`. Capsule / Cylinder: `Radius`, `Height`.
  Tapered capsule / cylinder: `Radius` (bottom), `Top Radius`, `Height`.
  Plane: `Normal` (local). Convex / mesh: a baked `AuraPhysicsMeshData`.

`Height` semantics: `Capsule` and `TaperedCapsule` treat `Height` as the total
height including the (rounded) caps, so the cylinder half-length is
`Height/2 - radius` and `(Height - bottomRadius - topRadius)/2`. `Cylinder` and
`TaperedCylinder` treat `Height` as the total of the flat-ended body
(half-length `Height/2`). Collider and gizmo use the same half-length.
