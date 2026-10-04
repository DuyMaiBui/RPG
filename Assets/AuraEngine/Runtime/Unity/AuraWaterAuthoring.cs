using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraWaterAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Min(0.1f)]
        private float _surfaceHeight = 1f;

        [SerializeField]
        [Min(0.0001f)]
        private float _density = 1000f;

        [SerializeField]
        [Min(0f)]
        private float _linearDrag;

        private AuraSimulationInstance _instance;
        private AuraWaterId _water = AuraWaterId.Invalid;

        public AuraWaterId WaterId => _water;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraWaterAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);

            _instance = null;
            _water = AuraWaterId.Invalid;
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            var position = transform.position;
            var definition = new AuraWaterDefinition(
                position.y + _surfaceHeight,
                new AuraVector3(0f, 1f, 0f),
                _density,
                _linearDrag);

            _water = instance.AttachWater(definition);
            if (!_water.IsValid)
                Debug.LogError($"{nameof(AuraWaterAuthoring)} on '{name}' failed to create water.", this);
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (!_water.IsValid)
                return;

            instance.DetachWater(_water);
            _water = AuraWaterId.Invalid;
        }
    }
}
