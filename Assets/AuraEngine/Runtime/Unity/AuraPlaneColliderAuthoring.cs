using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public sealed class AuraPlaneColliderAuthoring : AuraColliderAuthoring
    {
        [SerializeField]
        private Vector3 _normal = Vector3.up;

        public override AuraShapeType ShapeType => AuraShapeType.Plane;

        protected override AuraShapeGeometry Geometry => AuraShapeGeometry.Plane(_normal.normalized.ToAura());
    }
}
