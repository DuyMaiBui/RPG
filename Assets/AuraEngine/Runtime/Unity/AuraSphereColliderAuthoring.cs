using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public sealed class AuraSphereColliderAuthoring : AuraColliderAuthoring
    {
        [SerializeField]
        [Min(0.001f)]
        private float _radius = 0.5f;

        public override AuraShapeType ShapeType => AuraShapeType.Sphere;

        protected override AuraShapeGeometry Geometry => AuraShapeGeometry.Sphere(_radius);
    }
}
