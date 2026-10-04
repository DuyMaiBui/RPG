using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public static class AuraUnityConversions
    {
        public static Vector3 ToUnity(this AuraVector3 value) => new Vector3(value.X, value.Y, value.Z);

        public static Vector3 ToUnity(this AuraVector2 value) => new Vector3(value.X, value.Y, 0f);

        public static Quaternion ToUnity(this AuraQuaternion value) => new Quaternion(value.X, value.Y, value.Z, value.W);

        public static AuraVector3 ToAura(this Vector3 value) => new AuraVector3(value.x, value.y, value.z);

        public static AuraVector2 ToAuraVector2(this Vector2 value) => new AuraVector2(value.x, value.y);

        public static AuraQuaternion ToAura(this Quaternion value) =>
            new AuraQuaternion(value.x, value.y, value.z, value.w);

        public static AuraPose ToAuraPose(this Transform transform) =>
            new AuraPose(transform.position.ToAura(), transform.rotation.ToAura());

        public static AuraVector3[] ToAuraArray(this Vector3[] values)
        {
            if (values == null)
                return null;

            var result = new AuraVector3[values.Length];
            for (var index = 0; index < values.Length; index++)
                result[index] = values[index].ToAura();
            return result;
        }
    }
}
