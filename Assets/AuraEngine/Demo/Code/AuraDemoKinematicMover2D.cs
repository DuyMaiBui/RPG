using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    public sealed class AuraDemoKinematicMover2D : MonoBehaviour
    {
        [SerializeField] private AuraSimulationInstance _instance;
        [SerializeField] private AuraPhysicsBody2DAuthoring _body;
        [SerializeField] private Vector2 _amplitude = new Vector2(3f, 0f);
        [SerializeField] private float _speed = 1.5f;

        private Vector2 _origin;
        private float _time;

        private void Start() => _origin = transform.position;

        private void Update()
        {
            if (_instance == null || !_instance.IsCreated || _body == null || _body.EntityId.IsNone)
                return;

            _time += Time.deltaTime * _speed;
            var position = _origin + _amplitude * Mathf.Sin(_time);
            _instance.World.SetKinematicTarget(
                _body.EntityId,
                new AuraPose(new AuraVector3(position.x, position.y, 0f),
                    new AuraQuaternion(0f, 0f, transform.rotation.z, transform.rotation.w)));
        }
    }
}
