using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [RequireComponent(typeof(AuraSimulationInstance))]
    public sealed class AuraPhysicsDebugDraw : MonoBehaviour
    {
        [SerializeField]
        private bool _drawBodies = true;

        [SerializeField]
        private Color _bodyColor = new Color(0.2f, 1f, 0.4f, 0.8f);

        private AuraBodyState[] _buffer = new AuraBodyState[256];

        private void OnDrawGizmos()
        {
            if (!_drawBodies || !Application.isPlaying)
                return;

            var instance = GetComponent<AuraSimulationInstance>();
            if (instance == null || !instance.IsCreated)
                return;

            var world = instance.World;
            if (_buffer.Length < world.BodyCount)
                _buffer = new AuraBodyState[Mathf.NextPowerOfTwo(world.BodyCount)];

            var count = world.CopyBodyStates(_buffer);
            Gizmos.color = _bodyColor;
            for (var index = 0; index < count; index++)
            {
                var position = _buffer[index].Pose.Position;
                var point = new Vector3(position.X, position.Y, position.Z);
                Gizmos.DrawWireSphere(point, 0.1f);
                Gizmos.DrawLine(point, point + Vector3.up * 0.3f);
            }
        }
    }
}
