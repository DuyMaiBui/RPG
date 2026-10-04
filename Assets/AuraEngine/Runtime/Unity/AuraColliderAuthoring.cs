using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public abstract class AuraColliderAuthoring : MonoBehaviour
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Collider")]
#endif
        [SerializeField]
        protected Vector3 _center;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Collider")]
#endif
        [SerializeField]
        protected bool _isTrigger;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Collider")]
#endif
        [SerializeField]
        protected AuraPhysicsMaterialAsset _material;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Collider")]
        [Sirenix.OdinInspector.ValueDropdown("LayerNames")]
#endif
        [AuraLayer]
        [SerializeField]
        protected int _layer;

        private AuraSimulationInstance _instance;

        public abstract AuraShapeType ShapeType { get; }

        protected abstract AuraShapeGeometry Geometry { get; }

        public AuraPhysicsLayers Layers
        {
            get
            {
                if (_instance == null)
                    _instance = GetComponentInParent<AuraSimulationInstance>();
                return _instance != null ? _instance.Layers : null;
            }
        }

#if ODIN_INSPECTOR
        private System.Collections.Generic.IEnumerable<Sirenix.OdinInspector.ValueDropdownItem<int>> LayerNames()
        {
            var layers = Layers;
            if (layers != null)
            {
                var indices = layers.GetNamedIndices();
                for (var i = 0; i < indices.Length; i++)
                {
                    var index = indices[i];
                    yield return new Sirenix.OdinInspector.ValueDropdownItem<int>($"{index}: {layers.GetName(index)}", index);
                }

                yield break;
            }

            yield return new Sirenix.OdinInspector.ValueDropdownItem<int>($"0: {AuraPhysicsLayers.DefaultLayerName}", 0);
        }
#endif

        private AuraPhysicsLayer ResolvedLayer()
        {
            var layers = Layers;
            var layerIndex = _layer;
            if (layers != null && !layers.HasLayer(layerIndex))
                layerIndex = 0;

            return new AuraPhysicsLayer(Mathf.Clamp(layerIndex, 0, AuraPhysicsLayer.MaxLayers - 1));
        }

        protected AuraPose LocalPose =>
            new AuraPose(new AuraVector3(_center.x, _center.y, _center.z), AuraQuaternion.Identity);

        public AuraPhysicsShapeDefinition BuildShape() =>
            new AuraPhysicsShapeDefinition(
                ShapeType,
                LocalPose,
                _isTrigger,
                _material != null ? _material.ToDefinition() : AuraPhysicsMaterialDefinition.Default,
                ResolvedLayer(),
                Geometry);

    }
}
