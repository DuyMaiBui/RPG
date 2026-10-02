using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;

namespace AuraEngine.EditorTools
{
    /* Scene-view point handles for Aura colliders. Only the values that define
       the shape are exposed as drag points; no wire shape is drawn.

         Box              six face points (per-axis size)
         Sphere           one radius handle
         Cylinder         four radial points (radius) + two cap points (height)
         TaperedCylinder  four bottom + four top radial points + two cap points
         Capsule          four radial points (radius) + two cap points (height)
         TaperedCapsule   four bottom + four top radial points + two cap points

       Radial points sit on the local XZ circle; dragging one changes the shared
       (bottom) radius, or the top radius for the upper ring. */
    [InitializeOnLoad]
    public static class AuraColliderHandles
    {
        private static readonly Vector3[] RadialDirections =
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, -1f),
        };

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

            var world = transform.TransformPoint(centerProperty.vector3Value);
            var rotation = transform.rotation;
            var up = rotation * Vector3.up;

            if (MoveCenter(collider, serialized, centerProperty, transform, ref world, rotation))
                return;

            switch (collider.ShapeType)
            {
                case AuraShapeType.Sphere:
                    DrawRadius(collider, serialized, radiusProperty, world, rotation);
                    break;

                case AuraShapeType.Box:
                    DrawBoxSize(collider, serialized, sizeProperty, transform, centerProperty, world);
                    break;

                case AuraShapeType.Cylinder:
                    DrawBottomRadius(collider, serialized, radiusProperty, world, rotation);
                    DrawHeight(collider, serialized, heightProperty, world, up);
                    break;

                case AuraShapeType.TaperedCylinder:
                    DrawBottomRadius(collider, serialized, radiusProperty, world, rotation);
                    DrawTopRadius(collider, serialized, topRadiusProperty, heightProperty, world, rotation, up);
                    DrawHeight(collider, serialized, heightProperty, world, up);
                    break;

                case AuraShapeType.Capsule:
                    DrawBottomRadius(collider, serialized, radiusProperty, world, rotation);
                    DrawHeight(collider, serialized, heightProperty, world, up);
                    break;

                case AuraShapeType.TaperedCapsule:
                    DrawBottomRadius(collider, serialized, radiusProperty, world, rotation);
                    DrawTopRadius(collider, serialized, topRadiusProperty, heightProperty, world, rotation, up);
                    DrawHeight(collider, serialized, heightProperty, world, up);
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

        private static void DrawRadius(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Quaternion rotation)
        {
            if (property == null)
                return;

            var radius = Handles.RadiusHandle(rotation, world, property.floatValue);
            if (Mathf.Approximately(radius, property.floatValue))
                return;

            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = Mathf.Max(0.001f, radius);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        /* Four points on the local XZ circle. Dragging any one sets the shared
           bottom radius from the centre-to-pointer distance. */
        private static void DrawBottomRadius(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Quaternion rotation)
        {
            var updated = DrawRadialPoints(property == null ? 1f : Mathf.Max(0.001f, property.floatValue), world, rotation, out var changed);
            if (!changed)
                return;

            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = Mathf.Max(0.001f, updated);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static void DrawTopRadius(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, SerializedProperty heightProperty, Vector3 world, Quaternion rotation, Vector3 up)
        {
            if (property == null || heightProperty == null)
                return;

            var top = world + up * (heightProperty.floatValue * 0.5f);
            var updated = DrawRadialPoints(property.floatValue, top, rotation, out var changed);
            if (!changed)
                return;

            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = Mathf.Max(0f, updated);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static float DrawRadialPoints(float radius, Vector3 center, Quaternion rotation, out bool changed)
        {
            changed = false;
            var result = radius;
            var handleSize = HandleUtility.GetHandleSize(center) * 0.08f;

            for (var index = 0; index < RadialDirections.Length; index++)
            {
                var direction = rotation * RadialDirections[index];
                var position = center + direction * Mathf.Max(radius, 0.0001f);

                EditorGUI.BeginChangeCheck();
                var moved = Handles.Slider(position, direction, handleSize, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    result = Vector3.Dot(moved - center, direction);
                    changed = true;
                }
            }

            return result;
        }

        /* Two points on the local up axis; dragging one changes the total
           height while the centre stays fixed. */
        private static void DrawHeight(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Vector3 up)
        {
            if (property == null)
                return;

            var half = Mathf.Max(0.001f, property.floatValue) * 0.5f;
            var handleSize = HandleUtility.GetHandleSize(world) * 0.08f;
            var top = world + up * half;
            var bottom = world - up * half;

            EditorGUI.BeginChangeCheck();
            var newTop = Handles.Slider(top, up, handleSize, Handles.DotHandleCap, 0f);
            var newBottom = Handles.Slider(bottom, -up, handleSize, Handles.DotHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck())
                return;

            var height = Vector3.Dot(newTop - newBottom, up);
            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = Mathf.Max(0.001f, height);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        /* Six face points; dragging one changes the size along that local axis
           and keeps the centre fixed. */
        private static void DrawBoxSize(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Transform transform, SerializedProperty centerProperty, Vector3 world)
        {
            if (property == null)
                return;

            var current = property.vector3Value;
            current = new Vector3(Mathf.Max(0.001f, current.x), Mathf.Max(0.001f, current.y), Mathf.Max(0.001f, current.z));
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            var handleSize = HandleUtility.GetHandleSize(world) * 0.08f;
            var changed = false;

            for (var axis = 0; axis < 3; axis++)
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    var localDirection = axes[axis] * side;
                    var worldDirection = transform.rotation * localDirection;
                    var half = current[axis] * 0.5f;
                    var position = world + worldDirection * half;

                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.Slider(position, worldDirection, handleSize, Handles.DotHandleCap, 0f);
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

        private static void DrawPlaneNormal(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Quaternion rotation)
        {
            if (property == null)
                return;

            var localNormal = property.vector3Value.sqrMagnitude < 1e-6f ? Vector3.up : property.vector3Value.normalized;
            var worldNormal = (rotation * localNormal).normalized;
            var handleRotation = Quaternion.LookRotation(worldNormal, Mathf.Abs(Vector3.Dot(worldNormal, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up);

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
