using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    public sealed class AuraDemoRayProbe : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [SerializeField]
        private Camera _camera;

        [SerializeField]
        private float _maxDistance = 200f;

        public bool HasHit { get; private set; }

        public SimulationEntityId HitEntity { get; private set; } = SimulationEntityId.None;

        public float HitDistance { get; private set; }

        private void Update()
        {
            HasHit = false;
            HitEntity = SimulationEntityId.None;
            HitDistance = 0f;

            if (_instance == null || !_instance.IsCreated)
                return;

            var source = _camera != null ? _camera : Camera.main;
            if (source == null)
                return;

            var origin = source.transform.position;
            var direction = source.transform.forward;
            var ray = new AuraRay(
                new AuraVector3(origin.x, origin.y, origin.z),
                new AuraVector3(direction.x, direction.y, direction.z));

            if (!_instance.World.Raycast(ray, _maxDistance, AuraPhysicsQueryFilter.All, out var hit))
                return;

            HasHit = true;
            HitEntity = hit.Entity;
            HitDistance = hit.Distance;

            Debug.DrawLine(origin, new Vector3(hit.Point.X, hit.Point.Y, hit.Point.Z), Color.green);
        }
    }
}
