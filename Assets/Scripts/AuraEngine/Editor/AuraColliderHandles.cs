using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;

namespace AuraEngine.EditorTools
{
    [InitializeOnLoad]
    public static class AuraColliderHandles
    {
        static AuraColliderHandles()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
                return;

            for (var index = 0; index < selection.Length; index++)
            {
                var go = selection[index];
                if (go == null)
                    continue;

                var colliders = go.GetComponents<AuraColliderAuthoring>();
                for (var i = 0; i < colliders.Length; i++)
                    Draw(colliders[i]);
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
            var scaleProperty = serialized.FindProperty("_scale");

            var world = transform.TransformPoint(centerProperty.vector3Value);

            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(world, transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collider, "Move Aura Collider");
                centerProperty.vector3Value = transform.InverseTransformPoint(moved);
                serialized.ApplyModifiedProperties();
                MarkDirty(collider);
                return;
            }

            world = transform.TransformPoint(centerProperty.vector3Value);
            var rotation = transform.rotation;
            var up = rotation * Vector3.up;

            switch (collider.ShapeType)
            {
                case AuraShapeType.Sphere:
                    DrawRadius(collider, serialized, radiusProperty, world, rotation);
                    break;

                case AuraShapeType.Box:
                    DrawSize(collider, serialized, sizeProperty, world, rotation);
                    break;

                case AuraShapeType.Capsule:
                case AuraShapeType.Cylinder:
                {
                    DrawRadius(collider, serialized, radiusProperty, world, rotation);
                    DrawHeight(collider, serialized, heightProperty, world, up);
                    break;
                }

                case AuraShapeType.TaperedCapsule:
                case AuraShapeType.TaperedCylinder:
                {
                    DrawRadius(collider, serialized, radiusProperty, world, rotation);
                    DrawTopRadius(collider, serialized, topRadiusProperty, heightProperty, world, rotation, up);
                    DrawHeight(collider, serialized, heightProperty, world, up);
                    break;
                }

                case AuraShapeType.Plane:
                    DrawPlaneNormal(collider, serialized, normalProperty, world, rotation);
                    break;

                case AuraShapeType.ConvexMesh:
                case AuraShapeType.TriangleMesh:
                    DrawScale(collider, serialized, scaleProperty, world, rotation);
                    break;
            }
        }

        private static void DrawScale(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Quaternion rotation)
        {
            if (property == null)
                return;

            var current = property.vector3Value;
            if (current.x == 0f && current.y == 0f && current.z == 0f)
                current = Vector3.one;

            var scale = Handles.ScaleHandle(current, world, rotation, HandleUtility.GetHandleSize(world));
            if (scale == current)
                return;

            Undo.RecordObject(collider, "Scale Aura Collider");
            property.vector3Value = new Vector3(
                Mathf.Max(1e-4f, Mathf.Abs(scale.x)),
                Mathf.Max(1e-4f, Mathf.Abs(scale.y)),
                Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
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

        private static void DrawTopRadius(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty topRadiusProperty, SerializedProperty heightProperty, Vector3 world, Quaternion rotation, Vector3 up)
        {
            if (topRadiusProperty == null || heightProperty == null)
                return;

            var top = world + up * (heightProperty.floatValue * 0.5f);
            var radius = Handles.RadiusHandle(rotation, top, topRadiusProperty.floatValue);
            if (Mathf.Approximately(radius, topRadiusProperty.floatValue))
                return;

            Undo.RecordObject(collider, "Resize Aura Collider");
            topRadiusProperty.floatValue = Mathf.Max(0f, radius);
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static void DrawSize(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Quaternion rotation)
        {
            if (property == null)
                return;

            var size = Handles.ScaleHandle(property.vector3Value, world, rotation, HandleUtility.GetHandleSize(world));
            if (size == property.vector3Value)
                return;

            Undo.RecordObject(collider, "Resize Aura Collider");
            property.vector3Value = new Vector3(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y), Mathf.Max(0.001f, size.z));
            serialized.ApplyModifiedProperties();
            MarkDirty(collider);
        }

        private static void DrawHeight(AuraColliderAuthoring collider, SerializedObject serialized, SerializedProperty property, Vector3 world, Vector3 up)
        {
            if (property == null)
                return;

            var half = property.floatValue * 0.5f;
            var top = world + up * half;
            var bottom = world - up * half;

            EditorGUI.BeginChangeCheck();
            var newTop = Handles.Slider(top, up, HandleUtility.GetHandleSize(top) * 0.15f, Handles.SphereHandleCap, 0f);
            var newBottom = Handles.Slider(bottom, -up, HandleUtility.GetHandleSize(bottom) * 0.15f, Handles.SphereHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck())
                return;

            var height = Vector3.Dot(newTop - newBottom, up);
            Undo.RecordObject(collider, "Resize Aura Collider");
            property.floatValue = Mathf.Max(0.001f, height);
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
