using System;
using System.Collections.Generic;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraPhysicsView : MonoBehaviour
    {
        [SerializeField]
        private bool _syncTransform = true;

        private readonly List<IAuraPhysicsEventReceiver> _receivers = new List<IAuraPhysicsEventReceiver>();
        private AuraSimulationInstance _instance;
        private bool _loggedNaN;

        public SimulationEntityId EntityId { get; private set; } = SimulationEntityId.None;

        public bool SyncTransform
        {
            get => _syncTransform;
            set => _syncTransform = value;
        }

        internal void Bind(AuraSimulationInstance instance, SimulationEntityId entity)
        {
            _instance = instance;
            EntityId = entity;
        }

        internal void Unbind()
        {
            _instance = null;
            EntityId = SimulationEntityId.None;
        }

        internal void Dispatch(in AuraPhysicsEvent value)
        {
            for (var index = 0; index < _receivers.Count; index++)
            {
                var receiver = _receivers[index];
                switch (value.Type)
                {
                    case AuraPhysicsEventType.CollisionEnter:
                        receiver.OnCollisionEnter(value);
                        break;
                    case AuraPhysicsEventType.CollisionExit:
                        receiver.OnCollisionExit(value);
                        break;
                    case AuraPhysicsEventType.TriggerEnter:
                        receiver.OnTriggerEnter(value);
                        break;
                    case AuraPhysicsEventType.TriggerExit:
                        receiver.OnTriggerExit(value);
                        break;
                    default:
                        break;
                }
            }
        }

        private void Awake()
        {
            _receivers.Clear();
            var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (var index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IAuraPhysicsEventReceiver receiver && !ReferenceEquals(receiver, this))
                    _receivers.Add(receiver);
            }
        }

        private void Update()
        {
            if (!_syncTransform || _instance == null || EntityId.IsNone)
                return;

            if (_instance.TryGetBodyState(EntityId, out var state))
            {
                var p = state.Pose.Position;
                var q = state.Pose.Rotation;
                if (float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z) ||
                    float.IsNaN(q.X) || float.IsNaN(q.Y) || float.IsNaN(q.Z) || float.IsNaN(q.W))
                {
                    if (!_loggedNaN)
                    {
                        _loggedNaN = true;
                        Debug.LogError($"Aura NaN pose for {EntityId} name={name} tick={_instance.World.CurrentTick.Value} v={state.LinearVelocity}");
                    }

                    return;
                }

                AuraTransformSync.Apply(transform, state.Pose);
            }
        }
    }
}
