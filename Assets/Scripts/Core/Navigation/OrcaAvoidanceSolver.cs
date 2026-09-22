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
            var avoidance = SimulationVector2.Zero;
            for (var index = 0; index < _nearby.Count; index++)
            {
                if (!actors.TryGet(_nearby[index], out var other) || other.Id == actor.Id ||
                    other.Components.Get<HealthComponent>().IsDead)
                    continue;

                var otherPosition = other.Components.Get<PositionComponent>().Position;
                var offset = position - otherPosition;
                var combinedRadius = body.Radius + other.Components.Get<BodyComponent>().Radius;
                var distanceSquared = offset.LengthSquared;
                var predicted = offset + (preferredDirection * speed -
                    other.Components.Get<MovementComponent>().DesiredDirection *
                    other.Components.Get<MovementComponent>().Speed) * _timeHorizon;
                var predictedDistanceSquared = predicted.LengthSquared;
                if (predictedDistanceSquared >= combinedRadius * combinedRadius)
                    continue;

                var direction = distanceSquared <= 0.000001f
                    ? new SimulationVector2(actor.Id.Index < other.Id.Index ? -1f : 1f, 0f)
                    : offset.Normalized();
                var penetration = combinedRadius - System.MathF.Sqrt(System.MathF.Max(0f, distanceSquared));
                var urgency = 1f + penetration / combinedRadius;
                avoidance += direction * urgency;
            }

            return (preferredDirection + avoidance).Normalized();
        }
    }
}
