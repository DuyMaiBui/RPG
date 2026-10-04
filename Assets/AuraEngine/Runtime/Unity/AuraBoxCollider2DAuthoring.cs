using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public sealed class AuraBoxCollider2DAuthoring : AuraCollider2DAuthoring
    {
        [SerializeField]
        private Vector2 _size = Vector2.one;

        public override AuraShapeType ShapeType => AuraShapeType.Box;

        protected override AuraShapeGeometry Geometry => AuraShapeGeometry.Box(
            Mathf.Max(0.001f, _size.x) * 0.5f,
            Mathf.Max(0.001f, _size.y) * 0.5f,
            0.5f);
    }
}
