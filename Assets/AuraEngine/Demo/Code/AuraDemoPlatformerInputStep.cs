using System;
using UnityEngine;

namespace AuraEngine.Demo
{
    /* One segment of a scripted platformer input sequence (automated smoke tests). */
    [Serializable]
    public struct AuraDemoPlatformerInputStep
    {
        [Min(0f)]
        public float Duration;

        [Range(-1f, 1f)]
        public float Move;

        [Tooltip("Press jump at the start of this segment.")]
        public bool Jump;
    }
}
