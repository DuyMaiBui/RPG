using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AuraEngine.Tests
{
    /* Writes [SerializeField] values through UnityEditor.SerializedObject so tests exercise the real serialized
       path (the one scenes and prefabs use) instead of private reflection. A missing field name fails the test. */
    public static class AuraSerializedFields
    {
        public static void SetFloat(Object target, string field, float value) => Apply(target, field, property => property.floatValue = value);

        public static void SetInt(Object target, string field, int value) => Apply(target, field, property => property.intValue = value);

        public static void SetBool(Object target, string field, bool value) => Apply(target, field, property => property.boolValue = value);

        public static void SetVector2(Object target, string field, Vector2 value) => Apply(target, field, property => property.vector2Value = value);

        public static void SetVector3(Object target, string field, Vector3 value) => Apply(target, field, property => property.vector3Value = value);

        public static void SetEnum(Object target, string field, Enum value) =>
            Apply(target, field, property =>
            {
                var index = Array.IndexOf(property.enumNames, value.ToString());
                Assert.GreaterOrEqual(index, 0, $"Enum value {value} is not serialized by '{field}'.");
                property.enumValueIndex = index;
            });

        public static void SetReference(Object target, string field, Object value) => Apply(target, field, property => property.objectReferenceValue = value);

        public static void SetReferences(Object target, string field, Object[] values) =>
            Apply(target, field, property =>
            {
                property.arraySize = values.Length;
                for (var index = 0; index < values.Length; index++)
                    property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            });

        public static float GetFloat(Object target, string field)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"{target.GetType().Name} has no serialized field '{field}'.");
            return property.floatValue;
        }

        private static void Apply(Object target, string field, Action<SerializedProperty> write)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            Assert.IsNotNull(property, $"{target.GetType().Name} has no serialized field '{field}'.");
            write(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
