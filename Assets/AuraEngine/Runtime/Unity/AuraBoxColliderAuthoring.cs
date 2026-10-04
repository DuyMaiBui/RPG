using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public sealed class AuraBoxColliderAuthoring : AuraColliderAuthoring
    {
        [SerializeField]
        private Vector3 _size = Vector3.one;

        public override AuraShapeType ShapeType => AuraShapeType.Box;

        protected override AuraShapeGeometry Geometry =>
            AuraShapeGeometry.Box(
                Mathf.Max(0.001f, _size.x) * 0.5f,
                Mathf.Max(0.001f, _size.y) * 0.5f,
                Mathf.Max(0.001f, _size.z) * 0.5f);
    }
}
