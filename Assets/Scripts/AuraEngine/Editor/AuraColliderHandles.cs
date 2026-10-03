using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AuraEngine.EditorTools
{
    /* Scene-view authoring for Aura colliders.

       The wireframe and the drag handles are drawn twice: once with
       CompareFunction.LessEqual (visible in front, full color) and once with
       CompareFunction.Greater (occluded by geometry, faded). Handles are solid
       white billboard squares.

         Sphere           radius handle (unchanged) + two rings
         Box              twelve edges + six face squares
         Cylinder         two rings + four vertical ties + four radius squares
         TaperedCylinder  bottom/top radius squares + four ties
         Capsule          body rings + a half sphere on each cap (no seam rings)
         TaperedCapsule   tapered body + a half sphere on each cap */
    [InitializeOnLoad]
    public static class AuraColliderHandles
    {
        private const int RingSegments = 48;
        private const int HalfSphereMeridians = 4;
        private const int HalfSphereSegments = 20;

        private static readonly Vector3[] Ties =
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, -1f),
        };

        private static readonly Color SolidColor = new Color(1f, 0.6f, 0.1f);
        private static readonly Color TriggerColor = new Color(0.2f, 0.8f, 1f);
        private static readonly Color OccludedColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color HandleColor = Color.white;
        private static readonly Color HandleOccluded = new Color(1f, 1f, 1f, 0.35f);

        static AuraColliderHandles()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            var selection = Selection.gameObjects;
            if (selection == null)
                return;

            for (var index = 0; index < selection.Length; index++)
            {
                if (selection[index] == null)
                    continue;

                var colliders = selection[index].GetComponents<AuraColliderAuthoring>();
                for (var colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                    Draw(colliders[colliderIndex]);
            }
        }

        private static void Draw(AuraColliderAuthoring collider)
        {
            var transform = collider.transform;
            var serialized = new SerializedObject(collider);
            var centerProperty = serialized.FindProperty("_center");
            if (centerProperty == null)
                return;

            var radiusProperty = serialized.FindProperty("_radius");
            var topRadiusProperty = serialized.FindProperty("_topRadius");
            var heightProperty = serialized.FindProperty("_height");
            var sizeProperty = serialized.FindProperty("_size");
            var normalProperty = serialized.FindProperty("_normal");
            var trigger = serialized.FindProperty("_isTrigger");
            var color = trigger != null && trigger.boolValue ? TriggerColor : SolidColor;

            var world = transform.TransformPoint(centerProperty.vector3Value);
            var rotation = transform.rotation;

            if (MoveCenter(collider, serialized, centerProperty, transform, ref world, rotation))
                return;

            /* Front pass draws the wireframe; the occluded pass fades anything
               hidden behind geometry. Handles are drawn once, on top, so they
               stay easy to grab. */
            Handles.zTest = CompareFunction.LessEqual;
            DrawShapes(collider, serialized, radiusProperty, topRadiusProperty, heightProperty, sizeProperty, normalProperty, world, rotation, color);
            DrawHandles(collider, serialized, radiusProperty, topRadiusProperty, heightProperty, sizeProperty, normalProperty, world, rotation);

            Handles.zTest = CompareFunction.Greater;
            var occluded = OccludedColor;
            occluded.r = color.r;
            occluded.g = color.g;
            occluded.b = color.b;
            DrawShapes(collider, serialized, radiusProperty, topRadiusProperty, heightProperty, sizeProperty, normalProperty, world, rotation, occluded);

            Handles.zTest = CompareFunction.Always;
        }

        private static bool MoveCenter(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty centerProperty, Transform transform, ref Vector3 world, Quaternion rotation)
        {
            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(world, rotation);
            if (!EditorGUI.EndChangeCheck())
                return false;

            Undo.RecordObject(collider, "Move Aura Collider");
            centerProperty.vector3Value = transform.InverseTransformPoint(moved);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
            return true;
        }

        private static void DrawShapes(
            AuraColliderAuthoring collider,
            SerializedObject serialized,
            SerializedProperty radiusProperty,
            SerializedProperty topRadiusProperty,
            SerializedProperty heightProperty,
            SerializedProperty sizeProperty,
            SerializedProperty normalProperty,
            Vector3 world,
            Quaternion rotation,
            Color color)
        {
            Handles.color = color;
            switch (collider.ShapeType)
            {
                case AuraShapeType.Sphere:
                    if (radiusProperty != null)
                    {
                        var r = radiusProperty.floatValue;
                        DrawRing(world, rotation, r);
                        Handles.DrawWireDisc(world, rotation * Vector3.up, r);
                    }
                    break;

                case AuraShapeType.Box:
                    if (sizeProperty != null)
                        DrawBoxWire(world, rotation, sizeProperty.vector3Value * 0.5f);
                    break;

                case AuraShapeType.Cylinder:
                    DrawBody(world, rotation, radiusProperty, radiusProperty, heightProperty, false);
                    break;

                case AuraShapeType.TaperedCylinder:
                    DrawBody(world, rotation, radiusProperty, topRadiusProperty, heightProperty, false);
                    break;

                case AuraShapeType.Capsule:
                    DrawBody(world, rotation, radiusProperty, radiusProperty, heightProperty, true);
                    break;

                case AuraShapeType.TaperedCapsule:
                    DrawBody(world, rotation, radiusProperty, topRadiusProperty, heightProperty, true);
                    break;

                case AuraShapeType.Plane:
                    if (normalProperty != null)
                    {
                        var normal = (rotation * normalProperty.vector3Value.normalized).normalized;
                        Handles.DrawWireDisc(world, normal, 1f);
                        Handles.DrawLine(world, world + normal);
                    }
                    break;
            }
        }

        private static void DrawBody(Vector3 world, Quaternion rotation, SerializedProperty bottomRadiusProperty, SerializedProperty topRadiusProperty, SerializedProperty heightProperty, bool rounded)
        {
            if (bottomRadiusProperty == null || topRadiusProperty == null || heightProperty == null)
                return;

            var height = Mathf.Max(0.001f, heightProperty.floatValue);
            var bottomRadius = Mathf.Max(0.0001f, bottomRadiusProperty.floatValue);
            var topRadius = Mathf.Max(0.0001f, topRadiusProperty.floatValue);
            var up = rotation * Vector3.up;
            var half = height * 0.5f;
            var bodyHalfBottom = rounded ? Mathf.Max(0f, half - bottomRadius) : half;
            var bodyHalfTop = rounded ? Mathf.Max(0f, half - topRadius) : half;

            var bottom = world - up * bodyHalfBottom;
            var top = world + up * bodyHalfTop;

            DrawRing(bottom, rotation, bottomRadius);
            DrawRing(top, rotation, topRadius);
            for (var index = 0; index < Ties.Length; index++)
            {
                var direction = rotation * Ties[index];
                Handles.DrawLine(bottom + direction * bottomRadius, top + direction * topRadius);
            }

            if (!rounded)
                return;

            DrawHalfSphere(bottom, rotation, bottomRadius, false);
            DrawHalfSphere(top, rotation, topRadius, true);
        }

        private static void DrawHandles(
            AuraColliderAuthoring collider,
            SerializedObject serialized,
            SerializedProperty radiusProperty,
            SerializedProperty topRadiusProperty,
            SerializedProperty heightProperty,
            SerializedProperty sizeProperty,
            SerializedProperty normalProperty,
            Vector3 world,
            Quaternion rotation)
        {
            switch (collider.ShapeType)
            {
                case AuraShapeType.Sphere:
                    if (radiusProperty != null)
                        DrawRadius(collider, serialized, radiusProperty, world, rotation);
                    break;

                case AuraShapeType.Box:
                    DrawBoxHandles(collider, serialized, sizeProperty, world, rotation);
                    break;

                case AuraShapeType.Cylinder:
                    DrawBodyHandles(collider, serialized, radiusProperty, radiusProperty, heightProperty, world, rotation);
                    break;

                case AuraShapeType.TaperedCylinder:
                case AuraShapeType.TaperedCapsule:
                    DrawBodyHandles(collider, serialized, radiusProperty, topRadiusProperty, heightProperty, world, rotation);
                    break;

                case AuraShapeType.Capsule:
                    DrawBodyHandles(collider, serialized, radiusProperty, radiusProperty, heightProperty, world, rotation);
                    break;

                case AuraShapeType.Plane:
                    if (normalProperty != null)
                        DrawPlaneNormal(collider, serialized, normalProperty, world, rotation);
                    break;
            }
        }

        private static void DrawRadius(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Quaternion rotation)
        {
            Handles.color = HandleColor;
            var radius = Handles.RadiusHandle(rotation, world, property.floatValue);
            if (Mathf.Approximately(radius, property.floatValue))
                return;

            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = Mathf.Max(0.001f, radius);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static void DrawBodyHandles(
            AuraColliderAuthoring collider,
            SerializedObject serialized,
            SerializedProperty bottomRadiusProperty,
            SerializedProperty topRadiusProperty,
            SerializedProperty heightProperty,
            Vector3 world,
            Quaternion rotation)
        {
            if (bottomRadiusProperty == null || topRadiusProperty == null || heightProperty == null)
                return;

            var height = Mathf.Max(0.001f, heightProperty.floatValue);
            var up = rotation * Vector3.up;
            var half = height * 0.5f;
            var bottom = world - up * half;
            var top = world + up * half;

            Handles.color = HandleColor;
            DrawRadiusSquares(collider, serialized, bottomRadiusProperty, bottom, rotation, Mathf.Max(0.0001f, bottomRadiusProperty.floatValue));
            DrawRadiusSquares(collider, serialized, topRadiusProperty, top, rotation, Mathf.Max(0.0001f, topRadiusProperty.floatValue));
            DrawHeightHandle(collider, serialized, heightProperty, world, up);
        }

        private static void DrawRadiusSquares(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 center, Quaternion rotation, float radius)
        {
            var changed = false;
            var updated = radius;
            var size = HandleUtility.GetHandleSize(center) * 0.07f;

            for (var index = 0; index < Ties.Length; index++)
            {
                var direction = rotation * Ties[index];
                var position = center + direction * radius;

                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(position, size, Vector3.zero, CubeHandleCap);
                if (!EditorGUI.EndChangeCheck())
                    continue;

                updated = Mathf.Max(0.0001f, Vector3.Dot(moved - center, direction));
                changed = true;
            }

            if (!changed)
                return;

            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = updated;
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static void DrawHeightHandle(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Vector3 up)
        {
            var half = Mathf.Max(0.001f, property.floatValue) * 0.5f;
            var size = HandleUtility.GetHandleSize(world) * 0.07f;
            var top = world + up * half;
            var bottom = world - up * half;

            Handles.color = HandleColor;
            var newTop = Handles.Slider(top, up, size, CubeHandleCap, 0f);
            var newBottom = Handles.Slider(bottom, -up, size, CubeHandleCap, 0f);
            if (newTop == top && newBottom == bottom)
                return;

            var height = Vector3.Dot(newTop - newBottom, up);
            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = Mathf.Max(0.001f, height);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static void DrawBoxHandles(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Quaternion rotation)
        {
            if (property == null)
                return;

            var current = property.vector3Value;
            current = new Vector3(Mathf.Max(0.001f, current.x), Mathf.Max(0.001f, current.y), Mathf.Max(0.001f, current.z));
            var extents = current * 0.5f;
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            var size = HandleUtility.GetHandleSize(world) * 0.07f;
            var changed = false;

            Handles.color = HandleColor;
            for (var axis = 0; axis < 3; axis++)
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    var worldDirection = rotation * (axes[axis] * side);
                    var position = world + worldDirection * extents[axis];

                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.FreeMoveHandle(position, size, Vector3.zero, CubeHandleCap);
                    if (!EditorGUI.EndChangeCheck())
                        continue;

                    current[axis] = Mathf.Max(0.001f, Vector3.Dot(moved - world, worldDirection) * 2f);
                    changed = true;
                }
            }

            if (!changed)
                return;

            Undo.RecordObject(collider, "Resize Aura Collider");
            property.vector3Value = current;
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static void DrawRing(Vector3 center, Quaternion rotation, float radius)
        {
            var previous = center + rotation * new Vector3(radius, 0f, 0f);
            for (var index = 1; index <= RingSegments; index++)
            {
                var angle = index * Mathf.PI * 2f / RingSegments;
                var point = center + rotation * new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Handles.DrawLine(previous, point);
                previous = point;
            }
        }

        /* Half sphere on a cap. No seam ring is drawn where the cap meets the
           body: the body ring already marks that boundary. */
        private static void DrawHalfSphere(Vector3 center, Quaternion rotation, float radius, bool top)
        {
            var axis = rotation * Vector3.up * (top ? 1f : -1f);
            var right = rotation * Vector3.right;
            var forward = rotation * Vector3.forward;

            for (var meridian = 0; meridian < HalfSphereMeridians; meridian++)
            {
                var planeAxis = rotation * Quaternion.AngleAxis(meridian * 180f / HalfSphereMeridians, Vector3.up) * Vector3.right;
                var previous = center + planeAxis * radius;
                for (var index = 1; index <= HalfSphereSegments; index++)
                {
                    var angle = index * Mathf.PI / HalfSphereSegments;
                    var point = center + planeAxis * (Mathf.Cos(angle) * radius) + axis * (Mathf.Sin(angle) * radius);
                    Handles.DrawLine(previous, point);
                    previous = point;
                }
            }
        }

        private static void DrawBoxWire(Vector3 center, Quaternion rotation, Vector3 extents)
        {
            var corners = new Vector3[8];
            for (var index = 0; index < 8; index++)
            {
                var sign = new Vector3(
                    (index & 1) == 0 ? -1f : 1f,
                    (index & 2) == 0 ? -1f : 1f,
                    (index & 4) == 0 ? -1f : 1f);
                corners[index] = center + rotation * new Vector3(sign.x * extents.x, sign.y * extents.y, sign.z * extents.z);
            }

            for (var index = 0; index < 8; index++)
            {
                for (var bit = 1; bit <= 4; bit <<= 1)
                {
                    var neighbour = index ^ bit;
                    if (neighbour > index)
                        Handles.DrawLine(corners[index], corners[neighbour]);
                }
            }
        }

        private static void DrawPlaneNormal(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Quaternion rotation)
        {
            var localNormal = property.vector3Value.sqrMagnitude < 1e-6f ? Vector3.up : property.vector3Value.normalized;
            var worldNormal = (rotation * localNormal).normalized;
            var handleRotation = Quaternion.LookRotation(worldNormal, Mathf.Abs(Vector3.Dot(worldNormal, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up);

            Handles.color = HandleColor;
            EditorGUI.BeginChangeCheck();
            var newRotation = Handles.RotationHandle(handleRotation, world);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(collider, "Orient Aura Plane");
            property.vector3Value = Quaternion.Inverse(rotation) * (newRotation * Vector3.forward);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        /* CubeHandleCap looks 3D; a billboard quad reads as a flat white square
           facing the camera. It is drawn through Handles.RectangleHandleCap's
           wireframe plus a filled quad via Handles.DrawSolidRectangleWithOutline. */
        private static void CubeHandleCap(int controlId, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            if (eventType != EventType.Repaint && eventType != EventType.Layout)
                return;

            if (eventType == EventType.Layout)
            {
                HandleUtility.AddControl(controlId, HandleUtility.DistanceToRectangle(position, rotation, size));
                return;
            }

            var camera = Camera.current;
            var normal = camera != null ? (camera.transform.position - position).normalized : rotation * Vector3.forward;
            var billboard = Quaternion.LookRotation(normal, camera != null ? camera.transform.up : Vector3.up);
            var half = size * 0.5f;
            var a = position + billboard * new Vector3(-half, -half, 0f);
            var b = position + billboard * new Vector3(half, -half, 0f);
            var c = position + billboard * new Vector3(half, half, 0f);
            var d = position + billboard * new Vector3(-half, half, 0f);

            Handles.DrawSolidRectangleWithOutline(new[] { a, b, c, d }, HandleColor, HandleColor);
        }

        private static void MarkDirty(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
