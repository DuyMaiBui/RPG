using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public abstract class AuraColliderAuthoring : MonoBehaviour
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Collider")]
#endif
        [SerializeField]
        protected Vector3 _center;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Collider")]
#endif
        [SerializeField]
        protected bool _isTrigger;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Collider")]
#endif
        [SerializeField]
        protected AuraPhysicsMaterialAsset _material;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Collider")]
        [Sirenix.OdinInspector.ValueDropdown("LayerNames")]
#endif
        [AuraLayer]
        [SerializeField]
        protected int _layer;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Collider")]
#endif
        [SerializeField]
        protected Vector3 _scale = Vector3.one;

        private AuraSimulationInstance _instance;

        public abstract AuraShapeType ShapeType { get; }

        protected abstract AuraShapeGeometry Geometry { get; }

        protected AuraShapeGeometry ScaledGeometry => Geometry.Scaled(EffectiveScale());

        public AuraPhysicsLayers Layers
        {
            get
            {
                if (_instance == null)
                    _instance = GetComponentInParent<AuraSimulationInstance>();
                return _instance != null ? _instance.Layers : null;
            }
        }

#if ODIN_INSPECTOR
        private System.Collections.Generic.IEnumerable<Sirenix.OdinInspector.ValueDropdownItem<int>> LayerNames()
        {
            var layers = Layers;
            if (layers != null)
            {
                var indices = layers.GetNamedIndices();
                for (var i = 0; i < indices.Length; i++)
                {
                    var index = indices[i];
                    yield return new Sirenix.OdinInspector.ValueDropdownItem<int>($"{index}: {layers.GetName(index)}", index);
                }

                yield break;
            }

            yield return new Sirenix.OdinInspector.ValueDropdownItem<int>($"0: {AuraPhysicsLayers.DefaultLayerName}", 0);
        }
