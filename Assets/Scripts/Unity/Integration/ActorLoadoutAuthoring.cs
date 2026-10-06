using System;
using RPG.Content;
using RPG.Core.Actors;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;
using UnityEngine;

namespace RPG.Unity
{
    [Serializable]
    public sealed class ActorLoadoutAuthoring
    {
        [SerializeField] private AttackType _attackType;
        [SerializeField] private ActorArchetype _archetype;
        [SerializeField, Min(0f)] private float _attackRange = 0.25f;
        [SerializeField, Min(0f)] private float _projectileSpeed = 5f;
        [SerializeField, Min(0f)] private float _projectileRadius = 0.05f;
        [SerializeField, Min(0f)] private float _projectileLifetime = 5f;

        [SerializeField, Min(0.1f)] private float _healthMultiplier = 1f;

        public ActorLoadoutAuthoring()
        {
        }

        public ActorLoadoutAuthoring(AttackType attackType, float attackRange, float projectileSpeed,
            float projectileRadius, float projectileLifetime, ActorArchetype archetype = ActorArchetype.None,
            float healthMultiplier = 1f)
        {
            _attackType = attackType;
            _archetype = archetype;
            _attackRange = attackRange;
            _projectileSpeed = projectileSpeed;
            _projectileRadius = projectileRadius;
            _projectileLifetime = projectileLifetime;
            _healthMultiplier = healthMultiplier;
        }

        public ActorSpawnData CreateSpawnData(
            ContentCatalog catalog,
            int maximumHealth,
            int attackPower,
            SimulationVector2 position,
            float bodyRadius,
            float moveSpeed,
            float visionRange,
            float attackCooldown,
            ColliderShapeData[] colliderShapes = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (_attackRange < 0f)
                throw new InvalidOperationException("Actor loadout attack range cannot be negative.");
            if (_attackType == AttackType.Projectile && (_projectileSpeed <= 0f || _projectileLifetime <= 0f))
                throw new InvalidOperationException("Projectile loadout requires positive speed and lifetime.");

            return new ActorSpawnData(
                ResolveMaximumHealth(maximumHealth),
                attackPower,
                position,
                bodyRadius,
                moveSpeed,
                visionRange,
                _attackRange,
                attackCooldown,
                attackType: _attackType,
                projectileSpeed: _projectileSpeed,
                projectileRadius: _projectileRadius,
                projectileLifetime: _projectileLifetime,
                colliderShapes: colliderShapes,
                abilities: catalog.AbilitiesFor(ResolveArchetype()));
        }

        /// <summary>Health this loadout fields, scaled from the session default. A melee rank has to cross the range at
        /// which ranged abilities already hit it, so it needs more health than the rank that shoots from safety.</summary>
        private int ResolveMaximumHealth(int maximumHealth)
        {
            if (_healthMultiplier <= 0f)
                throw new InvalidOperationException("Actor loadout health multiplier must be positive.");
            return Math.Max(1, (int)Math.Round(maximumHealth * (double)_healthMultiplier));
        }

        private ActorArchetype ResolveArchetype() => _archetype != ActorArchetype.None
            ? _archetype
            : _attackType == AttackType.Projectile
                ? ActorArchetype.Skirmisher
                : ActorArchetype.Bruiser;
    }
}
