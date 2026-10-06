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
        /// deterministic. A non-overlapping pair is returned unchanged.
        /// <para><paramref name="relaxation"/> scales how much of the overlap is corrected in one call. Crowds need a
        /// relaxed push: resolving a full overlap in a single tick moves an actor further than its own movement, so
        /// the separation — not the actor's intent — decides where it ends up, and a jam pushes actors backwards.
        /// The default of 1 keeps the exact-separation behaviour for single-pair callers.</para></summary>
        public static SimulationVector2 SeparateCircles(
            SimulationVector2 position,
            float radius,
            SimulationVector2 otherPosition,
            float otherRadius,
            float tieBreak,
            float relaxation = 1f)
        {
            var minimumDistance = radius + otherRadius;
            var difference = position - otherPosition;
            var distanceSquared = difference.LengthSquared;
            if (distanceSquared >= minimumDistance * minimumDistance)
                return position;

            var direction = distanceSquared <= 0.000001f
                ? new SimulationVector2(tieBreak < 0f ? -1f : 1f, 0f)
                : difference.Normalized();
            var separated = otherPosition + direction * minimumDistance;
            return relaxation >= 1f ? separated : position + (separated - position) * relaxation;
        }
    }
}
