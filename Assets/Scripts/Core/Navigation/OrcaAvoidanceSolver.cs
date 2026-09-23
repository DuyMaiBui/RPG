using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class OrcaAvoidanceSolver
    {
        private readonly List<EntityId> _nearby = new();
        private readonly float _timeHorizon;

        public OrcaAvoidanceSolver(float timeHorizon = 0.75f)
        {
            _timeHorizon = timeHorizon > 0f ? timeHorizon : 0.75f;
        }

        public SimulationVector2 Solve(
            Actor actor,
            SimulationVector2 preferredDirection,
            ActorRegistry actors,
            SpatialHash spatialHash)
        {
            var position = actor.Components.Get<PositionComponent>().Position;
            var body = actor.Components.Get<BodyComponent>();
            var speed = actor.Components.Get<MovementComponent>().Speed;
            if (speed <= 0f) return SimulationVector2.Zero;

            spatialHash.Collect(position, speed * _timeHorizon + body.Radius + 1f, _nearby);
            if (_nearby.Count == 0) return preferredDirection;

            var bestDirection = SimulationVector2.Zero;
            var bestScore = float.MaxValue;
            for (var candidateIndex = 0; candidateIndex < 17; candidateIndex++)
            {
                var direction = Candidate(preferredDirection, candidateIndex);
                if (!IsSafe(position, direction, speed, body.Radius, actor, actors)) continue;

                var score = 1f - Dot(direction, preferredDirection);
                if (score >= bestScore) continue;
                bestDirection = direction;
                bestScore = score;
            }

            return bestScore < float.MaxValue ? bestDirection : SimulationVector2.Zero;
        }

        private bool IsSafe(
            SimulationVector2 position,
            SimulationVector2 direction,
            float speed,
            float radius,
            Actor actor,
            ActorRegistry actors)
        {
            var velocity = direction * speed;
            for (var index = 0; index < _nearby.Count; index++)
            {
                if (!actors.TryGet(_nearby[index], out var other) || other.Id == actor.Id ||
                    other.Components.Get<HealthComponent>().IsDead)
                    continue;

                var offset = position - other.Components.Get<PositionComponent>().Position;
                var combinedRadius = radius + other.Components.Get<BodyComponent>().Radius;
                var otherVelocity = other.Components.Get<MovementComponent>().DesiredDirection *
                                    other.Components.Get<MovementComponent>().Speed;
                var relativeVelocity = velocity - otherVelocity;
                var relativeSpeedSquared = relativeVelocity.LengthSquared;
                var timeToClosest = relativeSpeedSquared <= 0.000001f
                    ? 0f
                    : -(offset.X * relativeVelocity.X + offset.Y * relativeVelocity.Y) / relativeSpeedSquared;
                timeToClosest = System.MathF.Max(0f, System.MathF.Min(_timeHorizon, timeToClosest));
                var relativePosition = offset + relativeVelocity * timeToClosest;
                if (relativePosition.LengthSquared < combinedRadius * combinedRadius)
                    return false;
            }

            return true;
        }

        private static SimulationVector2 Candidate(SimulationVector2 preferred, int index)
        {
            if (index == 0) return preferred;
            var angle = (index - 1) * (System.MathF.PI * 2f / 16f);
            var cosine = System.MathF.Cos(angle);
            var sine = System.MathF.Sin(angle);
            return new SimulationVector2(
                preferred.X * cosine - preferred.Y * sine,
                preferred.X * sine + preferred.Y * cosine).Normalized();
        }

        private static float Dot(SimulationVector2 left, SimulationVector2 right) => left.X * right.X + left.Y * right.Y;
    }
}
