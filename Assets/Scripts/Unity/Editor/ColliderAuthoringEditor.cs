#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RPG.Unity.Editor
{
    [CustomEditor(typeof(ColliderAuthoring), true)]
    public sealed class ColliderAuthoringEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var authoring = (ColliderAuthoring)target;
            var offset = serializedObject.FindProperty("_localOffset");
            var rotation = serializedObject.FindProperty("_localRotationDegrees");
            var worldOffset = authoring.transform.TransformPoint(new Vector3(offset.vector2Value.x, offset.vector2Value.y, 0f));
            var moved = Handles.PositionHandle(worldOffset, authoring.transform.rotation);
            var local = authoring.transform.InverseTransformPoint(moved);
            if ((moved - worldOffset).sqrMagnitude > 0.000001f)
            {
                Undo.RecordObject(authoring, "Move Collider Shape");
                offset.vector2Value = new Vector2(local.x, local.y);
                serializedObject.ApplyModifiedProperties();
            }

            var worldRotation = authoring.transform.rotation * Quaternion.Euler(0f, 0f, rotation.floatValue);
            var rotated = Handles.RotationHandle(worldRotation, worldOffset);
            var localRotation = Quaternion.Inverse(authoring.transform.rotation) * rotated;
            var angle = localRotation.eulerAngles.z;
            if (Mathf.Abs(Mathf.DeltaAngle(rotation.floatValue, angle)) > 0.01f)
            {
                Undo.RecordObject(authoring, "Rotate Collider Shape");
                rotation.floatValue = angle;
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
#endif
