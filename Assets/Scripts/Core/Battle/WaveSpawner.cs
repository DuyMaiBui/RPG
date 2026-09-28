using System;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class WaveSpawner
    {
        private ActorSpawnData _redMelee;
        private ActorSpawnData _redRanged;
        private ActorSpawnData _blueMelee;
        private ActorSpawnData _blueRanged;
        private SimulationVector2 _redSpawnOrigin;
        private SimulationVector2 _blueSpawnOrigin;
        private int _meleeCount;
        private int _rangedCount;
        private int _unitIndex;
        private float _unitInterval;
        private float _waveInterval;
        private float _elapsed;
        private float _nextSpawnTime;
        private float _nextWaveTime;
        private bool _configured;

        public void Configure(
            int meleeCount,
            int rangedCount,
            float unitInterval,
            float waveInterval,
            float initialDelay,
            ActorSpawnData redMelee,
            ActorSpawnData redRanged,
            ActorSpawnData blueMelee,
            ActorSpawnData blueRanged,
            SimulationVector2 redSpawnOrigin,
            SimulationVector2 blueSpawnOrigin)
        {
            if (meleeCount < 0 || rangedCount < 0 || meleeCount + rangedCount == 0)
                throw new ArgumentOutOfRangeException(nameof(meleeCount));
            if (unitInterval < 0f || waveInterval < 0f || initialDelay < 0f)
                throw new ArgumentOutOfRangeException(nameof(unitInterval));

            _meleeCount = meleeCount;
            _rangedCount = rangedCount;
            _unitInterval = unitInterval;
            _waveInterval = waveInterval;
            _nextSpawnTime = initialDelay;
            _nextWaveTime = initialDelay + waveInterval;
            _redMelee = redMelee;
            _redRanged = redRanged;
            _blueMelee = blueMelee;
            _blueRanged = blueRanged;
            _redSpawnOrigin = redSpawnOrigin;
            _blueSpawnOrigin = blueSpawnOrigin;
            _elapsed = 0f;
            _unitIndex = 0;
            _configured = true;
        }

        public void Tick(RpgSimulationState state, float deltaTime)
        {
            if (!_configured || deltaTime <= 0f)
                return;

            _elapsed += deltaTime;
            var totalUnits = _meleeCount + _rangedCount;
            if (_unitIndex >= totalUnits)
            {
                if (_elapsed < _nextWaveTime)
                    return;
                _unitIndex = 0;
                _nextSpawnTime = _elapsed;
                _nextWaveTime += _waveInterval;
            }

            if (_elapsed < _nextSpawnTime)
                return;

            var redData = _unitIndex < _meleeCount ? _redMelee : _redRanged;
            var blueData = _unitIndex < _meleeCount ? _blueMelee : _blueRanged;
            if (!TryFindSpawnPosition(state, _redSpawnOrigin, redData.Radius, out var redPosition) ||
                !TryFindSpawnPosition(state, _blueSpawnOrigin, blueData.Radius, out var bluePosition))
                return;

            state.Actors.Spawn(ActorKind.Player, FactionId.Red, redData.WithPosition(redPosition));
            state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, blueData.WithPosition(bluePosition));
            _unitIndex++;
            if (_unitIndex < totalUnits)
                _nextSpawnTime = _elapsed + _unitInterval;
        }

        private bool TryFindSpawnPosition(
            RpgSimulationState state,
            SimulationVector2 origin,
            float radius,
            out SimulationVector2 position)
        {
            for (var ring = 0; ring < 16; ring++)
            {
                var searchRadius = ring * 0.75f;
                var sampleCount = ring == 0 ? 1 : ring * 8;
                for (var sample = 0; sample < sampleCount; sample++)
                {
                    var angle = ring == 0 ? 0f : sample * MathF.PI * 2f / sampleCount;
                    var candidate = origin + new SimulationVector2(
                        MathF.Cos(angle) * searchRadius,
                        MathF.Sin(angle) * searchRadius);
                    if (IsSpawnClear(state, candidate, radius))
                    {
                        position = candidate;
                        return true;
                    }
                }
            }

            position = origin;
            return false;
        }

        private static bool IsSpawnClear(RpgSimulationState state, SimulationVector2 position, float radius)
        {
            if (!state.Navigation.IsPositionWalkable(position, radius))
                return false;

            for (var index = 0; index < state.Actors.SlotCount; index++)
            {
                if (!state.Actors.TryGetAt(index, out var actor) ||
                    actor.Components.Get<HealthComponent>().IsDead)
                    continue;

                var otherPosition = actor.Components.Get<PositionComponent>().Position;
                var otherRadius = actor.Components.Get<ColliderComponent>().Compound.BoundingRadius;
                var minimumDistance = radius + otherRadius;
                if ((position - otherPosition).LengthSquared < minimumDistance * minimumDistance)
                    return false;
            }

            return true;
        }
    }
}
