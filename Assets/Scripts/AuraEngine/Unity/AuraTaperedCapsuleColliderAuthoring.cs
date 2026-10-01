using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public sealed class AuraTaperedCapsuleColliderAuthoring : AuraColliderAuthoring
    {
        [SerializeField]
        [Min(0.001f)]
        private float _radius = 0.5f;

        [SerializeField]
        [Min(0f)]
        private float _topRadius = 0.25f;

        [SerializeField]
        [Min(0.001f)]
        private float _height = 2f;

        public override AuraShapeType ShapeType => AuraShapeType.TaperedCapsule;

        protected override AuraShapeGeometry Geometry =>
            AuraShapeGeometry.TaperedCapsule(_radius, _topRadius, _height);
    }
}
