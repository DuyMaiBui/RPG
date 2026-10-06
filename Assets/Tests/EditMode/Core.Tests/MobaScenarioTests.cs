using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    /// <summary>Headless checks of the Moba demo battle, run from the authored scenario data instead of a scene. The
    /// measurements behind the thresholds are printed by <see cref="MobaScenarioFlowDiagnostic"/>; the two explicit
    /// tests at the bottom record defects that are measured but not fixed yet.</summary>
    public sealed class MobaScenarioTests
    {
        private const int ShortRunTicks = 1800;

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
            Assert.That(metrics.FailedToCloseUnits, Is.LessThanOrEqualTo(metrics.Spawned * 5 / 100 + 1),
                "units should not spend ten seconds failing to close on a reachable target");
        }

        [Test]
        [Explicit("Known defect (2026-10-06): basic attacks almost never fire. Measurement harness: MobaScenarioFlowDiagnostic.")]
        public void Battle_MeleeUnitsLandBasicAttacks()
        {
            var metrics = Run(ShortRunTicks);

            Assert.That(metrics.MeleeNeverAttacked, Is.Zero,
                $"melee units never land a basic attack: {metrics.MeleeNeverAttacked} of {metrics.MeleeUnits} melee units, "
                + $"{metrics.TotalAttacks} basic attacks and {metrics.TotalCasts} ability casts in the whole run. "
                + "Melee needs contact (0.97 centre distance) but a friendly rank that stops at its own attack range "
                + $"blocks {metrics.MeleeFrontBlockedByAllyFraction * 100f:0}% of the approach samples.");
        }

        [Test]
        [Explicit("Known defect (2026-10-06): units can be pinned on terrain. Measurement harness: MobaScenarioFlowDiagnostic.")]
        public void Battle_UnitsAreNotPinnedOnTerrain()
        {
            var metrics = Run(ShortRunTicks);

            Assert.That(metrics.FractionIdle, Is.LessThanOrEqualTo(0.05f),
                $"{metrics.IdleUnits} of {metrics.Spawned} units never acquired a target and stopped after roughly "
                + "eight units of travel, pinned against the obstacle field; with the obstacles removed the same run "
                + "pins none of them.");
            Assert.That(metrics.FractionStalledLong, Is.LessThanOrEqualTo(0.05f),
                $"{metrics.FractionStalledLong * 100f:0}% of units stood still for ten seconds or more");
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
