using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    public sealed class AuraDemoKinematicMover : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [SerializeField]
        private AuraPhysicsBodyAuthoring _body;

        [SerializeField]
        private Vector3 _amplitude = new Vector3(3f, 0f, 0f);

        [SerializeField]
        private float _speed = 1.5f;

        private Vector3 _origin;
        private float _time;

        private void Start()
        {
            _origin = transform.position;
        }

        private void Update()
        {
            if (_instance == null || !_instance.IsCreated || _body == null)
                return;

            var entity = _body.EntityId;
            if (entity.IsNone)
                return;

            _time += Time.deltaTime * _speed;
            var offset = Mathf.Sin(_time);
            var position = _origin + _amplitude * offset;
            _instance.World.SetKinematicTarget(
                entity,
                new AuraPose(
                    new AuraVector3(position.x, position.y, position.z),
                    new AuraQuaternion(transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w)));
        }
    }
}
