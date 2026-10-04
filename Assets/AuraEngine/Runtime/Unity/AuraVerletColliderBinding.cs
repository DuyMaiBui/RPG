using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /// <summary>
    /// Pairs static authored collider data with the Transform that owns its CURRENT world pose.
    /// Each <see cref="Build"/> converts that pose (and the transform's lossy scale) into the
    /// engine-free <see cref="AuraVerletCollider"/>; moving/kinematic owners work because the pose is
    /// read fresh, and linear velocity is derived from the previous call.
    /// </summary>
    internal sealed class AuraVerletColliderBinding
    {
        private readonly Transform _transform;
        private readonly AuraVerletColliderKind _kind;
        private readonly Vector3 _localPosition;
        private readonly Quaternion _localRotation;
        private readonly float _radius;
        private readonly float _height;
        private readonly Vector3 _halfExtents;
        private readonly Vector3 _normal;
        private readonly float _friction;
        private readonly bool _planar;
        private Vector3 _previousPosition;
        private bool _hasPrevious;

        public AuraVerletColliderBinding(
            Transform transform,
            AuraVerletColliderKind kind,
            Vector3 localPosition,
            Quaternion localRotation,
            float radius,
            float height,
            Vector3 halfExtents,
            Vector3 normal,
            float friction,
            bool planar)
        {
            _transform = transform;
            _kind = kind;
            _localPosition = localPosition;
            _localRotation = localRotation;
            _radius = radius;
            _height = height;
            _halfExtents = halfExtents;
            _normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
            _friction = friction;
            _planar = planar;
        }

        /// <summary>Maps an authored physics shape; returns null for shape types verlet collision does not support.</summary>
        public static AuraVerletColliderBinding FromShape(Transform transform, in AuraPhysicsShapeDefinition shape, bool planar)
        {
            AuraVerletColliderKind kind;
            switch (shape.Type)
            {
                case AuraShapeType.Sphere: kind = AuraVerletColliderKind.Sphere; break;
                case AuraShapeType.Capsule: kind = AuraVerletColliderKind.Capsule; break;
                case AuraShapeType.Box: kind = AuraVerletColliderKind.Box; break;
                case AuraShapeType.Plane: kind = AuraVerletColliderKind.Plane; break;
                default: return null;
            }

            var geometry = shape.Geometry;
            return new AuraVerletColliderBinding(
                transform,
                kind,
                shape.LocalPose.Position.ToUnity(),
                shape.LocalPose.Rotation.ToUnity(),
                geometry.Radius,
                geometry.Height,
                geometry.HalfExtents.ToUnity(),
                geometry.PlaneNormal.ToUnity(),
                shape.Material.Friction,
                planar);
        }

        public AuraVerletCollider Build(float skin, float deltaTime)
        {
            var position = _transform.TransformPoint(_localPosition);
            var rotation = _transform.rotation * _localRotation;
            var scale = _transform.lossyScale;
            var ax = Mathf.Abs(scale.x);
            var ay = Mathf.Abs(scale.y);
            var az = Mathf.Abs(scale.z);

            var velocity = Vector3.zero;
            if (_hasPrevious && deltaTime > 0f)
                velocity = (position - _previousPosition) / deltaTime;
            _previousPosition = position;
            _hasPrevious = true;
            if (_planar)
                velocity.z = 0f;

            var center = position.ToAura();
            var aura = velocity.ToAura();
            var orientation = rotation.ToAura();

            switch (_kind)
            {
                case AuraVerletColliderKind.Sphere:
                {
                    var radiusScale = _planar ? Mathf.Max(ax, ay) : Mathf.Max(ax, Mathf.Max(ay, az));
                    return AuraVerletCollider.Sphere(center, _radius * radiusScale, _friction, skin, aura, _planar);
                }

                case AuraVerletColliderKind.Capsule:
                {
                    var radiusScale = _planar ? ax : Mathf.Max(ax, az);
                    var radius = _radius * radiusScale;
                    var height = Mathf.Max(_height * ay, radius * 2f);
                    return AuraVerletCollider.Capsule(center, orientation, radius, height, _friction, skin, aura, _planar);
                }

                case AuraVerletColliderKind.Box:
                {
                    var half = new AuraVector3(_halfExtents.x * ax, _halfExtents.y * ay, _halfExtents.z * az);
                    return AuraVerletCollider.Box(center, orientation, half, _friction, skin, aura, _planar);
                }

                default:
                {
                    var normal = (rotation * _normal).ToAura();
                    return AuraVerletCollider.Plane(center, normal, _friction, skin, aura, _planar);
                }
            }
        }
    }
}
