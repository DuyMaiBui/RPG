using System;
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
        [SerializeField, Min(0f)] private float _attackRange = 0.25f;
        [SerializeField, Min(0f)] private float _projectileSpeed = 5f;
        [SerializeField, Min(0f)] private float _projectileRadius = 0.05f;
        [SerializeField, Min(0f)] private float _projectileLifetime = 5f;

        public ActorLoadoutAuthoring()
        {
        }

        public ActorLoadoutAuthoring(AttackType attackType, float attackRange, float projectileSpeed,
            float projectileRadius, float projectileLifetime)
        {
            _attackType = attackType;
            _attackRange = attackRange;
            _projectileSpeed = projectileSpeed;
            _projectileRadius = projectileRadius;
            _projectileLifetime = projectileLifetime;
        }

        public ActorSpawnData CreateSpawnData(
            int maximumHealth,
            int attackPower,
            SimulationVector2 position,
            float bodyRadius,
            float moveSpeed,
            float visionRange,
            float attackCooldown,
            ColliderShapeData[] colliderShapes = null)
        {
            if (_attackRange < 0f)
                throw new InvalidOperationException("Actor loadout attack range cannot be negative.");
            if (_attackType == AttackType.Projectile && (_projectileSpeed <= 0f || _projectileLifetime <= 0f))
                throw new InvalidOperationException("Projectile loadout requires positive speed and lifetime.");

            return new ActorSpawnData(
                maximumHealth,
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
                colliderShapes: colliderShapes);
        }
    }
}
