using RPG.Core.Physics;
using RPG.Simulation.Contracts;
using UnityEngine;

namespace RPG.Unity
{
    public abstract class ColliderAuthoring : MonoBehaviour
    {
        [SerializeField] private Vector2 _localOffset;
        [SerializeField] private float _localRotationDegrees;
        [SerializeField] private ColliderMode _mode = ColliderMode.Solid;
        [SerializeField, Range(0, 31)] private int _layer;
        [SerializeField] private int _mask = -1;

        public ColliderShapeData ToSimulationData()
        {
            return new ColliderShapeData(
                CreateShape(),
                new SimulationVector2(_localOffset.x, _localOffset.y),
                _localRotationDegrees * Mathf.Deg2Rad,
                _mode,
                new ColliderFilter(_layer, _mask));
        }

        protected abstract CollisionShape CreateShape();

        protected Color GizmoColor => _mode == ColliderMode.Solid
            ? new Color(1f, 0.25f, 0.1f, 0.9f)
            : _mode == ColliderMode.Trigger
                ? new Color(1f, 0.8f, 0.1f, 0.9f)
                : new Color(0.1f, 0.8f, 1f, 0.9f);

        protected Vector3 WorldCenter => transform.TransformPoint(new Vector3(_localOffset.x, _localOffset.y, 0f));
        protected Quaternion WorldRotation => transform.rotation * Quaternion.Euler(0f, 0f, _localRotationDegrees);
    }
}
