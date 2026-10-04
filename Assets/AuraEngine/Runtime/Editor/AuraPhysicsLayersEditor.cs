using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

namespace AuraEngine.EditorTools
{
#if ODIN_INSPECTOR
    [CustomEditor(typeof(AuraPhysicsLayers))]
    public sealed class AuraPhysicsLayersEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            AuraCollisionMatrixGUI.Draw((AuraPhysicsLayers)target);
        }
    }
#else
    [CustomEditor(typeof(AuraPhysicsLayers))]
    public sealed class AuraPhysicsLayersEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            AuraCollisionMatrixGUI.Draw((AuraPhysicsLayers)target);
        }
    }
#endif
}
