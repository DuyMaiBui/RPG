using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public sealed class AuraCapsuleColliderAuthoring : AuraColliderAuthoring
    {
        [SerializeField]
        [Min(0.001f)]
        private float _radius = 0.5f;

        [SerializeField]
        [Min(0.001f)]
        private float _height = 2f;

        public override AuraShapeType ShapeType => AuraShapeType.Capsule;

        protected override AuraShapeGeometry Geometry => AuraShapeGeometry.Capsule(_radius, Mathf.Max(_height, _radius * 2f));
    }
}
