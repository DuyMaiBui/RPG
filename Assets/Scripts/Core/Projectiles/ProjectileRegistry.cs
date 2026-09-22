using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Projectiles
{
    public sealed class ProjectileRegistry
    {
        private readonly List<Projectile> _projectiles = new();
        private readonly List<int> _generations = new();
        private readonly List<bool> _active = new();
        private readonly Stack<int> _freeIndices = new();

        public int Count => _projectiles.Count;

        public Projectile Spawn(EntityId source, EntityId target, SimulationVector2 position, int damage, float speed, float radius, float lifetime)
        {
            var index = _freeIndices.Count > 0 ? _freeIndices.Pop() : _projectiles.Count;
            if (index == _projectiles.Count)
            {
                _projectiles.Add(new Projectile(
                    new EntityId(index, 0), source, target, position, damage, speed, radius, lifetime));
                _generations.Add(0);
                _active.Add(false);
            }

            var projectile = _projectiles[index];
            projectile.Reset(new EntityId(index, _generations[index]), source, target, position, damage, speed, radius, lifetime);
            _projectiles[index] = projectile;
            _active[index] = true;
            return projectile;
        }

        public bool TryGetAt(int index, out Projectile projectile)
        {
            if (index < 0 || index >= _projectiles.Count || !_active[index])
            {
                projectile = null;
                return false;
            }

            projectile = _projectiles[index];
            return true;
        }

        public void Destroy(EntityId id)
        {
            if (id.Index < 0 || id.Index >= _projectiles.Count || _generations[id.Index] != id.Generation)
                return;

            if (!_active[id.Index]) return;
            _active[id.Index] = false;
            _generations[id.Index]++;
            _freeIndices.Push(id.Index);
        }

        public ProjectileSnapshot[] CreateSnapshot()
        {
            var snapshots = new List<ProjectileSnapshot>(_projectiles.Count);
            foreach (var projectile in _projectiles)
            {
                if (!_active[projectile.Id.Index]) continue;
                snapshots.Add(new ProjectileSnapshot(projectile.Id, projectile.Source, projectile.Target, projectile.Position));
            }

            return snapshots.ToArray();
        }
    }
}
