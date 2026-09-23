using RPG.Core.Navigation;
using RPG.Simulation.Contracts;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class NavigationObstacleAuthoring : MonoBehaviour
    {
        [SerializeField] private int _id;
        [SerializeField] private Vector2 _halfExtents = Vector2.one;
        [SerializeField] private bool _isEnabled = true;

        public NavigationObstacle ToSimulationObstacle()
        {
            return new NavigationObstacle(
                _id,
                new SimulationVector2(transform.position.x, transform.position.y),
                new SimulationVector2(Mathf.Max(0f, _halfExtents.x), Mathf.Max(0f, _halfExtents.y)));
        }

        private void OnValidate()
        {
            _halfExtents.x = Mathf.Max(0f, _halfExtents.x);
            _halfExtents.y = Mathf.Max(0f, _halfExtents.y);
        }

        public bool IsEnabled => _isEnabled;
    }
}
