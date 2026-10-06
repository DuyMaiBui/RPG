using System;
using System.Collections.Generic;
using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Core.Navigation;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    /// <summary>Regression guard for crowd movement. A dense group ordered through a narrow gap used to leave most of
    /// its units standing where they were: the per-tick separation resolved every overlap fully, so a push could be
    /// tens of times larger than the actor's own step and shoved units back the way they came. Separation is now a
    /// bounded relaxation and an actor that makes no headway steps sideways, so far fewer units stop for good.
    /// </summary>
    public sealed class CrowdMovementTests
    {
        private const int TotalTicks = 3600;
        private const int StallWindow = 120;
        private const int UnitCount = 60;
        private const float StallEpsilonSquared = 0.000001f;

        [Test]
        public void CrowdThroughANarrowGap_DoesNotLeaveMostUnitsStalled()
        {
            // 22 of 60 units used to be stalled after a minute; the bound leaves room either side of the measured 7.
            var stalled = CountStalledUnits(gapHalfWidth: 2f, out var passedGap);

            Assert.That(stalled, Is.LessThan(15), $"{stalled} of {UnitCount} units stopped moving for good");
            Assert.That(passedGap, Is.GreaterThan(40), $"only {passedGap} units got through the gap");
        }

        [Test]
        public void SingleUnit_ReachesTheFarSideThroughTheNarrowestGap()
        {
            var state = CreateState(gapHalfWidth: 1f);
            var ids = SpawnBlock(state, 1, new SimulationVector2(-24f, 0f));
            Assert.That(state.Actors.TryGet(ids[0], out var actor), Is.True);
            actor.Components.Get<OrderQueueComponent>().Enqueue(ActorOrder.Move(new SimulationVector2(24f, 0f)), true);

            Simulate(state, 3600);

            Assert.That(state.Actors.TryGet(ids[0], out var moved), Is.True);
            Assert.That(
                moved.Components.Get<PositionComponent>().Position.X,
                Is.GreaterThan(20f),
                "a lone unit must walk through the gap at full speed");
        }

        private static int CountStalledUnits(float gapHalfWidth, out int passedGap)
        {
            var destination = new SimulationVector2(24f, 0f);
            var state = CreateState(gapHalfWidth);
            var ids = SpawnBlock(state, UnitCount, new SimulationVector2(-24f, 0f));
            for (var index = 0; index < ids.Count; index++)
            {
                Assert.That(state.Actors.TryGet(ids[index], out var actor), Is.True);
                actor.Components.Get<OrderQueueComponent>().Enqueue(ActorOrder.Move(destination), true);
            }

            var lastProgress = new Dictionary<EntityId, int>();
            var previous = new Dictionary<EntityId, SimulationVector2>();
            var context = new SimulationContext<RpgSimulationState>(state, 1f / 60f);
            ISimulationApplication<RpgSimulationState> application = new RpgSimulationApplication();

            for (var tick = 0; tick < TotalTicks; tick++)
            {
                context.ResetForNextTick();
                var simulationTick = new SimulationTick(tick);
                application.BeginTick(context, simulationTick);
                application.Tick(context, simulationTick);
                application.HandleEvents(context, context.DrainEvents());
                context.CommitDeferredActions();
                TrackProgress(state, previous, lastProgress, tick);
            }

            passedGap = 0;
            var stalled = 0;
            for (var index = 0; index < state.Actors.SlotCount; index++)
            {
                if (!state.Actors.TryGetAt(index, out var actor)) continue;
                if (actor.Components.Get<PositionComponent>().Position.X > 2f)
                    passedGap++;
                if (lastProgress.TryGetValue(actor.Id, out var since) && TotalTicks - since >= StallWindow)
                    stalled++;
            }

            return stalled;
        }

        private static void Simulate(RpgSimulationState state, int ticks)
        {
            var context = new SimulationContext<RpgSimulationState>(state, 1f / 60f);
            ISimulationApplication<RpgSimulationState> application = new RpgSimulationApplication();
            for (var tick = 0; tick < ticks; tick++)
            {
                context.ResetForNextTick();
                var simulationTick = new SimulationTick(tick);
                application.BeginTick(context, simulationTick);
                application.Tick(context, simulationTick);
                application.HandleEvents(context, context.DrainEvents());
                context.CommitDeferredActions();
            }
        }

        private static void TrackProgress(
            RpgSimulationState state,
            Dictionary<EntityId, SimulationVector2> previous,
            Dictionary<EntityId, int> lastProgress,
            int tick)
        {
            for (var index = 0; index < state.Actors.SlotCount; index++)
            {
                if (!state.Actors.TryGetAt(index, out var actor)) continue;
                var current = actor.Components.Get<PositionComponent>().Position;
                if (!previous.TryGetValue(actor.Id, out var before))
                {
                    previous[actor.Id] = current;
                    lastProgress[actor.Id] = tick;
                    continue;
                }

                previous[actor.Id] = current;
                if ((current - before).LengthSquared > StallEpsilonSquared)
                    lastProgress[actor.Id] = tick;
            }
        }

        private static RpgSimulationState CreateState(float gapHalfWidth)
        {
            var state = new RpgSimulationState(60, 40, 1f);
            var wallHalf = (20f - gapHalfWidth) * 0.5f;
            var wallCenter = gapHalfWidth + wallHalf;
            state.Navigation.ApplyObstacle(new NavigationObstacle(1, new SimulationVector2(0f, -wallCenter), new SimulationVector2(0.5f, wallHalf)));
            state.Navigation.ApplyObstacle(new NavigationObstacle(2, new SimulationVector2(0f, wallCenter), new SimulationVector2(0.5f, wallHalf)));
            return state;
        }

        private static List<EntityId> SpawnBlock(RpgSimulationState state, int count, SimulationVector2 anchor)
        {
            var ids = new List<EntityId>(count);
            var columns = (int)MathF.Ceiling(MathF.Sqrt(count));
            var rows = (int)MathF.Ceiling(count / (float)columns);
            const float spacing = 0.8f;
            for (var index = 0; index < count; index++)
            {
                var row = index / columns;
                var column = index % columns;
                var position = new SimulationVector2(
                    anchor.X + column * spacing,
                    anchor.Y + (row - (rows - 1) * 0.5f) * spacing);
                if (!state.Navigation.IsPositionWalkable(position, 0.3f))
                    continue;

                ids.Add(state.Actors.Spawn(
                    ActorKind.Player,
                    FactionId.Red,
                    new ActorSpawnData(60, 1, position, 0.3f, 0.8f, 12f, 0.5f, 5f)));
            }

            return ids;
        }
    }
}
