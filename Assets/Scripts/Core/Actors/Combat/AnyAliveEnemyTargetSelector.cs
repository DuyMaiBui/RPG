using System;
using System.Collections.Generic;
using RPG.Core.Navigation;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class AnyAliveEnemyTargetSelector
    {
        private readonly List<EntityId> _nearby = new();

        public bool TrySelect(Actor attacker, ActorRegistry actors, out EntityId targetId)
        {
            return TrySelect(attacker, actors, null, out targetId);
        }

        public bool TrySelect(Actor attacker, ActorRegistry actors, SpatialHash spatialHash, out EntityId targetId)
        {
            targetId = EntityId.None;
            var faction = attacker.Components.Get<FactionComponent>().Faction;
            var position = attacker.Components.Get<PositionComponent>().Position;
            var body = attacker.Components.Get<BodyComponent>();
            var vision = attacker.Components.Get<VisionComponent>();
            var priority = attacker.Components.Get<TargetPriorityComponent>().Mode;
            var bestDistance = float.MaxValue;
            var bestHealth = int.MaxValue;

            if (spatialHash == null)
            {
                for (var index = 0; index < actors.SlotCount; index++)
                {
                    if (!TryConsider(attacker, actors, index, faction, position, body, vision, priority,
                            ref bestDistance, ref bestHealth, ref targetId))
                        continue;
                }
            }
            else
            {
                spatialHash.Collect(position, vision.Range + body.Radius + 1f, _nearby);
                for (var index = 0; index < _nearby.Count; index++)
                {
                    if (!actors.TryGet(_nearby[index], out var candidate)) continue;
                    if (candidate.Id == attacker.Id) continue;
                    Consider(attacker, candidate, faction, position, body, vision, priority,
                        ref bestDistance, ref bestHealth, ref targetId);
                }
            }

            return !targetId.IsNone;
        }

        private static bool TryConsider(
            Actor attacker,
            ActorRegistry actors,
            int index,
            FactionId faction,
            SimulationVector2 position,
            BodyComponent body,
            VisionComponent vision,
            TargetPriorityMode priority,
            ref float bestDistance,
            ref int bestHealth,
            ref EntityId targetId)
        {
            if (!actors.TryGetAt(index, out var candidate) || candidate.Id == attacker.Id)
                return false;

            Consider(attacker, candidate, faction, position, body, vision, priority,
                ref bestDistance, ref bestHealth, ref targetId);
            return true;
        }

        private static void Consider(
            Actor attacker,
            Actor candidate,
            FactionId faction,
            SimulationVector2 position,
            BodyComponent body,
            VisionComponent vision,
            TargetPriorityMode priority,
            ref float bestDistance,
            ref int bestHealth,
            ref EntityId targetId)
        {

            var candidateFaction = candidate.Components.Get<FactionComponent>().Faction;
            var candidateHealth = candidate.Components.Get<HealthComponent>();
            if (candidateFaction == faction || candidateHealth.IsDead)
                return;

                var candidatePosition = candidate.Components.Get<PositionComponent>().Position;
                var candidateBody = candidate.Components.Get<BodyComponent>();
                var difference = candidatePosition - position;
                var visionDistance = vision.Range + body.Radius + candidateBody.Radius;
                var distance = difference.LengthSquared;
            if (distance > visionDistance * visionDistance)
                return;

                var isBetter = priority == TargetPriorityMode.LowestHealth
                    ? candidateHealth.CurrentHealth < bestHealth ||
                      (candidateHealth.CurrentHealth == bestHealth && candidate.Id.Index < targetId.Index)
                    : distance < bestDistance ||
                      (Math.Abs(distance - bestDistance) < 0.0001f && candidate.Id.Index < targetId.Index);
            if (!isBetter) return;

                bestDistance = distance;
                bestHealth = candidateHealth.CurrentHealth;
            targetId = candidate.Id;
        }
    }
}
