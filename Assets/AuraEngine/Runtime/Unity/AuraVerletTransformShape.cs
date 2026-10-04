using System;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /// <summary>
    /// Authored collider description for a plain Transform (no Aura collider component needed).
    /// Values are in the transform's local space and scaled by its lossy scale at runtime.
    /// </summary>
    [Serializable]
    public struct AuraVerletTransformShape
    {
        public Transform Target;
        public AuraVerletColliderKind Kind;
        [Tooltip("Local-space centre (plane: a point on the plane).")]
        public Vector3 Center;
        [Tooltip("Sphere and capsule radius.")]
        public float Radius;
        [Tooltip("Capsule total height along local Y, caps included.")]
        public float Height;
        [Tooltip("Box full size in local space.")]
        public Vector3 Size;
        [Tooltip("Plane normal in local space (points to the free side).")]
        public Vector3 Normal;
        [Range(0f, 1f)]
        public float Friction;
    }
}
