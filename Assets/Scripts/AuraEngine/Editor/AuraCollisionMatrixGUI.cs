using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;

namespace AuraEngine.EditorTools
{
    public static class AuraCollisionMatrixGUI
    {
        private const float NameWidth = 130f;
        private const float CellSize = 16f;

        private static Vector2 _scroll;
        private static string _newLayerName = string.Empty;

        public static void Draw(AuraPhysicsLayers layers)
        {
            if (layers == null)
                return;

            DrawLayers(layers);
            DrawMatrix(layers);
        }

        private static void DrawLayers(AuraPhysicsLayers layers)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);

            var indices = layers.GetNamedIndices();
            var dirty = false;

            for (var i = 0; i < indices.Length; i++)
            {
                var index = indices[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(24f));
                    if (index == 0)
                    {
                        EditorGUILayout.LabelField(AuraPhysicsLayers.DefaultLayerName);
                        continue;
                    }

                    var current = layers.GetName(index);
                    var value = EditorGUILayout.TextField(current);
                    if (value != current)
                    {
                        layers.SetName(index, value);
                        dirty = true;
                    }

                    if (GUILayout.Button("✕", GUILayout.Width(22f)))
                    {
                        Undo.RecordObject(layers, "Remove Aura Layer");
                        layers.SetName(index, string.Empty);
                        dirty = true;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _newLayerName = EditorGUILayout.TextField("New Layer", _newLayerName);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newLayerName)))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(60f)))
                    {
                        Undo.RecordObject(layers, "Add Aura Layer");
                        layers.AddLayer(_newLayerName.Trim());
                        _newLayerName = string.Empty;
                        dirty = true;
                    }
                }
            }

            if (dirty)
                EditorUtility.SetDirty(layers);

            EditorGUILayout.HelpBox("Layers without a name cannot interact and are not shown in the matrix or selectable on bodies.", MessageType.None);
        }

        private static void DrawMatrix(AuraPhysicsLayers layers)
        {
            var indices = layers.GetNamedIndices();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Collision Matrix", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All", GUILayout.Width(60f)))
                {
                    Undo.RecordObject(layers, "Aura Collision Matrix");
                    layers.SetAll(true);
                    EditorUtility.SetDirty(layers);
                }

                if (GUILayout.Button("None", GUILayout.Width(60f)))
                {
                    Undo.RecordObject(layers, "Aura Collision Matrix");
                    layers.SetAll(false);
                    EditorUtility.SetDirty(layers);
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, true, true, GUILayout.Height(220f));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(string.Empty, GUILayout.Width(NameWidth));
                for (var i = 0; i < indices.Length; i++)
                    GUILayout.Label(indices[i].ToString(), EditorStyles.miniLabel, GUILayout.Width(CellSize));
            }

            var dirty = false;
            for (var r = 0; r < indices.Length; r++)
            {
                var row = indices[r];
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{row}: {layers.GetName(row)}", GUILayout.Width(NameWidth));
                    for (var c = 0; c < indices.Length; c++)
                    {
                        var column = indices[c];
                        var current = layers.CanCollide(row, column);
                        var value = EditorGUILayout.Toggle(current, GUILayout.Width(CellSize));
                        if (value != current)
                        {
                            layers.SetCollision(row, column, value);
                            dirty = true;
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            if (dirty)
                EditorUtility.SetDirty(layers);
        }
    }
}
