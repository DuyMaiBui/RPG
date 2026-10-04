using System;
using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEngine;
using UnityEngine.Events;

namespace AuraEngine.Demo
{
    /* Polls the watched joints and reports the moment one snaps (break threshold exceeded): logs it, raises the
       UnityEvents and the C# event once per joint. Optionally logs the peak reaction force of the watched joints. */
    public sealed class AuraDemoJointBreakMonitor2D : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [SerializeField]
        private AuraJoint2DAuthoring[] _joints = new AuraJoint2DAuthoring[0];

        [SerializeField]
        private bool _logWhenBroken = true;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds between peak-force log lines; 0 disables them.")]
        private float _feedbackLogInterval;

        [SerializeField]
        private UnityEvent _onAnyBroken = new UnityEvent();

        private bool[] _reported;
        private float _peakForce;
        private float _logTimer;

        /* Raised once per snapped joint. */
        public event Action<AuraJoint2DAuthoring> JointBroken;

        public int BrokenCount { get; private set; }

        private void OnEnable()
        {
            _reported = new bool[_joints == null ? 0 : _joints.Length];
            BrokenCount = 0;
            _peakForce = 0f;
            _logTimer = _feedbackLogInterval;
        }

        private void Update()
        {
            if (_instance == null || !_instance.IsCreated || _joints == null || _reported == null || _reported.Length != _joints.Length)
                return;

            var control = _instance.World.JointControl;
            if (control == null)
                return;

            for (var index = 0; index < _joints.Length; index++)
            {
                var joint = _joints[index];
                if (_reported[index] || joint == null || !joint.JointId.IsValid)
                    continue;

                if (control.GetFeedback(joint.JointId, out var feedback) != AuraResult.Success)
                    continue;

                if (feedback.Force > _peakForce)
                    _peakForce = feedback.Force;

                if (!feedback.IsBroken)
                    continue;

                _reported[index] = true;
                BrokenCount++;
                if (_logWhenBroken)
                    Debug.Log($"{nameof(AuraDemoJointBreakMonitor2D)}: joint '{joint.name}' broke (last force {feedback.Force:F1} N, torque {feedback.Torque:F1} N*m).", joint);
                JointBroken?.Invoke(joint);
                _onAnyBroken.Invoke();
            }

            if (_feedbackLogInterval > 0f)
            {
                _logTimer -= Time.deltaTime;
                if (_logTimer <= 0f)
                {
                    _logTimer += _feedbackLogInterval;
                    Debug.Log($"{nameof(AuraDemoJointBreakMonitor2D)}: peak joint force {_peakForce:F1} N, broken {BrokenCount}/{_joints.Length}.", this);
                    _peakForce = 0f;
                }
            }
        }
    }
}
