using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;

namespace AuraEngine.EditorTools
{
    [CustomEditor(typeof(AuraPhysicsBodyAuthoring))]
    public sealed class AuraPhysicsBodyAuthoringEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var authoring = (AuraPhysicsBodyAuthoring)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AuraEngine", EditorStyles.boldLabel);

            if (authoring.TryBuildDefinition(out var definition, out var result))
            {
                EditorGUILayout.HelpBox(
                    $"Valid: {definition.Shapes.Length} shape(s), layer {definition.Layer.Value}, type {definition.Type}.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Invalid physics definition: {result}. Add an Aura collider authoring component.", MessageType.Warning);
            }
        }
    }
}
