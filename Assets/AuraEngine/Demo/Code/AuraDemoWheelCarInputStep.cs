using System;
using UnityEngine;

namespace AuraEngine.Demo
{
    /* One segment of a scripted wheel car sequence (automated smoke tests). */
    [Serializable]
    public struct AuraDemoWheelCarInputStep
    {
        [Min(0f)]
        public float Duration;

        [Range(-1f, 1f)]
        public float Throttle;

        [Range(0f, 1f)]
        public float Brake;
    }
}
