using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

namespace AuraEngine.EditorTools
{
#if ODIN_INSPECTOR
    [CustomEditor(typeof(AuraSimulationInstance))]
    public sealed class AuraSimulationInstanceEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawAuraSettings((AuraSimulationInstance)target);
        }

        private static void DrawAuraSettings(AuraSimulationInstance instance)
        {
            DrawLayers(instance);
            DrawRuntime(instance);
        }
#else
    [CustomEditor(typeof(AuraSimulationInstance))]
    public sealed class AuraSimulationInstanceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawAuraSettings((AuraSimulationInstance)target);
        }

        private static void DrawAuraSettings(AuraSimulationInstance instance)
        {
            DrawLayers(instance);
            DrawRuntime(instance);
        }
#endif

        private static void DrawLayers(AuraSimulationInstance instance)
        {
            if (instance.Layers != null)
            {
                AuraCollisionMatrixGUI.Draw(instance.Layers);
            }
            else if (GUILayout.Button("Create Default Physics Layers Asset"))
            {
                CreateDefault(instance);
            }
            else
            {
                EditorGUILayout.HelpBox("Assign an AuraPhysicsLayers asset to edit layer names and the collision matrix here.", MessageType.Info);
            }
        }

        private static void DrawRuntime(AuraSimulationInstance instance)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AuraEngine runtime", EditorStyles.boldLabel);

            if (!Application.isPlaying || !instance.IsCreated)
            {
                EditorGUILayout.HelpBox("Not running. Enter Play Mode to inspect the simulation.", MessageType.None);
                return;
            }

            var world = instance.World;
            EditorGUILayout.LabelField("Backend", instance.BackendName);
            EditorGUILayout.LabelField("Tick", world.CurrentTick.Value.ToString());
            EditorGUILayout.LabelField("Entities", world.EntityCount.ToString());
            EditorGUILayout.LabelField("Bodies", world.BodyCount.ToString());
            EditorGUILayout.LabelField("Pending events", world.PendingEventCount.ToString());
            EditorGUILayout.LabelField("State hash", world.ComputeStateHash().ToString("X16"));
            EditorGUILayout.LabelField("Capabilities", world.Capabilities.ToString());
        }

        private static void CreateDefault(AuraSimulationInstance instance)
        {
            var path = EditorUtility.SaveFilePanelInProject("Save Physics Layers", "AuraPhysicsLayers", "asset", "Choose a location");
            if (string.IsNullOrEmpty(path))
                return;

            var asset = ScriptableObject.CreateInstance<AuraPhysicsLayers>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            var serialized = new SerializedObject(instance);
            serialized.FindProperty("_layers").objectReferenceValue = asset;
            serialized.ApplyModifiedProperties();
        }
    }
}
