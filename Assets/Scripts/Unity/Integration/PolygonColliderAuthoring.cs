using RPG.Core.Physics;
using RPG.Simulation.Contracts;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class PolygonColliderAuthoring : ColliderAuthoring
    {
        [SerializeField] private Vector2[] _vertices =
        {
            new Vector2(-0.5f, -0.5f),
            new Vector2(0.5f, -0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-0.5f, 0.5f),
        };

        protected override CollisionShape CreateShape()
        {
            var vertices = new SimulationVector2[_vertices == null ? 0 : _vertices.Length];
            for (var index = 0; index < vertices.Length; index++)
                vertices[index] = new SimulationVector2(_vertices[index].x, _vertices[index].y);
            return CollisionShape.Polygon(vertices);
        }

        private void OnDrawGizmosSelected()
        {
            if (_vertices == null || _vertices.Length < 2) return;
            Gizmos.color = GizmoColor;
            var previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(WorldCenter, WorldRotation, Vector3.one);
            for (var index = 0; index < _vertices.Length; index++)
            {
                var current = _vertices[index];
                var next = _vertices[(index + 1) % _vertices.Length];
                Gizmos.DrawLine(current, next);
            }
            Gizmos.matrix = previous;
        }
    }
}
