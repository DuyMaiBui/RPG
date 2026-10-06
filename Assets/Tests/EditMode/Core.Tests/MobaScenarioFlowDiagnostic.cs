using System;
using System.IO;
using NUnit.Framework;
using RPG.Content;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    /// <summary>Measurement harness for the Moba demo scenario. The heavy runs are marked explicit so the ordinary
    /// suite stays fast; run one on demand with
    /// <c>dotnet test --filter FullyQualifiedName~MobaScenarioFlowDiagnostic</c>. The full report is written to
    /// <c>/tmp/rpg_flow_report.txt</c> as well as to the test log.</summary>
    public sealed class MobaScenarioFlowDiagnostic
    {


        [Test]
        [Explicit("Measurement harness: about two minutes of CPU. Run on demand.")]
        public void Moba_ReportsAttackMovementAndStallData()
        {
            var state = MobaScenarioData.CreateState();
            Assert.That(ScenarioFlowDiagnosticFingerprint(state), Is.EqualTo(MobaScenarioData.NavigationFingerprint));
            var metrics = new MobaScenarioMetrics();
            metrics.Run(state, 9000, 600, 1f / MobaScenarioData.TickRate);
            Report(metrics, "/tmp/rpg_flow_report.txt");
        }

        [Test]
        [Explicit("A/B measurement: same scenario with the static obstacles removed.")]
        public void Moba_ReportsAttackMovementAndStallData_WithoutObstacles()
        {
            var metrics = new MobaScenarioMetrics();
            metrics.Run(MobaScenarioData.CreateState(withObstacles: false), 6000, 600, 1f / MobaScenarioData.TickRate);
            Report(metrics, "/tmp/rpg_flow_report_no_obstacles.txt");
        }

        [TestCase(AttackType.Melee, AttackType.Melee, 8f)]
        [TestCase(AttackType.Melee, AttackType.Projectile, 8f)]
        [TestCase(AttackType.Projectile, AttackType.Projectile, 8f)]
        [Explicit("Duel measurement: two actors and nothing else in the way.")]
        public void Duel_ReportsBasicAttackBehaviour(AttackType redType, AttackType blueType, float separation)
        {
            var catalog = MobaScenarioData.CreateCatalog();
            var state = new RpgSimulationState(60, 40, 1f);
            var red = MobaScenarioData.Spawn(state, catalog, FactionId.Red, redType,
                new SimulationVector2(-separation * 0.5f, 0f), MobaScenarioData.WaveAttackCooldown);
            var blue = MobaScenarioData.Spawn(state, catalog, FactionId.Blue, blueType,
                new SimulationVector2(separation * 0.5f, 0f), MobaScenarioData.WaveAttackCooldown);
            var context = new SimulationContext<RpgSimulationState>(state, 1f / MobaScenarioData.TickRate);
            ISimulationApplication<RpgSimulationState> application = new RpgSimulationApplication();
            var attacks = 0;
            var casts = 0;
            var damage = 0;
            var minimumDistance = float.MaxValue;
            for (var tick = 0; tick < 1800; tick++)
            {
                context.ResetForNextTick();
                var simulationTick = new SimulationTick(tick);
                application.BeginTick(context, simulationTick);
                application.Tick(context, simulationTick);
                var events = context.DrainEvents();
                application.HandleEvents(context, events);
                context.CommitDeferredActions();
                for (var index = 0; index < events.Count; index++)
                {
                    switch (events[index])
                    {
                        case ActorAttackStarted: attacks++; break;
                        case ActorAbilityCast: casts++; break;
                        case ActorDamaged damaged: damage += damaged.Damage; break;
                    }
                }

                if (state.Actors.TryGet(red, out var redActor) && state.Actors.TryGet(blue, out var blueActor))
                {
                    var distance = Distance(redActor, blueActor);
                    if (distance < minimumDistance) minimumDistance = distance;
                    if (tick % 15 == 0 && tick <= 300)
                    {
                        Console.WriteLine($"[duel] {redType} vs {blueType} t={tick,4} d={distance,6:0.00} "
                            + $"redDir={redActor.Components.Get<MovementComponent>().DesiredDirection} "
                            + $"redTarget={redActor.Components.Get<TargetComponent>().CurrentTarget.Index,3} "
                            + $"redHp={redActor.Components.Get<HealthComponent>().CurrentHealth,3} "
                            + $"blueDir={blueActor.Components.Get<MovementComponent>().DesiredDirection} "
                            + $"blueTarget={blueActor.Components.Get<TargetComponent>().CurrentTarget.Index,3} "
                            + $"blueHp={blueActor.Components.Get<HealthComponent>().CurrentHealth,3}");
                    }
                }
            }

            Console.WriteLine($"[duel] {redType} vs {blueType} at {separation}: attacks {attacks}, casts {casts}, "
                + $"damage {damage}, min distance {minimumDistance:0.00}");
        }

        private static float Distance(Actor left, Actor right)
        {
            var delta = right.Components.Get<PositionComponent>().Position -
                        left.Components.Get<PositionComponent>().Position;
            return (float)Math.Sqrt(delta.LengthSquared);
        }

        private static void Report(MobaScenarioMetrics metrics, string path)
        {
            var text = metrics.ReportText;
            File.WriteAllText(path, text);
            TestContext.Progress.WriteLine(text);
        }

        private static uint ScenarioFlowDiagnosticFingerprint(RpgSimulationState state)
        {
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

            Assert.That(blocked, Is.EqualTo(MobaScenarioData.NavigationBlockedSamples), "blocked navigation samples");
            return hash;
        }
    }
}
