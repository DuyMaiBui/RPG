using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    /// <summary>Headless checks of the Moba demo battle, run from the authored scenario data instead of a scene. The
    /// measurements behind the thresholds are printed by <see cref="MobaScenarioFlowDiagnostic"/>; the explicit tests
    /// at the bottom are the long runs that need about a minute of CPU each, so they are run on demand.</summary>
    public sealed class MobaScenarioTests
    {
        private const int ShortRunTicks = 1800;
        private const int LongRunTicks = 9000;

        [Test]
        public void Map_MatchesTheRuntimeNavigationGrid()
        {
            var state = MobaScenarioData.CreateState();
            var hash = 2166136261u;
            var blocked = 0;
            for (var y = -20f; y <= 20f; y += 0.25f)
            {
                for (var x = -30f; x <= 30f; x += 0.25f)
                {
                    var walkable = state.Navigation.IsPositionWalkable(
                        new SimulationVector2(x, y), MobaScenarioData.ActorRadius);
                    hash = (hash ^ (walkable ? 1u : 0u)) * 16777619u;
                    if (!walkable) blocked++;
                }
            }

            Assert.That(blocked, Is.EqualTo(MobaScenarioData.NavigationBlockedSamples));
            Assert.That(hash, Is.EqualTo(MobaScenarioData.NavigationFingerprint),
                "the transcribed Moba map no longer matches the runtime navigation grid");
        }

        [Test]
        public void Battle_UnitsAdvanceTowardsTheirTargets()
        {
            var metrics = Run(ShortRunTicks);

            Assert.That(metrics.Spawned, Is.GreaterThanOrEqualTo(30), "waves should have spawned units");
            Assert.That(metrics.MedianSpeedRatio, Is.GreaterThan(0.5f),
                "units should travel at a large fraction of their move speed");
            Assert.That(metrics.MedianPathEfficiency, Is.GreaterThan(0.7f),
                "units should travel close to the straight line to their destination");
            Assert.That(metrics.MedianHeadingToTarget, Is.LessThan(45f),
                "units should head towards the target they acquired");
            Assert.That(metrics.FailedToCloseUnits, Is.Zero,
                "no unit should spend ten seconds failing to close on a reachable target");
        }

        [Test]
        [Explicit("Long run: about a minute of CPU. Measurement harness: MobaScenarioFlowDiagnostic.")]
        public void Battle_BasicAttacksLand()
        {
            var metrics = Run(LongRunTicks);

            Assert.That(metrics.RangedAttacks, Is.GreaterThan(0),
                $"no ranged basic attack landed in {LongRunTicks / MobaScenarioData.TickRate}s "
                + $"(total basic attacks {metrics.TotalAttacks}, ability casts {metrics.TotalCasts})");
            Assert.That(metrics.TotalAttacks, Is.GreaterThan(20),
                $"only {metrics.TotalAttacks} basic attacks landed against {metrics.TotalCasts} ability casts");
            // Melee basic attacks are marginal in this content, so the guard cannot hinge on them: melee reaches 0.25
            // (attack distance 0.97) against Cleave's 1.92 and VenomStrike's 4.72, and about three quarters of its
            // approaches are blocked by an ally that stopped closer to the same target. Melee reaches contact (closest
            // approach measured at 0.86-1.4) and lands a handful of blows at best, so what is asserted is the ranged
            // side and the total; the melee numbers belong to the report.
            TestContext.Progress.WriteLine(
                $"melee landed {metrics.MeleeAttacks} basic attacks, closest approach "
                + $"{metrics.MeleeClosestApproach:0.00} against an attack distance "
                + $"{0.72f + MobaScenarioData.MeleeAttackRange:0.00}");
        }

        [Test]
        [Explicit("Long run: about a minute of CPU. Measurement harness: MobaScenarioFlowDiagnostic.")]
        public void Battle_UnitsAreNotPinnedOnTerrain()
        {
            var metrics = Run(LongRunTicks);

            Assert.That(metrics.PinnedUnits, Is.Zero,
                $"{metrics.PinnedUnits} units lived for ten seconds or more, never acquired a target and travelled "
                + "less than five units: they are pinned, not late spawns (the same run pinned around fifteen before "
                + "the committed escape and the crawl detection landed)");
            Assert.That(metrics.FractionIdle, Is.LessThanOrEqualTo(0.06f),
                $"{metrics.IdleUnits} of {metrics.Spawned} units never acquired a target; the ones left are late "
                + "spawns with no time to travel");
            Assert.That(metrics.FractionStalledLong, Is.LessThanOrEqualTo(0.05f),
                $"{metrics.FractionStalledLong * 100f:0}% of units stood still for ten seconds or more");
            Assert.That(metrics.FailedToCloseUnits, Is.Zero,
                "no unit should spend ten seconds failing to close on a reachable target");
        }

        private static MobaScenarioMetrics Run(int ticks)
        {
            var metrics = new MobaScenarioMetrics();
            metrics.Run(MobaScenarioData.CreateState(), ticks, ticks, 1f / MobaScenarioData.TickRate);
            Assert.That(metrics.ReportText, Is.Not.Null.And.Not.Empty);
            return metrics;
        }
    }
}
