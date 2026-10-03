using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;

namespace AuraEngine.EditorTools
{
    /* Scene-view authoring for Aura colliders. Draws a depth-tested wireframe
       (so parts behind geometry look dimmer, like the built-in sphere radius
       handle) together with the radius/height drag points.

         Sphere           one radius handle (unchanged)
         Box              twelve edges + six face points
         Cylinder         two rings + four vertical ties + four radius points
         TaperedCylinder  two rings (bottom/top radius points) + four ties
         Capsule          body rings + a half sphere on each cap + radius/height
         TaperedCapsule   tapered body rings + a half sphere on each cap

       The wireframe and the handles are both drawn in the Handles pass so they
       share the same depth testing as the native sphere handle. */
    [InitializeOnLoad]
    public static class AuraColliderHandles
    {
        private const int RingSegments = 32;
        private const int HalfSphereSegments = 10;

        private static readonly Vector3[] Ties =
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, -1f),
        };

        private static readonly Color SolidColor = new Color(1f, 0.6f, 0.1f);
        private static readonly Color TriggerColor = new Color(0.2f, 0.8f, 1f);

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

            switch (collider.ShapeType)
            {
                case AuraShapeType.Sphere:
                    DrawSphere(collider, serialized, radiusProperty, world, rotation, color);
                    break;

                case AuraShapeType.Box:
                    DrawBox(collider, serialized, sizeProperty, world, rotation, color);
                    break;

                case AuraShapeType.Cylinder:
                    DrawCylinder(collider, serialized, radiusProperty, radiusProperty, heightProperty, world, rotation, color, false);
                    break;

                case AuraShapeType.TaperedCylinder:
                    DrawCylinder(collider, serialized, radiusProperty, topRadiusProperty, heightProperty, world, rotation, color, false);
                    break;

                case AuraShapeType.Capsule:
                    DrawCylinder(collider, serialized, radiusProperty, radiusProperty, heightProperty, world, rotation, color, true);
                    break;

                case AuraShapeType.TaperedCapsule:
                    DrawCylinder(collider, serialized, radiusProperty, topRadiusProperty, heightProperty, world, rotation, color, true);
                    break;

                case AuraShapeType.Plane:
                    DrawPlaneNormal(collider, serialized, normalProperty, world, rotation);
                    break;
            }
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

        private static void DrawSphere(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty radiusProperty, Vector3 world, Quaternion rotation, Color color)
        {
            if (radiusProperty == null)
                return;

            Handles.color = color;
            Handles.DrawWireDisc(world, rotation * Vector3.up, radiusProperty.floatValue);
            Handles.DrawWireDisc(world, rotation * Vector3.right, radiusProperty.floatValue);

            var radius = Handles.RadiusHandle(rotation, world, radiusProperty.floatValue);
            if (Mathf.Approximately(radius, radiusProperty.floatValue))
                return;

            Undo.RecordObject(collider, "Resize Aura Collider");
            radiusProperty.floatValue = Mathf.Max(0.001f, radius);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static void DrawCylinder(
            AuraColliderAuthoring collider,
            SerializedObject serialized,
            SerializedProperty bottomRadiusProperty,
            SerializedProperty topRadiusProperty,
            SerializedProperty heightProperty,
            Vector3 world,
            Quaternion rotation,
            Color color,
            bool rounded)
        {
            if (bottomRadiusProperty == null || topRadiusProperty == null || heightProperty == null)
                return;

            var height = Mathf.Max(0.001f, heightProperty.floatValue);
            var bottomRadius = Mathf.Max(0.0001f, bottomRadiusProperty.floatValue);
            var topRadius = Mathf.Max(0.0001f, topRadiusProperty.floatValue);
            var up = rotation * Vector3.up;
            var half = height * 0.5f;
            var bodyHalf = rounded ? Mathf.Max(0f, half - bottomRadius) : half;
            var bodyHalfTop = rounded ? Mathf.Max(0f, half - topRadius) : half;

            var bottom = world - up * bodyHalf;
            var top = world + up * bodyHalfTop;

            Handles.color = color;
            DrawRing(bottom, rotation, bottomRadius);
            DrawRing(top, rotation, topRadius);
            for (var index = 0; index < Ties.Length; index++)
            {
                var direction = rotation * Ties[index];
                Handles.DrawLine(bottom + direction * bottomRadius, top + direction * topRadius);
            }

            if (rounded)
            {
                DrawHalfSphere(bottom, rotation, bottomRadius, false);
                DrawHalfSphere(top, rotation, topRadius, true);
            }

            DrawRadiusPoints(collider, serialized, bottomRadiusProperty, bottom, rotation, bottomRadius);
            var topRadiusUpdated = DrawRadiusPoints(collider, serialized, topRadiusProperty, top, rotation, topRadius);
            if (topRadiusUpdated)
            {
                heightProperty.floatValue = Mathf.Max(0.001f, height);
                serialized.ApplyModifiedProperties();
            }

            DrawHeight(collider, serialized, heightProperty, world, up);
        }

        /* Draws the four radius points on a ring; returns true when the radius
           changed. Dragging any point sets the shared radius from the distance
           to the held point. */
        private static bool DrawRadiusPoints(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 center, Quaternion rotation, float radius)
        {
            var changed = false;
            var updated = radius;
            var handleSize = HandleUtility.GetHandleSize(center) * 0.06f;

            for (var index = 0; index < Ties.Length; index++)
            {
                var direction = rotation * Ties[index];
                var position = center + direction * radius;

                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(position, handleSize, Vector3.zero, Handles.CircleHandleCap);
                if (!EditorGUI.EndChangeCheck())
                    continue;

                var projected = Vector3.Dot(moved - center, direction);
                updated = Mathf.Max(0.0001f, projected);
                changed = true;
            }

            if (!changed)
                return false;

            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = updated;
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
            return true;
        }

        private static void DrawHeight(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Vector3 up)
        {
            var half = Mathf.Max(0.001f, property.floatValue) * 0.5f;
            var handleSize = HandleUtility.GetHandleSize(world) * 0.06f;
            var top = world + up * half;
            var bottom = world - up * half;

            Handles.color = new Color(1f, 0.6f, 0.1f);
            var newTop = Handles.Slider(top, up, handleSize, Handles.SphereHandleCap, 0f);
            var newBottom = Handles.Slider(bottom, -up, handleSize, Handles.SphereHandleCap, 0f);

            if (newTop == top && newBottom == bottom)
                return;

            var height = Vector3.Dot(newTop - newBottom, up);
            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = Mathf.Max(0.001f, height);
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

        /* Half sphere on a cap, matching how the sphere collider is drawn. */
        private static void DrawHalfSphere(Vector3 center, Quaternion rotation, float radius, bool top)
        {
            var axis = rotation * Vector3.up * (top ? 1f : -1f);
            var right = rotation * Vector3.right;
            var forward = rotation * Vector3.forward;

            for (var meridian = 0; meridian < 2; meridian++)
            {
                var planeAxis = meridian == 0 ? right : forward;
                var previous = center + planeAxis * radius;
                for (var index = 1; index <= HalfSphereSegments; index++)
                {
                    var angle = index * Mathf.PI / HalfSphereSegments;
                    var point = center + planeAxis * (Mathf.Cos(angle) * radius) + axis * (Mathf.Sin(angle) * radius);
                    Handles.DrawLine(previous, point);
                    previous = point;
                }
            }

            var ringCenter = center + axis * radius;
            var ringPrevious = ringCenter + right * radius;
            for (var index = 1; index <= RingSegments; index++)
            {
                var angle = index * Mathf.PI * 2f / RingSegments;
                var point = ringCenter + right * (Mathf.Cos(angle) * radius) + forward * (Mathf.Sin(angle) * radius);
                Handles.DrawLine(ringPrevious, point);
                ringPrevious = point;
            }
        }

        private static void DrawBox(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Quaternion rotation, Color color)
        {
            if (property == null)
                return;

            var current = property.vector3Value;
            current = new Vector3(Mathf.Max(0.001f, current.x), Mathf.Max(0.001f, current.y), Mathf.Max(0.001f, current.z));
            var extents = current * 0.5f;

            Handles.color = color;
            DrawBoxWire(world, rotation, extents);

            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            var handleSize = HandleUtility.GetHandleSize(world) * 0.06f;
            var changed = false;

            for (var axis = 0; axis < 3; axis++)
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    var localDirection = axes[axis] * side;
                    var worldDirection = rotation * localDirection;
                    var position = world + worldDirection * extents[axis];

                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.FreeMoveHandle(position, handleSize, Vector3.zero, Handles.CircleHandleCap);
                    if (!EditorGUI.EndChangeCheck())
                        continue;

                    var extent = Vector3.Dot(moved - world, worldDirection);
                    current[axis] = Mathf.Max(0.001f, extent * 2f);
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
            if (property == null)
                return;

            var localNormal = property.vector3Value.sqrMagnitude < 1e-6f ? Vector3.up : property.vector3Value.normalized;
            var worldNormal = (rotation * localNormal).normalized;
            var handleRotation = Quaternion.LookRotation(worldNormal, Mathf.Abs(Vector3.Dot(worldNormal, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up);

            Handles.color = new Color(1f, 0.6f, 0.1f);
            Handles.DrawWireDisc(world, worldNormal, 1f);
            Handles.DrawLine(world, world + worldNormal);

            EditorGUI.BeginChangeCheck();
            var newRotation = Handles.RotationHandle(handleRotation, world);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(collider, "Orient Aura Plane");
            property.vector3Value = Quaternion.Inverse(rotation) * (newRotation * Vector3.forward);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static void MarkDirty(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
