using RPG.Core.Physics;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class CircleColliderAuthoring : ColliderAuthoring
    {
        [SerializeField, Min(0f)] private float _radius = 0.5f;

        protected override CollisionShape CreateShape() => CollisionShape.Circle(Mathf.Max(0f, _radius));

        private void OnValidate() => _radius = Mathf.Max(0f, _radius);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GizmoColor;
            var previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(WorldCenter, WorldRotation, Vector3.one);
            Gizmos.DrawWireSphere(Vector3.zero, _radius);
            Gizmos.matrix = previous;
        }
    }
}
