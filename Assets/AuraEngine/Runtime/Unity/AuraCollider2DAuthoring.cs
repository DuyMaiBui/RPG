using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public abstract class AuraCollider2DAuthoring : MonoBehaviour
    {
        [SerializeField]
        private Vector2 _offset;

        [SerializeField]
        private float _rotationDegrees;

        [SerializeField]
        private bool _isTrigger;

        [SerializeField]
        [Tooltip("One-way platform: solid only for bodies and characters approaching from this collider's local +Y side (rotate the collider to tilt it). Bodies and characters pass up through it from below.")]
        private bool _oneWay;

        [SerializeField]
        private AuraPhysicsMaterialAsset _material;

        [SerializeField]
        [AuraLayer]
        private int _layer;

        public abstract AuraShapeType ShapeType { get; }

        protected abstract AuraShapeGeometry Geometry { get; }

        public AuraPhysicsShapeDefinition BuildShape()
        {
            var layers = GetComponentInParent<AuraSimulationInstance>()?.Layers;
            var layerIndex = layers != null && layers.HasLayer(_layer) ? _layer : 0;
            var halfAngle = _rotationDegrees * Mathf.Deg2Rad * 0.5f;
            var localPose = new AuraPose(
                new AuraVector3(_offset.x, _offset.y, 0f),
                new AuraQuaternion(0f, 0f, Mathf.Sin(halfAngle), Mathf.Cos(halfAngle)));

            return new AuraPhysicsShapeDefinition(
                ShapeType,
                localPose,
                _isTrigger,
                _material != null ? _material.ToDefinition() : AuraPhysicsMaterialDefinition.Default,
                new AuraPhysicsLayer(Mathf.Clamp(layerIndex, 0, AuraPhysicsLayer.MaxLayers - 1)),
                Geometry,
                _oneWay);
        }
    }
}
