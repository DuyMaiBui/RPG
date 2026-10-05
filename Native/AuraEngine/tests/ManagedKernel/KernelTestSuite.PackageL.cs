using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package L: buoyancy checked against Archimedes' principle on unit boxes (a 1 m cube in 3D, a 1 m
       square per metre of depth in 2D), where the AABB-based submerged fraction is exact. Water density is 1000. */
    public sealed partial class KernelTestSuite
    {
        private const float WaterSurface = 10f;
        private const float WaterDrag = 3f;

        private static IEnumerable<(string Name, Action Body)> PackageLTests()
        {
            return new (string Name, Action Body)[]
            {
                ("l_water_light_box_floats_with_draft_equal_to_density_ratio_3d", () => L_DraftEqualsDensityRatio(AuraPhysicsMode.Full3D)),
                ("l_water_light_box_floats_with_draft_equal_to_density_ratio_2d", () => L_DraftEqualsDensityRatio(AuraPhysicsMode.Plane2D)),
                ("l_water_neutral_box_stays_where_it_was_placed_3d", () => L_NeutralBoxStaysPut(AuraPhysicsMode.Full3D)),
                ("l_water_neutral_box_stays_where_it_was_placed_2d", () => L_NeutralBoxStaysPut(AuraPhysicsMode.Plane2D)),
                ("l_water_heavy_box_sinks_at_terminal_velocity_3d", () => L_HeavyBoxTerminalVelocity(AuraPhysicsMode.Full3D)),
                ("l_water_heavy_box_sinks_at_terminal_velocity_2d", () => L_HeavyBoxTerminalVelocity(AuraPhysicsMode.Plane2D)),
                ("l_water_dropped_box_settles_without_bouncing_3d", () => L_DroppedBoxSettles(AuraPhysicsMode.Full3D)),
                ("l_water_dropped_box_settles_without_bouncing_2d", () => L_DroppedBoxSettles(AuraPhysicsMode.Plane2D)),
                ("l_water_without_drag_keeps_oscillating_3d", () => L_WithoutDragKeepsOscillating(AuraPhysicsMode.Full3D)),
            };
        }

        private static (AuraSimulationWorld World, AuraWaterId Water) L_Pool(AuraPhysicsMode mode, float drag, bool withFloor)
        {
            var world = NewWorld(mode);
            if (withFloor)
                world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(20f, 0.5f, 20f))));
            var water = world.Physics.CreateWater(new AuraWaterDefinition(WaterSurface, AuraVector3.UnitY, 1000f, drag));
            Check(water.IsValid, "water creation failed.");
            return (world, water);
        }

        /* One tick exactly as AuraSimulationInstance does it: water first, then the world step. */
        private static void L_Tick(AuraSimulationWorld world, AuraWaterId water, uint tick)
        {
            Ok(world.Physics.ApplyWaterStep(water, Dt), "ApplyWaterStep");
            world.Step(new SimulationStep(new SimulationTick(tick), Dt));
        }

        private static PhysicsBodyId L_Box(AuraSimulationWorld world, float centerY, float density) =>
            Dyn(world, V(0f, centerY, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), mass: density);

        /* Equilibrium: submerged height d = H * (rho_body / rho_water), so the box centre sits at surface - d + H/2. */
        private static void L_DraftEqualsDensityRatio(AuraPhysicsMode mode)
        {
            foreach (var density in new[] { 250f, 500f, 750f })
            {
                var (world, water) = L_Pool(mode, WaterDrag, withFloor: true);
                using (world)
                {
                    var box = L_Box(world, WaterSurface + 3f, density);
                    for (uint tick = 1; tick <= 1200; tick++)
                        L_Tick(world, water, tick);
                    var state = StateOf(world, box);
                    var expectedCenter = WaterSurface - density / 1000f + 0.5f;
                    Near(state.Pose.Position.Y, expectedCenter, 0.03f, $"density {density}: floating centre height");
                    Near(state.LinearVelocity.Y, 0f, 0.05f, $"density {density}: vertical speed at rest");
                }
            }
        }

        /* Neutral buoyancy (body density == water density): no net force at any depth. */
        private static void L_NeutralBoxStaysPut(AuraPhysicsMode mode)
        {
            var (world, water) = L_Pool(mode, WaterDrag, withFloor: true);
            using (world)
            {
                var box = L_Box(world, WaterSurface - 4f, 1000f);
                for (uint tick = 1; tick <= 300; tick++)
                    L_Tick(world, water, tick);

                Near(StateOf(world, box).Pose.Position.Y, WaterSurface - 4f, 0.05f, "a neutrally buoyant box drifted");
            }
        }

        /* Fully submerged and denser than water: a = g (1 - rho_w/rho_b). The scheme is drag, buoyancy impulse, then the
           world step (gravity), whose fixed point is v_t = a (1 + c dt) / c: the continuous a / c plus an O(dt) bias that
           vanishes for a neutrally buoyant body (a = 0). */
        private static void L_HeavyBoxTerminalVelocity(AuraPhysicsMode mode)
        {
            var (world, water) = L_Pool(mode, WaterDrag, withFloor: true);
            using (world)
            {
                var box = L_Box(world, WaterSurface - 1.5f, 2000f);
                var netAcceleration = 9.81f * (1f - 1000f / 2000f);
                var expected = netAcceleration * (1f + WaterDrag * Dt) / WaterDrag;
                var peak = 0f;
                for (uint tick = 1; tick <= 180; tick++)
                {
                    L_Tick(world, water, tick);
                    if (tick > 120)
                        peak = Math.Max(peak, -StateOf(world, box).LinearVelocity.Y);
                }

                Near(peak, expected, expected * 0.05f, "terminal sinking speed");
                for (uint tick = 181; tick <= 900; tick++)
                    L_Tick(world, water, tick);
                Near(StateOf(world, box).Pose.Position.Y, 0.5f, 0.05f, "a sunk box must rest on the floor");
            }
        }

        /* A light box dropped from above: with drag the rebound must shrink geometrically and the box must come to rest,
           not bounce like a spring (the failure this guards: undamped buoyancy returned the body to its drop height). */
        private static void L_DroppedBoxSettles(AuraPhysicsMode mode)
        {
            var (world, water) = L_Pool(mode, WaterDrag, withFloor: true);
            using (world)
            {
                var box = L_Box(world, WaterSurface + 4f, 500f);
                var equilibrium = WaterSurface; // draft 0.5 => centre at the surface
                var entered = false;
                var apexes = new List<float>();
                var previousY = StateOf(world, box).Pose.Position.Y;
                var rising = false;
                for (uint tick = 1; tick <= 900; tick++)
                {
                    L_Tick(world, water, tick);
                    var y = StateOf(world, box).Pose.Position.Y;
                    if (y < WaterSurface + 0.5f)
                        entered = true;
                    if (entered)
                    {
                        if (rising && y < previousY)
                            apexes.Add(previousY - equilibrium);
                        rising = y > previousY;
                    }

                    previousY = y;
                }

                var final = StateOf(world, box);
                Near(final.Pose.Position.Y, equilibrium, 0.03f, "box must come to rest at its floating height");
                Near(final.LinearVelocity.Y, 0f, 0.05f, "box must be at rest");
                Check(apexes.Count >= 1, "the box never rebounded; the scenario did not exercise the water.");
                Check(apexes[0] < 1.5f, $"first rebound apex {apexes[0]:F2} m above equilibrium: water behaves like a trampoline.");
                for (var index = 1; index < apexes.Count; index++)
                    Check(apexes[index] < apexes[index - 1] * 0.6f + 0.01f, $"rebound {index} ({apexes[index]:F3}) did not decay from {apexes[index - 1]:F3}.");
            }
        }

        /* Control: without drag the same box keeps bouncing, proving the damping in the test above comes from the drag. */
        private static void L_WithoutDragKeepsOscillating(AuraPhysicsMode mode)
        {
            var (world, water) = L_Pool(mode, 0f, withFloor: true);
            using (world)
            {
                var box = L_Box(world, WaterSurface + 4f, 500f);
                var lateAmplitude = 0f;
                for (uint tick = 1; tick <= 900; tick++)
                {
                    L_Tick(world, water, tick);
                    if (tick > 600)
                        lateAmplitude = Math.Max(lateAmplitude, Math.Abs(StateOf(world, box).Pose.Position.Y - WaterSurface));
                }

                Check(lateAmplitude > 0.3f, $"without drag the box should still oscillate after 10 s (amplitude {lateAmplitude:F3}).");
            }
        }
    }
}
