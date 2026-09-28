using System;
using System.Collections.Generic;
using RPG.Core.Navigation;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ColliderCompoundAuthoring : MonoBehaviour
    {
        [SerializeField] private ColliderAuthoring[] _colliders = new ColliderAuthoring[0];

        public ColliderCompound CreateCompound()
        {
            return new ColliderCompound(CreateShapeData());
        }

        public ColliderShapeData[] CreateCompoundShapes() => CreateShapeData();

        public NavigationObstacle[] CreateNavigationObstacles(int idStart)
        {
            var shapes = CreateShapeData();
            var obstacles = new List<NavigationObstacle>(shapes.Length);
            for (var index = 0; index < shapes.Length; index++)
            {
                if (shapes[index].Mode != ColliderMode.Solid) continue;
                var offset = Rotate(shapes[index].LocalOffset, transform.eulerAngles.z * Mathf.Deg2Rad);
                obstacles.Add(new NavigationObstacle(
                    idStart + index,
                    new SimulationVector2(transform.position.x + offset.X, transform.position.y + offset.Y),
                    shapes[index].Shape.WithRotation(transform.eulerAngles.z * Mathf.Deg2Rad + shapes[index].LocalRotationRadians)));
            }

            return obstacles.ToArray();
        }

        private ColliderShapeData[] CreateShapeData()
        {
            if (_colliders == null || _colliders.Length == 0)
                throw new InvalidOperationException($"{name} requires at least one collider authoring component.");

            var shapes = new ColliderShapeData[_colliders.Length];
            for (var index = 0; index < _colliders.Length; index++)
            {
                if (_colliders[index] == null)
                    throw new InvalidOperationException($"{name} contains a missing collider authoring reference at index {index}.");
                shapes[index] = _colliders[index].ToSimulationData();
            }

            return shapes;
        }

        private static SimulationVector2 Rotate(SimulationVector2 value, float radians)
        {
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            return new SimulationVector2(
                value.X * cosine - value.Y * sine,
                value.X * sine + value.Y * cosine);
        }
    }
}
