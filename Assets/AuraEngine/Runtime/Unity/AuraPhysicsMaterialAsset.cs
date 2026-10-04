using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [CreateAssetMenu(menuName = "AuraEngine/Physics Material", fileName = "AuraPhysicsMaterial")]
    public sealed class AuraPhysicsMaterialAsset : ScriptableObject
    {
        [SerializeField]
        [Min(0f)]
        private float _friction = 0.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _restitution;

        [SerializeField]
        [Min(0.0001f)]
        private float _density = 1000f;

        public float Friction => _friction;

        public float Restitution => _restitution;

        public float Density => _density;

        public AuraPhysicsMaterialDefinition ToDefinition() =>
            new AuraPhysicsMaterialDefinition(_friction, _restitution, _density);
    }
}
