using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AuraEngine.Demo
{
    /* Drags a Box2D body with a mouse joint. While the left button is held the body under the pointer is grabbed
       (a joint is created against the static anchor body) and the joint target follows the pointer; release destroys
       the joint. Scripted mode grabs Script Body and moves the target along Script Path for smoke runs. */
    public sealed class AuraDemoMouseDragDriver2D : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [SerializeField]
        private Camera _camera;

        [SerializeField]
        [Tooltip("Static body used as the mouse joint's fixed side (for example the ground).")]
        private AuraPhysicsBody2DAuthoring _anchorBody;

        [SerializeField]
        [Tooltip("Bodies that may be grabbed. Empty = any body except the anchor body.")]
        private AuraPhysicsBody2DAuthoring[] _grabbable = new AuraPhysicsBody2DAuthoring[0];

        [SerializeField]
        [Min(0f)]
        private float _frequency = 5f;

        [SerializeField]
        [Min(0f)]
        private float _damping = 0.7f;

        [SerializeField]
        [Min(0f)]
        private float _maxForce = 1000f;

        [SerializeField]
        private bool _useScript;

        [SerializeField]
        private AuraPhysicsBody2DAuthoring _scriptBody;

        [SerializeField]
        [Tooltip("World-space waypoints the target visits after starting at the body's position.")]
        private Vector2[] _scriptPath = new Vector2[0];

        [SerializeField]
        [Min(0.01f)]
        private float _secondsPerSegment = 1f;

        [SerializeField]
        private bool _loopScript = true;

        private AuraJointId _joint = AuraJointId.Invalid;
        private AuraScriptPlayer _player;
        private Vector2 _scriptOrigin;

        public bool IsDragging => _joint.IsValid;

        private void OnDisable() => Release();

        private void Update()
        {
            if (_instance == null || !_instance.IsCreated || _anchorBody == null || _anchorBody.EntityId.IsNone)
                return;

            if (_useScript)
                UpdateScript();
            else
                UpdatePointer();
        }

        private void UpdatePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                Release();
                return;
            }

            if (!TryGetPointerWorld(mouse.position.ReadValue(), out var point))
                return;

            if (!_joint.IsValid && mouse.leftButton.wasPressedThisFrame)
            {
                var entity = FindBodyAt(point);
                if (!entity.IsNone)
                    Grab(entity, point);
            }
            else if (_joint.IsValid)
            {
                MoveTarget(point);
            }
        }

        private void UpdateScript()
        {
            if (_scriptBody == null || _scriptBody.EntityId.IsNone || _scriptPath == null || _scriptPath.Length == 0)
                return;

            if (!_joint.IsValid)
            {
                if (_player != null && _player.IsFinished)
                    return;

                _scriptOrigin = _scriptBody.transform.position;
                var durations = new float[_scriptPath.Length];
                for (var index = 0; index < durations.Length; index++)
                    durations[index] = _secondsPerSegment;
                _player = new AuraScriptPlayer(durations, _loopScript);
                Grab(_scriptBody.EntityId, _scriptOrigin);
                return;
            }

            var segment = _player.Advance(Time.deltaTime);
            if (segment < 0)
            {
                Release();
                return;
            }

            var from = segment == 0 ? _scriptOrigin : _scriptPath[segment - 1];
            MoveTarget(Vector2.Lerp(from, _scriptPath[segment], _player.Progress));
        }

        private SimulationEntityId FindBodyAt(Vector2 point)
        {
            var hits = new AuraPhysicsQueryHit[8];
            var count = _instance.World.Queries.OverlapPoint(new AuraVector3(point.x, point.y, 0f), AuraPhysicsQueryFilter.All, hits);
            for (var index = 0; index < count; index++)
            {
                var entity = hits[index].Entity;
                if (entity == _anchorBody.EntityId)
                    continue;
                if (IsGrabbable(entity))
                    return entity;
            }

            return SimulationEntityId.None;
        }

        private bool IsGrabbable(SimulationEntityId entity)
        {
            if (_grabbable == null || _grabbable.Length == 0)
                return true;

            for (var index = 0; index < _grabbable.Length; index++)
            {
                if (_grabbable[index] != null && _grabbable[index].EntityId == entity)
                    return true;
            }

            return false;
        }

        private void Grab(SimulationEntityId entity, Vector2 point)
        {
            var definition = AuraJointDefinition.CreateMouse(
                PhysicsBodyId.Invalid,
                PhysicsBodyId.Invalid,
                new AuraVector3(point.x, point.y, 0f),
                _frequency,
                _damping,
                _maxForce);
            _joint = _instance.AttachJoint(_anchorBody.EntityId, entity, definition);
            if (!_joint.IsValid)
                Debug.LogError($"{nameof(AuraDemoMouseDragDriver2D)} failed to create the mouse joint.", this);
        }

        private void MoveTarget(Vector2 point)
        {
            var target = _instance.World.Physics as IPhysicsJointTarget;
            if (target == null)
                return;

            var result = target.SetJointTarget(_joint, new AuraVector3(point.x, point.y, 0f));
            if (result == AuraResult.InvalidHandle)
                _joint = AuraJointId.Invalid;
        }

        private void Release()
        {
            if (_joint.IsValid && _instance != null && _instance.IsCreated)
                _instance.DetachJoint(_joint);
            _joint = AuraJointId.Invalid;
        }

        private bool TryGetPointerWorld(Vector2 screen, out Vector2 point)
        {
            point = default;
            var camera = _camera != null ? _camera : Camera.main;
            if (camera == null)
                return false;

            var ray = camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.z) < 1e-6f)
                return false;

            var t = -ray.origin.z / ray.direction.z;
            var hit = ray.origin + ray.direction * t;
            point = new Vector2(hit.x, hit.y);
            return true;
        }
    }
}
