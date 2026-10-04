using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraRagdollView : MonoBehaviour
    {
        [SerializeField]
        private AuraRagdollAuthoring _ragdoll;

        [SerializeField]
        private Transform[] _limbs = new Transform[0];

        private AuraPose[] _scratch = new AuraPose[0];

        private void Update()
        {
            if (_ragdoll == null || _limbs == null || _limbs.Length == 0)
                return;

            var id = _ragdoll.RagdollId;
            if (!id.IsValid)
                return;

            var instance = GetComponentInParent<AuraSimulationInstance>();
            if (instance == null || !instance.IsCreated)
                return;

            if (_scratch.Length != _limbs.Length)
                _scratch = new AuraPose[_limbs.Length];

            if (!instance.TryGetRagdollPose(id, _scratch))
                return;

            for (var index = 0; index < _limbs.Length; index++)
            {
                if (_limbs[index] == null)
                    continue;

                _limbs[index].position = new Vector3(_scratch[index].Position.X, _scratch[index].Position.Y, _scratch[index].Position.Z);
                _limbs[index].rotation = new Quaternion(_scratch[index].Rotation.X, _scratch[index].Rotation.Y, _scratch[index].Rotation.Z, _scratch[index].Rotation.W);
            }
        }
    }
}
