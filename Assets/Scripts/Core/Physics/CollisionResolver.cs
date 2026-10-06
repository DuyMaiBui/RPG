using RPG.Simulation.Contracts;

namespace RPG.Core.Physics
{
    /// <summary>Deterministic overlap resolution for the lightweight collision layer. It separates circles (the
    /// bounding approximation the simulation uses for actors) so no solver is involved and the result depends only on
    /// the inputs.</summary>
    public static class CollisionResolver
    {
        /// <summary>Pushes a circle out of another circle along the shortest direction and returns the adjusted centre.
        /// When the centres coincide, <paramref name="tieBreak"/> (-1 or +1) selects a fixed axis so the result stays
        /// deterministic. A non-overlapping pair is returned unchanged.</summary>
        public static SimulationVector2 SeparateCircles(
            SimulationVector2 position,
            float radius,
            SimulationVector2 otherPosition,
            float otherRadius,
            float tieBreak)
        {
            var minimumDistance = radius + otherRadius;
            var difference = position - otherPosition;
            var distanceSquared = difference.LengthSquared;
            if (distanceSquared >= minimumDistance * minimumDistance)
                return position;

            var direction = distanceSquared <= 0.000001f
                ? new SimulationVector2(tieBreak < 0f ? -1f : 1f, 0f)
                : difference.Normalized();
            return otherPosition + direction * minimumDistance;
        }
    }
}