#endif

        private AuraPhysicsLayer ResolvedLayer()
        {
            var layers = Layers;
            var layerIndex = _layer;
            if (layers != null && !layers.HasLayer(layerIndex))
                layerIndex = 0;

            return new AuraPhysicsLayer(Mathf.Clamp(layerIndex, 0, AuraPhysicsLayer.MaxLayers - 1));
        }

        private AuraVector3 EffectiveScale()
        {
            var scale = _scale;
            if (scale.x == 0f && scale.y == 0f && scale.z == 0f)
                return AuraVector3.One;

            return new AuraVector3(
                Mathf.Max(Mathf.Abs(scale.x), 1e-4f),
                Mathf.Max(Mathf.Abs(scale.y), 1e-4f),
                Mathf.Max(Mathf.Abs(scale.z), 1e-4f));
        }

        protected AuraPose LocalPose =>
            new AuraPose(new AuraVector3(_center.x, _center.y, _center.z), AuraQuaternion.Identity);

        public AuraPhysicsShapeDefinition BuildShape() =>
            new AuraPhysicsShapeDefinition(
                ShapeType,
                LocalPose,
                _isTrigger,
                _material != null ? _material.ToDefinition() : AuraPhysicsMaterialDefinition.Default,
                ResolvedLayer(),
                ScaledGeometry);

        private void OnDrawGizmos()
        {
            Gizmos.color = _isTrigger ? new Color(0.2f, 0.8f, 1f, 0.6f) : new Color(1f, 0.6f, 0.1f, 0.6f);
            var world = transform.TransformPoint(_center);
            var geometry = ScaledGeometry;

            switch (ShapeType)
            {
                case AuraShapeType.Sphere:
                    Gizmos.DrawWireSphere(world, geometry.Radius);
                    break;

                case AuraShapeType.Box:
                    Gizmos.matrix = Matrix4x4.TRS(world, transform.rotation, Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, geometry.HalfExtents.ToUnity() * 2f);
                    Gizmos.matrix = Matrix4x4.identity;
                    break;

                case AuraShapeType.Capsule:
                    DrawTubeLike(world, geometry.Radius, geometry.Radius, Mathf.Max(0f, geometry.Height * 0.5f - geometry.Radius), true);
                    break;

                case AuraShapeType.Cylinder:
                    DrawTubeLike(world, geometry.Radius, geometry.Radius, geometry.Height * 0.5f, false);
                    break;

                case AuraShapeType.TaperedCapsule:
                    DrawTubeLike(world, geometry.Radius, geometry.TopRadius, Mathf.Max(0f, (geometry.Height - geometry.Radius - geometry.TopRadius) * 0.5f), true);
                    break;

                case AuraShapeType.TaperedCylinder:
                    DrawTubeLike(world, geometry.Radius, geometry.TopRadius, geometry.Height * 0.5f, false);
                    break;

                case AuraShapeType.Plane:
                    DrawPlaneGizmo(world, geometry);
                    break;

                case AuraShapeType.ConvexMesh:
                case AuraShapeType.TriangleMesh:
                    DrawMeshGizmo(geometry);
                    break;

                case AuraShapeType.HeightField:
                    Gizmos.matrix = Matrix4x4.TRS(world, transform.rotation, Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(
                        (geometry.HeightResolution - 1) * geometry.HeightScale.X,
                        geometry.HeightScale.Y * 2f,
                        (geometry.HeightResolution - 1) * geometry.HeightScale.Z));
                    Gizmos.matrix = Matrix4x4.identity;
                    break;

                default:
                    break;
            }
        }

        private void DrawTubeLike(Vector3 world, float bottomRadius, float topRadius, float half, bool rounded)
        {
            var rotation = transform.rotation;
            var up = rotation * Vector3.up;
            var bottom = world - up * half;
            var top = world + up * half;

            DrawRing(bottom, rotation, bottomRadius);
            DrawRing(top, rotation, topRadius);

            var right = rotation * Vector3.right;
            var forward = rotation * Vector3.forward;
            Gizmos.DrawLine(bottom + right * bottomRadius, top + right * topRadius);
            Gizmos.DrawLine(bottom - right * bottomRadius, top - right * topRadius);
            Gizmos.DrawLine(bottom + forward * bottomRadius, top + forward * topRadius);
            Gizmos.DrawLine(bottom - forward * bottomRadius, top - forward * topRadius);

            if (!rounded)
                return;

            DrawHemisphere(bottom, rotation, bottomRadius, false);
            DrawHemisphere(top, rotation, topRadius, true);
        }

        private void DrawPlaneGizmo(Vector3 world, AuraShapeGeometry geometry)
        {
            var normal = (transform.rotation * geometry.PlaneNormal.ToUnity()).normalized;
            var tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 1e-4f)
                tangent = Vector3.right;
            tangent.Normalize();
            var bitangent = Vector3.Cross(normal, tangent).normalized;
            const float size = 2f;
            var p0 = world + (tangent + bitangent) * size;
            var p1 = world + (tangent - bitangent) * size;
            var p2 = world + (-tangent - bitangent) * size;
            var p3 = world + (-tangent + bitangent) * size;
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);
            Gizmos.DrawLine(world, world + normal * size);
        }

        private void DrawMeshGizmo(AuraShapeGeometry geometry)
        {
            var vertices = geometry.MeshVertices;
            if (vertices == null || vertices.Length == 0)
                return;

            var indices = geometry.MeshIndices;
            if (indices != null && indices.Length >= 3)
            {
                for (var index = 0; index + 2 < indices.Length; index += 3)
                {
                    var a = WorldVertex(vertices[indices[index]]);
                    var b = WorldVertex(vertices[indices[index + 1]]);
                    var c = WorldVertex(vertices[indices[index + 2]]);
                    Gizmos.DrawLine(a, b);
                    Gizmos.DrawLine(b, c);
                    Gizmos.DrawLine(c, a);
                }

                return;
            }

            for (var index = 0; index < vertices.Length; index++)
                Gizmos.DrawWireSphere(WorldVertex(vertices[index]), 0.02f);
        }

        private Vector3 WorldVertex(AuraVector3 vertex) =>
            transform.TransformPoint(_center + vertex.ToUnity());

        private static void DrawRing(Vector3 center, Quaternion rotation, float radius, int segments = 24)
        {
            var previous = center + rotation * new Vector3(radius, 0f, 0f);
            for (var index = 1; index <= segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var point = center + rotation * new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }

        private static void DrawHemisphere(Vector3 center, Quaternion rotation, float radius, bool top, int segments = 12)
        {
            var axis = rotation * Vector3.up * (top ? 1f : -1f);
            var right = rotation * Vector3.right;
            var forward = rotation * Vector3.forward;

            for (var meridian = 0; meridian < 2; meridian++)
            {
                var planeAxis = meridian == 0 ? right : forward;
                var previous = center + planeAxis * radius;
                for (var index = 1; index <= segments; index++)
                {
                    var angle = index * Mathf.PI / segments;
                    var point = center + planeAxis * (Mathf.Cos(angle) * radius) + axis * (Mathf.Sin(angle) * radius);
                    Gizmos.DrawLine(previous, point);
                    previous = point;
                }
            }
        }
    }
}
