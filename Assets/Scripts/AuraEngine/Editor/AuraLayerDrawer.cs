using AuraEngine.Unity;
using UnityEditor;
using UnityEngine;

namespace AuraEngine.EditorTools
{
    [CustomPropertyDrawer(typeof(AuraLayerAttribute))]
    public sealed class AuraLayerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var layers = FindLayers(property);
            if (layers == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var indices = layers.GetNamedIndices();
            var count = indices.Length;
            if (count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var names = new string[count];
            var selected = 0;
            for (var i = 0; i < count; i++)
            {
                names[i] = $"{indices[i]}: {layers.GetName(indices[i])}";
                if (indices[i] == property.intValue)
                    selected = i;
            }

            EditorGUI.BeginProperty(position, label, property);
            selected = EditorGUI.Popup(position, label.text, selected, names);
            property.intValue = indices[selected];
            EditorGUI.EndProperty();
        }

        private static AuraPhysicsLayers FindLayers(SerializedProperty property)
        {
            var serialized = property.serializedObject;
            var layersProperty = serialized.FindProperty("_layers");
            if (layersProperty != null && layersProperty.objectReferenceValue is AuraPhysicsLayers layers)
                return layers;

            if (serialized.targetObject is Component component)
            {
                var instance = component.GetComponentInParent<AuraSimulationInstance>();
                if (instance != null && instance.Layers != null)
                    return instance.Layers;
            }

            return null;
        }
    }
}
