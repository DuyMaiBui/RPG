using System;
using System.Collections.Generic;
using System.Diagnostics;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Physics.Native;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Opt-in step-time benchmark of the real native backends:
       `managed_kernel_tests.dll bench [--check]` (budgets scale with AURA_BENCH_BUDGET_SCALE, default 1).
       Piles of boxes fall onto a ground plane; the per-step wall time is reported after a warm-up. */
    public static class BenchRunner
    {
        private const float Dt = 1f / 60f;
        private const int WarmupSteps = 30;
        private const int MeasuredSteps = 240;

        /* Ceilings about 5x the measured Apple-silicon release numbers (3d_1000 about 1.2 ms, 2d_1000 about 0.26 ms) so a
           slower CI machine does not flake while a several-fold regression still fails; scale them with
           AURA_BENCH_BUDGET_SCALE. Milliseconds per step. */
        private static readonly (string Name, AuraPhysicsMode Mode, int Bodies, double AvgBudgetMs, double P95BudgetMs)[] Scenarios =
        {
            ("3d_250", AuraPhysicsMode.Full3D, 250, 2.0, 4.0),
            ("3d_1000", AuraPhysicsMode.Full3D, 1000, 6.0, 10.0),
            ("2d_250", AuraPhysicsMode.Plane2D, 250, 0.5, 1.0),
            ("2d_1000", AuraPhysicsMode.Plane2D, 1000, 2.0, 4.0),
        };

        public static int Run(string[] args)
        {
            var check = Array.IndexOf(args, "--check") >= 0;
            var scale = 1.0;
            var env = Environment.GetEnvironmentVariable("AURA_BENCH_BUDGET_SCALE");
            if (!string.IsNullOrEmpty(env) && double.TryParse(env, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                scale = parsed;

            if (!NativePhysicsBackend.IsAvailable())
            {
                Console.WriteLine("BENCH_FAIL libaura is not loadable.");
                return 2;
            }

            var failures = 0;
            foreach (var scenario in Scenarios)
            {
                var times = Measure(scenario.Mode, scenario.Bodies);
                times.Sort();
                var avg = 0.0;
                foreach (var value in times)
                    avg += value;
                avg /= times.Count;
                var p95 = times[(int)(times.Count * 0.95) - 1];
                var max = times[times.Count - 1];
                var verdict = "";
                if (check)
                {
                    var ok = avg <= scenario.AvgBudgetMs * scale && p95 <= scenario.P95BudgetMs * scale;
                    verdict = ok ? " PASS" : " FAIL";
                    if (!ok)
                        failures++;
                }

                Console.WriteLine($"bench {scenario.Name} bodies={scenario.Bodies} steps={times.Count} avg_ms={avg:F3} p95_ms={p95:F3} max_ms={max:F3} budget_avg={scenario.AvgBudgetMs * scale:F1} budget_p95={scenario.P95BudgetMs * scale:F1}{verdict}");
            }

            Console.WriteLine(check ? (failures == 0 ? "BENCH_OK" : $"BENCH_FAIL ({failures} over budget)") : "BENCH_DONE");
            return failures == 0 ? 0 : 1;
        }

        private static List<double> Measure(AuraPhysicsMode mode, int bodies)
        {
            using var world = new AuraSimulationWorld(new NativePhysicsBackend(), new AuraWorldDefinition(mode, initialBodyCapacity: bodies + 16));
            var is3D = mode == AuraPhysicsMode.Full3D;
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(is3D ? 40f : 80f, 0.5f, is3D ? 40f : 1f))));

            for (var index = 0; index < bodies; index++)
            {
                // A deterministic pile: 10x10 columns per layer in 3D, 25 columns in 2D, 1.2 m pitch.
                float x, y, z;
                if (is3D)
                {
                    x = (index % 10) * 1.2f - 6f;
                    z = ((index / 10) % 10) * 1.2f - 6f;
                    y = 1f + (index / 100) * 1.1f;
                }
                else
                {
                    x = (index % 25) * 1.2f - 15f;
                    y = 1f + (index / 25) * 1.1f;
                    z = 0f;
                }

                // Sleeping is disabled so every body is solved every step; a pile that falls asleep measures nothing.
                world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(
                    AuraBodyType.Dynamic,
                    new AuraPose(new AuraVector3(x, y, z), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                    new[] { AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f)) },
                    allowSleeping: false));
            }

            uint tick = 0;
            for (var step = 0; step < WarmupSteps; step++)
                world.Step(new SimulationStep(new SimulationTick(++tick), Dt));

            var times = new List<double>(MeasuredSteps);
            var stopwatch = new Stopwatch();
            for (var step = 0; step < MeasuredSteps; step++)
            {
                stopwatch.Restart();
                world.Step(new SimulationStep(new SimulationTick(++tick), Dt));
                stopwatch.Stop();
                times.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            return times;
        }
    }
}
