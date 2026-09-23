using System;
using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class OrcaAvoidanceSolver
    {
        private const float Epsilon = 0.00001f;
        private const int MaxLines = 128;
        private readonly List<EntityId> _nearby = new();
        private readonly OrcaLine[] _lines = new OrcaLine[MaxLines];
        private readonly OrcaLine[] _projectionLines = new OrcaLine[MaxLines];
        private readonly float _timeHorizon;

        public OrcaAvoidanceSolver(float timeHorizon = 0.75f)
        {
            _timeHorizon = timeHorizon > 0f ? timeHorizon : 0.75f;
        }

        public SimulationVector2 Solve(
            Actor actor,
            SimulationVector2 preferredDirection,
            ActorRegistry actors,
            SpatialHash spatialHash) =>
            Solve(actor, preferredDirection, actors, spatialHash, 1f / 30f);

        public SimulationVector2 Solve(
            Actor actor,
            SimulationVector2 preferredDirection,
            ActorRegistry actors,
            SpatialHash spatialHash,
            float deltaTime)
        {
            var position = actor.Components.Get<PositionComponent>().Position;
            var movement = actor.Components.Get<MovementComponent>();
            var radius = actor.Components.Get<BodyComponent>().Radius;
            var maxSpeed = movement.Speed;
            if (maxSpeed <= 0f) return SimulationVector2.Zero;

            var preferredVelocity = preferredDirection.Normalized() * maxSpeed;
            spatialHash.Collect(position, maxSpeed * _timeHorizon + radius + 1f, _nearby);
            var lineCount = BuildLines(actor, position, radius, actors, deltaTime, preferredVelocity);
            if (lineCount == 0) return preferredDirection.Normalized();

            var result = SimulationVector2.Zero;
            var feasible = LinearProgram2(_lines, lineCount, 0, maxSpeed, preferredVelocity, false, ref result);
            if (!feasible)
                LinearProgram3(_lines, lineCount, maxSpeed, preferredVelocity, ref result);

            return result.LengthSquared <= Epsilon
                ? DeterministicEscape(actor, preferredVelocity)
                : result.Normalized();
        }

        private int BuildLines(
            Actor actor,
            SimulationVector2 position,
            float radius,
            ActorRegistry actors,
            float deltaTime,
            SimulationVector2 preferredVelocity)
        {
            var velocity = preferredVelocity;
            var lineCount = 0;
            var inverseTimeStep = 1f / MathF.Max(deltaTime, Epsilon);

            for (var index = 0; index < _nearby.Count && lineCount < MaxLines; index++)
            {
                if (!actors.TryGet(_nearby[index], out var other) || other.Id == actor.Id ||
                    other.Components.Get<HealthComponent>().IsDead)
                    continue;

                var otherPosition = other.Components.Get<PositionComponent>().Position;
                var otherMovement = other.Components.Get<MovementComponent>();
                var relativePosition = otherPosition - position;
                var relativeVelocity = velocity - otherMovement.DesiredDirection * otherMovement.Speed;
                var combinedRadius = radius + other.Components.Get<BodyComponent>().Radius;
                var distanceSquared = relativePosition.LengthSquared;
                SimulationVector2 direction;
                SimulationVector2 u;

                if (distanceSquared > combinedRadius * combinedRadius)
                {
                    var w = relativeVelocity - relativePosition / _timeHorizon;
                    var wLengthSquared = w.LengthSquared;
                    var dotProduct = Dot(w, relativePosition);
                    if (dotProduct < 0f && dotProduct * dotProduct > combinedRadius * combinedRadius * wLengthSquared)
                    {
                        var wLength = MathF.Sqrt(wLengthSquared);
                        var unitW = w / MathF.Max(wLength, Epsilon);
                        direction = Perpendicular(unitW);
                        u = unitW * (combinedRadius / _timeHorizon - wLength);
                    }
                    else
                    {
                        var leg = MathF.Sqrt(MathF.Max(0f, distanceSquared - combinedRadius * combinedRadius));
                        if (Determinant(relativePosition, w) >= 0f)
                            direction = new SimulationVector2(
                                (relativePosition.X * leg - relativePosition.Y * combinedRadius) / distanceSquared,
                                (relativePosition.X * combinedRadius + relativePosition.Y * leg) / distanceSquared);
                        else
                            direction = new SimulationVector2(
                                (relativePosition.X * leg + relativePosition.Y * combinedRadius) / distanceSquared,
                                (-relativePosition.X * combinedRadius + relativePosition.Y * leg) / distanceSquared);

                        u = direction * Dot(relativeVelocity, direction) - relativeVelocity;
                    }
                }
                else
                {
                    var w = relativeVelocity - relativePosition * inverseTimeStep;
                    var wLength = MathF.Sqrt(w.LengthSquared);
                    var unitW = wLength <= Epsilon
                        ? TieBreakDirection(actor.Id, other.Id)
                        : w / wLength;
                    direction = Perpendicular(unitW);
                    u = unitW * (combinedRadius * inverseTimeStep - wLength);
                }

                _lines[lineCount++] = new OrcaLine(velocity + u * 0.5f, direction);
            }

            return lineCount;
        }

        private void LinearProgram3(
            OrcaLine[] lines,
            int lineCount,
            float radius,
            SimulationVector2 preferredVelocity,
            ref SimulationVector2 result)
        {
            var distance = 0f;
            for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                if (Determinant(lines[lineIndex].Direction, lines[lineIndex].Point - result) <= distance)
                    continue;

                var projectionCount = 0;
                for (var previous = 0; previous < lineIndex; previous++)
                {
                    var determinant = Determinant(lines[lineIndex].Direction, lines[previous].Direction);
                    SimulationVector2 point;
                    if (MathF.Abs(determinant) <= Epsilon)
                    {
                        if (Dot(lines[lineIndex].Direction, lines[previous].Direction) > 0f) continue;
                        point = (lines[lineIndex].Point + lines[previous].Point) * 0.5f;
                    }
                    else
                    {
                        point = lines[lineIndex].Point + lines[lineIndex].Direction *
                            (Determinant(lines[previous].Direction, lines[lineIndex].Point - lines[previous].Point) / determinant);
                    }

                    _projectionLines[projectionCount++] = new OrcaLine(
                        point, (lines[previous].Direction - lines[lineIndex].Direction).Normalized());
                }

                var previousResult = result;
                if (!LinearProgram2(_projectionLines, projectionCount, 0, radius, Perpendicular(lines[lineIndex].Direction), true, ref result))
                    result = previousResult;
                distance = Determinant(lines[lineIndex].Direction, lines[lineIndex].Point - result);
            }
        }

        private static bool LinearProgram2(
            OrcaLine[] lines,
            int lineCount,
            int lineStart,
            float radius,
            SimulationVector2 preferredVelocity,
            bool directionOpt,
            ref SimulationVector2 result)
        {
            result = directionOpt
                ? preferredVelocity.Normalized() * radius
                : preferredVelocity.LengthSquared > radius * radius
                    ? preferredVelocity.Normalized() * radius
                    : preferredVelocity;

            for (var lineIndex = lineStart; lineIndex < lineCount; lineIndex++)
            {
                if (Determinant(lines[lineIndex].Direction, lines[lineIndex].Point - result) <= 0f)
                    continue;

                var dotProduct = Dot(lines[lineIndex].Point, lines[lineIndex].Direction);
                var discriminant = dotProduct * dotProduct + radius * radius - lines[lineIndex].Point.LengthSquared;
                if (discriminant < 0f) return false;

                var root = MathF.Sqrt(discriminant);
                var left = -dotProduct - root;
                var right = -dotProduct + root;
                for (var previous = 0; previous < lineIndex; previous++)
                {
                    var denominator = Determinant(lines[lineIndex].Direction, lines[previous].Direction);
                    var numerator = Determinant(lines[previous].Direction, lines[lineIndex].Point - lines[previous].Point);
                    if (MathF.Abs(denominator) <= Epsilon)
                    {
                        if (numerator < 0f) return false;
                        continue;
                    }

                    var value = numerator / denominator;
                    if (denominator >= 0f) right = MathF.Min(right, value);
                    else left = MathF.Max(left, value);
                    if (left > right) return false;
                }

                var t = directionOpt
                    ? Dot(preferredVelocity, lines[lineIndex].Direction) > 0f ? right : left
                    : Dot(preferredVelocity - lines[lineIndex].Point, lines[lineIndex].Direction);
                t = MathF.Max(left, MathF.Min(right, t));
                result = lines[lineIndex].Point + lines[lineIndex].Direction * t;
            }

            return true;
        }

        private static SimulationVector2 DeterministicEscape(Actor actor, SimulationVector2 preferredVelocity) =>
            preferredVelocity.LengthSquared > Epsilon
                ? preferredVelocity.Normalized()
                : TieBreakDirection(actor.Id, EntityId.None);

        private static SimulationVector2 TieBreakDirection(EntityId left, EntityId right) =>
            left.Index <= right.Index ? new SimulationVector2(1f, 0f) : new SimulationVector2(-1f, 0f);

        private static SimulationVector2 Perpendicular(SimulationVector2 value) => new(-value.Y, value.X);
        private static float Dot(SimulationVector2 left, SimulationVector2 right) => left.X * right.X + left.Y * right.Y;
        private static float Determinant(SimulationVector2 left, SimulationVector2 right) => left.X * right.Y - left.Y * right.X;
    }
}
