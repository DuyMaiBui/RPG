using RPG.Core.Physics;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class BoxColliderAuthoring : ColliderAuthoring
    {
        [SerializeField] private Vector2 _halfExtents = Vector2.one * 0.5f;

        protected override CollisionShape CreateShape() => CollisionShape.Box(
            new RPG.Simulation.Contracts.SimulationVector2(
                Mathf.Max(0f, _halfExtents.x),
                Mathf.Max(0f, _halfExtents.y)));

        private void OnValidate()
        {
            _halfExtents.x = Mathf.Max(0f, _halfExtents.x);
            _halfExtents.y = Mathf.Max(0f, _halfExtents.y);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GizmoColor;
            var previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(WorldCenter, WorldRotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_halfExtents.x * 2f, _halfExtents.y * 2f, 0f));
            Gizmos.matrix = previous;
        }
    }
}
