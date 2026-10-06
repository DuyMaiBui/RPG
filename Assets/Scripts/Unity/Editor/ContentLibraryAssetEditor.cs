#if UNITY_EDITOR
using RPG.Content;
using UnityEditor;
using UnityEngine;

namespace RPG.Unity.Editor
{
    /// <summary>Renders the authored-content validation report inline so a broken asset is visible while editing
    /// instead of only at scene start.</summary>
    [CustomEditor(typeof(ContentLibraryAsset))]
    public sealed class ContentLibraryAssetEditor : UnityEditor.Editor
    {
        private ContentValidationResult _validation;

        private void OnEnable() => Refresh();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("Validate content"))
                Refresh();

            if (_validation == null)
            {
                EditorGUILayout.HelpBox("Validation has not run.", MessageType.Info);
                return;
            }

            if (_validation.IsValid)
            {
                EditorGUILayout.HelpBox("Content is valid.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"{_validation.Errors.Count} content error(s)", MessageType.Error);
            for (var index = 0; index < _validation.Errors.Count; index++)
            {
                var error = _validation.Errors[index];
                EditorGUILayout.HelpBox($"{error.Code}: {error.Message}\nSource: {error.Source}", MessageType.Error);
            }
        }

        private void Refresh()
        {
            var library = target as ContentLibraryAsset;
            _validation = library == null ? null : library.Validate();
        }
    }
}
#endif
