using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package G: determinism guards for the ABI 10-12 features. Every scenario runs in two fresh
       worlds and must produce identical per-step state hashes, identical bit-exact body poses and velocities, and
       identical character and joint feedback. A second pass issues extra read-only queries between steps and must
       land on the same final state. Set AURA_DET_DUMP=1 to print each scenario's final hash (compare two processes). */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageGTests()
        {
            var cases = new List<(string Name, Action Body)>();
            foreach (var scenario in DetScenarios())
            {
                var captured = scenario;
                cases.Add(("g_det_repeat_" + captured.Name, () => DetRepeat(captured)));
                cases.Add(("g_det_chunking_" + captured.Name, () => DetChunking(captured)));
                if (!captured.ExpectsBreak)
                    cases.Add(("g_det_restore_inworld_" + captured.Name, () => DetRestoreInWorld(captured)));
            }

            cases.Add(("g_det_restore_config_contract_bodies_3d", () => DetConfigContract(AuraPhysicsMode.Full3D, false)));
            cases.Add(("g_det_restore_config_contract_bodies_2d", () => DetConfigContract(AuraPhysicsMode.Plane2D, false)));
            cases.Add(("g_det_restore_config_contract_hinge_3d", () => DetConfigContract(AuraPhysicsMode.Full3D, true)));
            cases.Add(("g_det_restore_config_contract_hinge_2d", () => DetConfigContract(AuraPhysicsMode.Plane2D, true)));
            return cases;
        }

        private sealed class DetCtx
        {
            public readonly List<AuraCharacterId> Characters = new List<AuraCharacterId>();
            public readonly List<AuraJointId> Joints = new List<AuraJointId>();
            public AuraPhysicsMode Mode;
        }

        private sealed class DetScenario
        {
            public string Name;
            public AuraPhysicsMode Mode;
            public int Steps;
            public bool ExpectsBreak;
            /* Last step whose script changes configuration; in-world restore replays only after it. */
            public int LastConfigStep = -1;
            /* Largest body position error tolerated after RestoreState and a replay (see DetRestoreInWorld). */
            public float RestoreTolerance = 0.05f;
            /* Builds the world content and returns the per-step command callback (a pure function of the step index). */
            public Func<AuraSimulationWorld, DetCtx, Action<int>> Setup;
        }

        private sealed class DetTrace
        {
            public readonly List<ulong> StepHashes = new List<ulong>();
            public readonly List<ulong> StepExtras = new List<ulong>();
            public int[] BreakStep = new int[0];
            public ulong FinalHash;
            public ulong FinalExtra;
            public int[] FinalBits = new int[0];
        }

        private sealed class DetRunner : IDisposable
        {
            private readonly AuraSimulationWorld _world;
            private readonly DetCtx _ctx = new DetCtx();
            private readonly Action<int> _before;
            private readonly AuraBodyState[] _buffer = new AuraBodyState[512];
            public readonly DetTrace Trace = new DetTrace();

            public DetRunner(DetScenario scenario)
            {
                _world = NewWorld(scenario.Mode);
                _ctx.Mode = scenario.Mode;
                _before = scenario.Setup(_world, _ctx);
                Trace.BreakStep = new int[_ctx.Joints.Count];
                for (var index = 0; index < Trace.BreakStep.Length; index++)
                    Trace.BreakStep[index] = -1;
            }

            public AuraSimulationWorld World => _world;

            public void Advance(int from, int to, bool perStep, bool noisy)
            {
                for (var step = from; step < to; step++)
                {
                    if (noisy)
                        Noise();
                    _before?.Invoke(step);
                    if (noisy)
                        Noise();
                    _world.Step(new SimulationStep(new SimulationTick((uint)step + 1u), Dt));
                    if (noisy)
                        Noise();
                    if (perStep)
                    {
                        Trace.StepHashes.Add(_world.ComputeStateHash());
                        Trace.StepExtras.Add(Extra(step));
                    }
                }
            }

            public void CaptureFinal()
            {
                Trace.FinalHash = _world.ComputeStateHash();
                Trace.FinalExtra = Extra(-1);
                var count = _world.CopyBodyStates(_buffer);
                Array.Sort(_buffer, 0, count, DetBodyOrder.Instance);
                var bits = new List<int>();
                for (var index = 0; index < count; index++)
                {
                    var s = _buffer[index];
                    AddBits(bits, s.Pose.Position);
                    bits.Add(F(s.Pose.Rotation.X)); bits.Add(F(s.Pose.Rotation.Y)); bits.Add(F(s.Pose.Rotation.Z)); bits.Add(F(s.Pose.Rotation.W));
                    AddBits(bits, s.LinearVelocity);
                    AddBits(bits, s.AngularVelocity);
                }

                Trace.FinalBits = bits.ToArray();
            }

            /* Character state and joint feedback folded into one value so per-step divergence has a step number. */
            private ulong Extra(int step)
            {
                ulong hash = 1469598103934665603UL;
                for (var index = 0; index < _ctx.Characters.Count; index++)
                {
                    if (!_world.TryGetCharacterState(_ctx.Characters[index], out var c))
                        continue;
                    var bits = new List<int>();
                    AddBits(bits, c.Position);
                    AddBits(bits, c.Velocity);
                    bits.Add(c.IsGrounded ? 1 : 0);
                    foreach (var b in bits)
                        hash = (hash ^ (uint)b) * 1099511628211UL;
                }

                for (var index = 0; index < _ctx.Joints.Count; index++)
                {
                    var joint = _ctx.Joints[index];
                    if (_world.JointControl.IsBroken(joint, out var broken) == AuraResult.Success)
                    {
                        hash = (hash ^ (broken ? 7UL : 3UL)) * 1099511628211UL;
                        if (broken && step >= 0 && Trace.BreakStep[index] < 0)
                            Trace.BreakStep[index] = step;
                    }

                    if (_world.JointControl.GetFeedback(joint, out var feedback) == AuraResult.Success)
                    {
                        hash = (hash ^ (uint)F(feedback.Force)) * 1099511628211UL;
                        hash = (hash ^ (uint)F(feedback.Position)) * 1099511628211UL;
                    }
                }

                return hash;
            }

            /* Read-only traffic a game issues between steps; it must never change simulation results. */
            private void Noise()
            {
                _world.ComputeStateHash();
                var count = _world.CopyBodyStates(_buffer);
                for (var index = 0; index < count; index++)
                    _world.Physics.GetBodyState(_buffer[index].Body, out _);
                _world.Raycast(new AuraRay(V(0f, 20f, 0f), V(0f, -1f, 0f)), 100f,
                    new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All), out _);
                foreach (var character in _ctx.Characters)
                    _world.TryGetCharacterState(character, out _);
                foreach (var joint in _ctx.Joints)
                {
                    _world.JointControl.IsBroken(joint, out _);
                    _world.JointControl.GetFeedback(joint, out _);
                    _world.HasJoint(joint);
                }
            }

            void IDisposable.Dispose() => ((IDisposable)_world).Dispose();
        }

        private sealed class DetBodyOrder : IComparer<AuraBodyState>
        {
            public static readonly DetBodyOrder Instance = new DetBodyOrder();

            int IComparer<AuraBodyState>.Compare(AuraBodyState a, AuraBodyState b) =>
                a.Body.Index != b.Body.Index ? a.Body.Index.CompareTo(b.Body.Index) : a.Body.Generation.CompareTo(b.Body.Generation);
        }

        private static int F(float value) => BitConverter.SingleToInt32Bits(value);

        private static void AddBits(List<int> bits, AuraVector3 v)
        {
            bits.Add(F(v.X)); bits.Add(F(v.Y)); bits.Add(F(v.Z));
        }

        private static DetTrace DetRun(DetScenario scenario, bool perStep, bool noisy)
        {
            using (var runner = new DetRunner(scenario))
            {
                runner.Advance(0, scenario.Steps, perStep, noisy);
                runner.CaptureFinal();
                return runner.Trace;
            }
        }

        private static void DetCompare(string label, DetTrace a, DetTrace b, bool compareSteps, bool compareBreaks)
        {
            if (compareSteps)
            {
                Check(a.StepHashes.Count == b.StepHashes.Count, label + ": step count differs.");
                for (var index = 0; index < a.StepHashes.Count; index++)
                {
                    Check(a.StepHashes[index] == b.StepHashes[index], $"{label}: body state hash diverged at step {index + 1}.");
                    Check(a.StepExtras[index] == b.StepExtras[index], $"{label}: character/joint state diverged at step {index + 1}.");
                }
            }

            if (compareBreaks)
            {
                Check(a.BreakStep.Length == b.BreakStep.Length, label + ": joint count differs.");
                for (var index = 0; index < a.BreakStep.Length; index++)
                    Check(a.BreakStep[index] == b.BreakStep[index], $"{label}: joint {index} broke at step {a.BreakStep[index]} vs {b.BreakStep[index]}.");
            }

            Check(a.FinalHash == b.FinalHash, label + ": final state hash differs.");
            Check(a.FinalExtra == b.FinalExtra, label + ": final character/joint state differs.");
            Check(a.FinalBits.Length == b.FinalBits.Length, label + ": body count differs.");
            for (var index = 0; index < a.FinalBits.Length; index++)
                Check(a.FinalBits[index] == b.FinalBits[index], $"{label}: final pose/velocity bits differ at word {index} ({a.FinalBits[index]:X8} vs {b.FinalBits[index]:X8}).");
        }

        private static void DetRepeat(DetScenario scenario)
        {
            var first = DetRun(scenario, true, false);
            var second = DetRun(scenario, true, false);
            DetCompare(scenario.Name, first, second, true, true);
            if (scenario.ExpectsBreak)
            {
                var broke = false;
                foreach (var step in first.BreakStep)
                    broke |= step >= 0;
                Check(broke, scenario.Name + ": the scenario was meant to break a joint but none broke.");
            }

            if (Environment.GetEnvironmentVariable("AURA_DET_DUMP") == "1")
                Console.WriteLine($"DET {scenario.Name} {first.FinalHash:X16} {first.FinalExtra:X16}");
        }

        /* Extra read-only queries around every step must not change the outcome (chunking independence). */
        private static void DetChunking(DetScenario scenario)
        {
            var plain = DetRun(scenario, false, false);
            var noisy = DetRun(scenario, true, true);
            DetCompare(scenario.Name + " (plain vs queried)", plain, noisy, false, false);
        }

        private static float[] DetPositions(AuraSimulationWorld world)
        {
            var states = new AuraBodyState[512];
            var count = world.CopyBodyStates(states);
            Array.Sort(states, 0, count, DetBodyOrder.Instance);
            var values = new float[count * 3];
            for (var index = 0; index < count; index++)
            {
                values[index * 3] = states[index].Pose.Position.X;
                values[index * 3 + 1] = states[index].Pose.Position.Y;
                values[index * 3 + 2] = states[index].Pose.Position.Z;
            }

            return values;
        }

        /* Save, continue, restore into the same world and continue again. The snapshot is the raw body-state stream, so
           contact and joint warm-start caches, sleep timers and the awake flag are not restored, and Box2D rebuilds the
           rotation with b2MakeRot (an approximation, ~1e-4 rad). The continuation is therefore compared with a positional
           tolerance (DetScenario.RestoreTolerance), not bit for bit. AURA_DET_STRICT_RESTORE=1 demands equal state hashes
           on every replayed step and is expected to fail until the kernel snapshot captures those caches. */
        private static void DetRestoreInWorld(DetScenario scenario)
        {
            var split = Math.Max(scenario.Steps / 3, scenario.LastConfigStep + 1);
            var end = Math.Min(scenario.Steps, split + 60);
            Check(end > split, scenario.Name + ": no replay window after the last configuration step.");
            var strict = Environment.GetEnvironmentVariable("AURA_DET_STRICT_RESTORE") == "1";
            using (var runner = new DetRunner(scenario))
            {
                runner.Advance(0, split, false, false);
                var saved = runner.World.SaveState();
                runner.Advance(split, end, true, false);
                var firstHashes = new List<ulong>(runner.Trace.StepHashes);
                var firstExtras = new List<ulong>(runner.Trace.StepExtras);
                var firstPositions = DetPositions(runner.World);
                runner.Trace.StepHashes.Clear();
                runner.Trace.StepExtras.Clear();

                runner.World.RestoreState(saved);
                runner.Advance(split, end, true, false);
                if (strict)
                {
                    for (var index = 0; index < firstHashes.Count; index++)
                    {
                        Check(firstHashes[index] == runner.Trace.StepHashes[index],
                            $"{scenario.Name}: continuation after RestoreState diverged at step {split + index + 1}.");
                        Check(firstExtras[index] == runner.Trace.StepExtras[index],
                            $"{scenario.Name}: character/joint continuation after RestoreState diverged at step {split + index + 1}.");
                    }

                    return;
                }

                var worst = DetMaxDelta(firstPositions, DetPositions(runner.World));
                if (Environment.GetEnvironmentVariable("AURA_DET_DUMP") == "1")
                    Console.WriteLine($"DET restore {scenario.Name} max position delta {worst:R}");
                Check(worst <= scenario.RestoreTolerance, $"{scenario.Name}: continuation after RestoreState drifted by {worst} (tolerance {scenario.RestoreTolerance}).");
            }
        }

        private static SimulationEntityId DetEntity(AuraSimulationWorld world, PhysicsBodyId body)
        {
            var states = new AuraBodyState[512];
            var count = world.CopyBodyStates(states);
            for (var index = 0; index < count; index++)
                if (states[index].Body.Index == body.Index && states[index].Body.Generation == body.Generation)
                    return states[index].Entity;
            throw new Exception("body has no entity.");
        }

        private static AuraJointId DetJoint(AuraSimulationWorld world, DetCtx ctx, PhysicsBodyId a, PhysicsBodyId b, in AuraJointDefinition definition)
        {
            var joint = world.CreateJoint(DetEntity(world, a), DetEntity(world, b), definition);
            Check(joint.IsValid, $"{definition.Type} joint creation failed.");
            ctx.Joints.Add(joint);
            return joint;
        }

        private static AuraJointId DetJoint2D(AuraSimulationWorld world, DetCtx ctx, in AuraJointDefinition definition)
        {
            var joint = world.Physics.Joints.CreateJoint(definition);
            Check(joint.IsValid, $"{definition.Type} 2D joint creation failed.");
            ctx.Joints.Add(joint);
            return joint;
        }

        private static AuraQuaternion DetAngleZ(float angle) => new AuraQuaternion(0f, 0f, MathF.Sin(angle * 0.5f), MathF.Cos(angle * 0.5f));

        private static DetScenario DetCase(string name, AuraPhysicsMode mode, int steps, Func<AuraSimulationWorld, DetCtx, Action<int>> setup,
            bool expectsBreak = false, int lastConfigStep = -1, float restoreTolerance = 0.05f) =>
            new DetScenario { Name = name, Mode = mode, Steps = steps, Setup = setup, ExpectsBreak = expectsBreak, LastConfigStep = lastConfigStep, RestoreTolerance = restoreTolerance };

        private static string DetSuffix(AuraPhysicsMode mode) => mode == AuraPhysicsMode.Full3D ? "3d" : "2d";

        private static IEnumerable<DetScenario> DetScenarios()
        {
            foreach (var mode in new[] { AuraPhysicsMode.Full3D, AuraPhysicsMode.Plane2D })
            {
                var m = mode;
                var suffix = DetSuffix(m);
                yield return DetCase("pile_" + suffix, m, 240, DetPile);
                yield return DetCase("free_flight_" + suffix, m, 240, DetFreeFlight, false, 150, m == AuraPhysicsMode.Full3D ? 1e-4f : 5e-3f);
                yield return DetCase("fields_" + suffix, m, 220, DetFields, false, 170);
                yield return DetCase("body_control_" + suffix, m, 180, DetBodyControl, false, 130);
                yield return DetCase("kinematic_targets_" + suffix, m, 240, DetKinematic, false, 150, m == AuraPhysicsMode.Full3D ? 0.05f : 0.15f);
                yield return DetCase("ccd_" + suffix, m, 60, DetCcd, false, 20);
                yield return DetCase("joint_hinge_motor_limits_" + suffix, m, 200, DetHingeMotorLimits, false, 150);
                yield return DetCase("joint_hinge_break_" + suffix, m, 120, DetHingeBreak, true);
                yield return DetCase("joint_fixed_break_" + suffix, m, 90, DetFixedBreak, true);
                yield return DetCase("joint_slider_motor_limits_" + suffix, m, 240, DetSliderMotorLimits, false, 150);
            }

            yield return DetCase("joint_hinge_position_motor_3d", AuraPhysicsMode.Full3D, 200, DetHingePositionMotor, false, 100);
            yield return DetCase("joint_sixdof_break_3d", AuraPhysicsMode.Full3D, 90, DetSixDofBreak, true);
            yield return DetCase("joint_sixdof_3d", AuraPhysicsMode.Full3D, 240, DetSixDof, false, 160);
            yield return DetCase("joint_cone_3d", AuraPhysicsMode.Full3D, 180, DetCone, false, 90);
            yield return DetCase("joint_swingtwist_3d", AuraPhysicsMode.Full3D, 300, DetSwingTwist, false, 160);
            yield return DetCase("joint_pulley_3d", AuraPhysicsMode.Full3D, 180, DetPulley, false, 100);
            yield return DetCase("joint_gear_3d", AuraPhysicsMode.Full3D, 180, DetGear, false, 100);
            yield return DetCase("joint_rack_and_pinion_3d", AuraPhysicsMode.Full3D, 180, DetRack, false, 100);
            yield return DetCase("joint_wheel_car_2d", AuraPhysicsMode.Plane2D, 240, DetWheelCar, false, 200);
            yield return DetCase("joint_mouse_2d", AuraPhysicsMode.Plane2D, 300, DetMouse, false, 160);
            yield return DetCase("joint_rope_2d", AuraPhysicsMode.Plane2D, 240, DetRope, false, 20);
            yield return DetCase("char2d_walk_jump_ledge", AuraPhysicsMode.Plane2D, 320, DetChar2DWalkJump);
            yield return DetCase("char2d_slope", AuraPhysicsMode.Plane2D, 260, DetChar2DSlope);
            yield return DetCase("char2d_oneway", AuraPhysicsMode.Plane2D, 260, DetChar2DOneWay);
            yield return DetCase("char2d_moving_platform", AuraPhysicsMode.Plane2D, 300, DetChar2DPlatform, false, 180);
            yield return DetCase("char3d_walk_jump_push", AuraPhysicsMode.Full3D, 360, DetChar3D);
            yield return DetCase("char3d_slope_platform", AuraPhysicsMode.Full3D, 300, DetChar3DSlopePlatform, false, 200);
        }

        // ---- shared body scenes ---------------------------------------------------------------

        private static Action<int> DetPile(AuraSimulationWorld world, DetCtx ctx)
        {
            Ground(world);
            for (var index = 0; index < 24; index++)
            {
                var x = (index % 6) * 1.1f - 3f;
                var y = 1f + (index / 6) * 1.3f + (index % 3) * 0.05f;
                var shape = index % 2 == 0 ? AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)) : AuraPhysicsShapeDefinition.Sphere(0.45f);
                Dyn(world, V(x, y, 0f), shape, mass: 1f + index % 4);
            }

            return null;
        }

        /* Bodies in empty space under fields, impulses, enable toggles and kinematic targets: no contacts, no joints. */
        private static Action<int> DetFreeFlight(AuraSimulationWorld world, DetCtx ctx)
        {
            Ok(world.ForceFields.SetGravity(V(0f, -3f, 0f)), "gravity");
            var bodies = new List<PhysicsBodyId>();
            for (var index = 0; index < 6; index++)
            {
                var shape = index % 2 == 0 ? AuraPhysicsShapeDefinition.Sphere(0.4f) : AuraPhysicsShapeDefinition.Box(V(0.4f, 0.4f, 0.4f));
                bodies.Add(Dyn(world, V(index * 4f - 10f, 10f + index, 0f), shape, gravityScale: 0.5f + index * 0.3f, mass: 1f + index % 3));
            }

            var kinematic = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(V(0f, 30f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f))));
            var kinematicEntity = DetEntity(world, kinematic);
            Field(world, SphereField(AuraForceFieldKind.Radial, V(0f, 12f, 0f), 30f, default, 25f, AuraForceFieldMode.Acceleration, AuraForceFieldFalloff.InverseSquare, 1f));
            Field(world, BoxField(AuraForceFieldKind.Drag, V(0f, 12f, 0f), V(40f, 40f, 40f), V(1f, 0f, 0f), 0.3f));
            Field(world, BoxField(AuraForceFieldKind.Directional, V(-8f, 12f, 0f), V(5f, 20f, 5f), V(0f, 6f, 0f), 4f, AuraForceFieldMode.Force));
            return step =>
            {
                world.SetKinematicTarget(kinematicEntity, new AuraPose(V(MathF.Sin(step * Dt) * 5f, 30f - step * 0.02f, 0f), DetAngleZ(step * 0.01f)));
                if (step == 5) Ok(world.BodyControl.AddImpulse(bodies[0], V(3f, 2f, 0f)), "impulse");
                if (step == 10) Ok(world.BodyControl.SetAngularVelocity(bodies[1], V(0f, 0f, 2f)), "spin");
                if (step == 30) Ok(world.BodyControl.AddForce(bodies[2], V(10f, 0f, 0f)), "force");
                if (step == 50) Ok(world.BodyControl.SetEnabled(bodies[3], false), "disable");
                if (step == 80) Ok(world.BodyControl.SetEnabled(bodies[3], true), "enable");
                if (step == 100) Ok(world.BodyControl.SetPose(bodies[4], new AuraPose(V(2f, 14f, 0f), DetAngleZ(0.5f)), false), "teleport");
                if (step == 120) Ok(world.ForceFields.SetGravity(V(1f, 4f, 0f)), "gravity change");
                if (step == 150) Ok(world.ForceFields.SetGravity(V(0f, -3f, 0f)), "gravity back");
            };
        }

        private static Action<int> DetFields(AuraSimulationWorld world, DetCtx ctx)
        {
            Ground(world);
            for (var index = 0; index < 8; index++)
            {
                var shape = index % 2 == 0 ? AuraPhysicsShapeDefinition.Sphere(0.4f) : AuraPhysicsShapeDefinition.Box(V(0.4f, 0.4f, 0.4f));
                Dyn(world, V(index * 2f - 8f, 1f + index * 0.4f, 0f), shape, gravityScale: 1f + index * 0.1f, mass: 1f + index % 3);
            }

            var forces = world.ForceFields;
            Field(world, BoxField(AuraForceFieldKind.Directional, V(-6f, 3f, 0f), V(4f, 4f, 4f), V(0f, 14f, 0f), 0f));
            var radial = Field(world, SphereField(AuraForceFieldKind.Radial, V(4f, 2f, 0f), 10f, default, 40f,
                AuraForceFieldMode.Acceleration, AuraForceFieldFalloff.InverseSquare, 1f));
            var drag = Field(world, BoxField(AuraForceFieldKind.Drag, V(0f, 3f, 0f), V(30f, 10f, 10f), V(2f, 0f, 0f), 0.8f));
            Field(world, BoxField(AuraForceFieldKind.Directional, V(0f, 2f, 0f), V(30f, 6f, 6f), V(0f, 5f, 0f), 12f, AuraForceFieldMode.Force));
            return step =>
            {
                if (step == 20) Ok(forces.SetGravity(V(0f, 9.81f, 0f)), "gravity up");
                if (step == 60) Ok(forces.SetGravity(V(3f, -9.81f, 0f)), "gravity side");
                if (step == 100) Ok(forces.UpdateField(radial, SphereField(AuraForceFieldKind.Radial, V(4f, 2f, 0f), 10f, default, -40f,
                    AuraForceFieldMode.Acceleration, AuraForceFieldFalloff.Linear)), "update radial");
                if (step == 140) Ok(forces.DestroyField(drag), "destroy drag");
                if (step == 170) Ok(forces.SetGravity(V(0f, -9.81f, 0f)), "gravity normal");
            };
        }

        private static Action<int> DetBodyControl(AuraSimulationWorld world, DetCtx ctx)
        {
            Ground(world);
            var a = Dyn(world, V(0f, 3f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var b = Dyn(world, V(2f, 1.5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)));
            var c = Dyn(world, V(-2f, 6f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), layer: 1);
            var d = Dyn(world, V(4f, 1.5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)));
            var e = Dyn(world, V(-4f, 2f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var control = world.BodyControl;
            return step =>
            {
                if (step == 3)
                {
                    Ok(control.AddImpulse(a, V(4f, 6f, 0f)), "impulse");
                    Ok(control.SetLinearVelocity(b, V(-2f, 0f, 0f)), "velocity");
                    Ok(control.AddTorque(d, V(0f, 0f, 5f)), "torque");
                }

                if (step >= 10 && step < 20)
                    Ok(control.AddForce(c, V(30f, 0f, 0f)), "force");
                if (step == 15)
                {
                    Ok(control.SetPose(c, new AuraPose(V(-2f, 8f, 0f), DetAngleZ(0.3f)), false), "teleport keep velocity");
                    Ok(control.SetAngularVelocity(e, V(0f, 0f, 3f)), "angular velocity");
                }

                if (step == 25)
                {
                    Ok(control.SetGravityScale(a, 0.3f), "gravity scale a");
                    Ok(control.SetGravityScale(b, 2f), "gravity scale b");
                }

                if (step == 40)
                {
                    Ok(control.SetBodyType(d, AuraBodyType.Kinematic), "to kinematic");
                    Ok(control.SetLinearVelocity(d, V(1f, 0f, 0f)), "kinematic velocity");
                }

                if (step == 50) Ok(control.SetEnabled(e, false), "disable");
                if (step == 60) Ok(control.SetBodyType(d, AuraBodyType.Dynamic), "to dynamic");
                if (step == 70) Ok(control.SetEnabled(e, true), "enable");
                if (step == 80) Ok(control.SetPose(a, new AuraPose(V(0f, 5f, 0f), AuraQuaternion.Identity), true), "teleport zero velocity");
                if (step == 90) Ok(control.SetBodyType(b, AuraBodyType.Static), "to static");
                if (step == 100) Ok(control.SetLayer(c, new AuraPhysicsLayer(2), new AuraPhysicsLayerMask(~1UL)), "layer away");
                if (step == 110) Ok(control.SetBodyType(b, AuraBodyType.Dynamic), "static to dynamic");
                if (step == 120) Ok(control.SetLayer(c, new AuraPhysicsLayer(1), AuraPhysicsLayerMask.All), "layer back");
                if (step == 130) Ok(control.SetGravityScale(a, 1f), "gravity scale back");
            };
        }

        private static Action<int> DetKinematic(AuraSimulationWorld world, DetCtx ctx)
        {
            Ground(world, -3f);
            var platform = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(V(0f, 0.5f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(3f, 0.25f, 3f))));
            var driven = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(V(8f, 0.5f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(1f, 0.25f, 1f))));
            for (var index = 0; index < 4; index++)
            {
                Dyn(world, V(index * 1.2f - 1.8f, 1.5f + index * 0.1f, 0f), AuraPhysicsShapeDefinition.Box(V(0.4f, 0.4f, 0.4f)));
                Dyn(world, V(8f + index * 0.3f - 0.5f, 2f + index, 0f), AuraPhysicsShapeDefinition.Sphere(0.3f));
            }

            var platformEntity = DetEntity(world, platform);
            var control = world.BodyControl;
            Ok(control.SetLinearVelocity(driven, V(-1f, 0f, 0f)), "driven velocity");
            return step =>
            {
                /* Targets arrive at a variable rate: every step, then every third step (a slow frame runs several steps). */
                var due = step < 120 || step % 3 == 0;
                if (due)
                {
                    var t = step * Dt;
                    Ok(world.SetKinematicTarget(platformEntity, new AuraPose(V(MathF.Sin(t * 1.5f) * 3f, 0.5f + MathF.Sin(t * 2.1f) * 0.4f, 0f), DetAngleZ(MathF.Sin(t) * 0.2f))), "kinematic target");
                }

                if (step == 150) Ok(control.SetLinearVelocity(driven, V(1.5f, 0.5f, 0f)), "driven reverse");
            };
        }

        private static Action<int> DetCcd(AuraSimulationWorld world, DetCtx ctx)
        {
            Ok(world.ForceFields.SetGravity(V(0f, 0f, 0f)), "zero gravity");
            var mode = ctx.Mode;
            var thin = AuraPhysicsShapeDefinition.Box(V(0.05f, 10f, 10f));
            var wallPose = new AuraPose(V(10f, 0f, 0f), AuraQuaternion.Identity);
            if (mode == AuraPhysicsMode.Full3D)
                world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(wallPose, AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, thin));
            else
                world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(AuraBodyType.Dynamic, wallPose, AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All, new[] { thin }, mass: 1000f, gravityScale: 0f));
            for (var index = 0; index < 3; index++)
                world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(AuraBodyType.Dynamic,
                    new AuraPose(V(0f, index * 1.5f - 1.5f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                    new[] { AuraPhysicsShapeDefinition.Sphere(0.1f) }, gravityScale: 0f, initialLinearVelocity: V(200f + index * 30f, 0f, 0f),
                    collisionDetection: index == 1 ? AuraBodyCollisionDetection.Discrete : AuraBodyCollisionDetection.Continuous));
            var late = world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(AuraBodyType.Dynamic,
                new AuraPose(V(0f, 4f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new[] { AuraPhysicsShapeDefinition.Sphere(0.1f) }, gravityScale: 0f, initialLinearVelocity: V(0f, 0f, 0f)));
            return step =>
            {
                if (step == 2)
                {
                    Ok(world.BodyControl.SetLinearVelocity(late, V(250f, 0f, 0f)), "late velocity");
                    Ok(world.BodyControl.SetCollisionDetection(late, AuraBodyCollisionDetection.Continuous), "late CCD on");
                }

                if (step == 20) Ok(world.BodyControl.SetCollisionDetection(late, AuraBodyCollisionDetection.Discrete), "late CCD off");
            };
        }

        // ---- joints -----------------------------------------------------------------------------

        private static Action<int> DetHingeMotorLimits(AuraSimulationWorld world, DetCtx ctx)
        {
            var rig = HingeRig(world, 1f);
            ctx.Joints.Add(rig.Joint);
            var control = world.JointControl;
            return step =>
            {
                if (step == 5) Ok(control.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(2f, 60f)), "motor velocity");
                if (step == 40) Ok(control.SetLimits(rig.Joint, true, -0.5f, 0.5f), "limits on");
                if (step == 90) Ok(control.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(-3f, 1.0e5f)), "motor reverse");
                if (step == 130) Ok(control.SetMotor(rig.Joint, AuraJointMotorDefinition.Off), "motor off");
                if (step == 150) Ok(control.SetLimits(rig.Joint, false, 0f, 0f), "limits off");
            };
        }

        private static Action<int> DetHingeBreak(AuraSimulationWorld world, DetCtx ctx)
        {
            var rig = HingeRig(world, 1f);
            ctx.Joints.Add(rig.Joint);
            Ok(world.JointControl.SetBreakThreshold(rig.Joint, 14f, 0f), "hinge threshold");
            return step =>
            {
                if (step == 10) Ok(world.BodyControl.AddImpulse(rig.Arm, V(0f, -3f, 0f)), "kick");
            };
        }

        private static Action<int> DetFixedBreak(AuraSimulationWorld world, DetCtx ctx)
        {
            var mode = ctx.Mode;
            var rig = HangingRig(world, mode);
            ctx.Joints.Add(rig.Joint);
            return step =>
            {
                if (step == 30) Ok(world.JointControl.SetBreakThreshold(rig.Joint, rig.Newtons * 0.5f, 0f), "fixed threshold");
            };
        }

        private static Action<int> DetSixDofBreak(AuraSimulationWorld world, DetCtx ctx)
        {
            var anchor = DStatic(world, V(0f, 6f, 0f));
            var weight = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), mass: 10f);
            var joint = DetJoint(world, ctx, anchor, weight, AuraJointDefinition.CreateSixDof(anchor, weight, V(0f, 5.5f, 0f), V(0f, 5.5f, 0f), DLocked()));
            return step =>
            {
                if (step == 30) Ok(world.JointControl.SetBreakThreshold(joint, 49f, 0f), "sixdof threshold");
            };
        }

        private static Action<int> DetSliderMotorLimits(AuraSimulationWorld world, DetCtx ctx)
        {
            var rig = SliderRig(world);
            ctx.Joints.Add(rig.Joint);
            var control = world.JointControl;
            return step =>
            {
                if (step == 2) Ok(control.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(1f, 1.0e5f)), "slider motor");
                if (step == 60)
                {
                    Ok(control.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(-2f, 200f)), "slider reverse");
                    Ok(control.SetLimits(rig.Joint, true, -0.5f, 0.5f), "slider limits");
                }

                if (step == 150) Ok(control.SetLimits(rig.Joint, false, 0f, 0f), "slider limits off");
            };
        }

        private static Action<int> DetHingePositionMotor(AuraSimulationWorld world, DetCtx ctx)
        {
            var rig = HingeRig(world, 0f);
            ctx.Joints.Add(rig.Joint);
            return step =>
            {
                if (step == 2) Ok(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Position(0.5f, 1.0e5f, 4f, 1f)), "position motor");
                if (step == 100) Ok(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Position(-0.8f, 1.0e5f, 4f, 1f)), "position motor 2");
            };
        }

        private static Action<int> DetSixDof(AuraSimulationWorld world, DetCtx ctx)
        {
            var axes = DAxis(world);
            var anchor = DStatic(world, V(0f, 5f, 0f));
            var slide = DBox(world, V(0f, 5f, 0f));
            var slideJoint = DetJoint(world, ctx, anchor, slide, AuraJointDefinition.CreateSixDof(anchor, slide, V(0f, 5f, 0f), V(0f, 5f, 0f),
                DLimits(tx: AuraJointAxisLimit.Limited(-1f, 1f), ty: AuraJointAxisLimit.Free)));
            var arm = DBox(world, V(5f, 5f, 0f), gravityScale: 1f);
            var armAnchor = DStatic(world, V(4f, 5f, 0f));
            DetJoint(world, ctx, armAnchor, arm, AuraJointDefinition.CreateSixDof(armAnchor, arm, V(4f, 5f, 0f), V(4f, 5f, 0f),
                DLimits(rz: AuraJointAxisLimit.Limited(0f, 0.5f))));
            var spin = DBox(world, V(10f, 5f, 0f));
            var spinAnchor = DStatic(world, V(10f, 5f, 0f));
            var spinJoint = DetJoint(world, ctx, spinAnchor, spin, AuraJointDefinition.CreateSixDof(spinAnchor, spin, V(10f, 5f, 0f), V(10f, 5f, 0f),
                DLimits(rz: AuraJointAxisLimit.Free)));
            return step =>
            {
                if (step == 3) Ok(world.BodyControl.AddImpulse(slide, V(6f, 6f, 0f)), "impulse");
                if (step == 20) Ok(axes.SetAxisMotor(spinJoint, 5, AuraJointMotorDefinition.Velocity(1.5f, 1.0e5f)), "rotation motor");
                if (step == 50) Ok(axes.SetAxisLimits(slideJoint, 1, AuraJointAxisLimit.Limited(-0.5f, 0.5f)), "runtime limit");
                if (step == 80) Ok(axes.SetAxisMotor(slideJoint, 0, AuraJointMotorDefinition.Velocity(2f, 1.0e5f)), "translation motor");
                if (step == 110) Ok(axes.SetAxisMotor(slideJoint, 0, AuraJointMotorDefinition.Off), "translation motor off");
                if (step == 130) Ok(world.BodyControl.AddImpulse(slide, V(-4f, 4f, 0f)), "impulse 2");
                if (step == 160) Ok(axes.SetAxisMotor(spinJoint, 5, AuraJointMotorDefinition.Off), "rotation motor off");
            };
        }

        private static Action<int> DetCone(AuraSimulationWorld world, DetCtx ctx)
        {
            var anchor = DStatic(world, V(0f, 5f, 0f));
            var bob = DBox(world, V(0f, 4f, 0f), gravityScale: 1f);
            DetJoint(world, ctx, anchor, bob, AuraJointDefinition.CreateCone(anchor, bob, V(0f, 5f, 0f), V(0f, 5f, 0f),
                V(0f, -1f, 0f), V(0f, -1f, 0f), 0.4f));
            return step =>
            {
                if (step == 2) Ok(world.BodyControl.AddImpulse(bob, V(5f, 0f, 2f)), "impulse");
                if (step == 90) Ok(world.BodyControl.AddImpulse(bob, V(-4f, 0f, -3f)), "impulse 2");
            };
        }

        private static Action<int> DetSwingTwist(AuraSimulationWorld world, DetCtx ctx)
        {
            var rig = DSwingTwistRig(world, 1f, 0.5f, -0.3f, 0.4f, 0.2f);
            ctx.Joints.Add(rig.Joint);
            var axes = DAxis(world);
            return step =>
            {
                if (step == 5) Ok(world.BodyControl.SetAngularVelocity(rig.Arm, V(4f, 0f, 0f)), "twist spin");
                if (step == 60) Ok(axes.SetAxisMotor(rig.Joint, 0, AuraJointMotorDefinition.Velocity(1f, 1.0e5f)), "twist motor");
                if (step == 100) Ok(axes.SetAxisMotor(rig.Joint, 0, AuraJointMotorDefinition.Off), "twist motor off");
                if (step == 120) Ok(world.JointControl.SetLimits(rig.Joint, true, -0.1f, 0.1f), "twist limits");
                if (step == 140) Ok(axes.SetAxisLimits(rig.Joint, 1, AuraJointAxisLimit.Limited(0f, 0.2f)), "swing limit");
                if (step == 160) Ok(world.BodyControl.SetAngularVelocity(rig.Arm, V(-3f, 1f, 0f)), "spin back");
            };
        }

        private static Action<int> DetPulley(AuraSimulationWorld world, DetCtx ctx)
        {
            var f1 = V(-1f, 10f, 0f);
            var f2 = V(1f, 10f, 0f);
            var light = Dyn(world, V(-1f, 6f, 0f), AuraPhysicsShapeDefinition.Box(V(0.2f, 0.2f, 0.2f)), mass: 1f);
            var heavy = Dyn(world, V(1f, 4f, 0f), AuraPhysicsShapeDefinition.Box(V(0.2f, 0.2f, 0.2f)), mass: 4f);
            const float ratio = 2f;
            var length = 4f + ratio * 6f;
            var joint = DetJoint(world, ctx, light, heavy, AuraJointDefinition.CreatePulley(light, heavy, V(-1f, 6f, 0f), V(1f, 4f, 0f), f1, f2, ratio, length, length));
            return step =>
            {
                if (step == 90) Ok(world.JointControl.SetLimits(joint, true, 0f, length), "rope limits");
                if (step == 100) Ok(world.BodyControl.AddImpulse(light, V(0f, 6f, 0f)), "kick");
            };
        }

        private static Action<int> DetGear(AuraSimulationWorld world, DetCtx ctx)
        {
            var rig = DBuildGear(world, 2f);
            ctx.Joints.Add(rig.HingeA);
            ctx.Joints.Add(rig.HingeB);
            ctx.Joints.Add(rig.Gear);
            return step =>
            {
                if (step == 2) Ok(world.JointControl.SetMotor(rig.HingeA, AuraJointMotorDefinition.Velocity(2f, 1.0e5f)), "drive gear");
                if (step == 100) Ok(world.JointControl.SetMotor(rig.HingeA, AuraJointMotorDefinition.Velocity(-1f, 1.0e5f)), "reverse gear");
            };
        }

        private static Action<int> DetRack(AuraSimulationWorld world, DetCtx ctx)
        {
            var ground = DStatic(world, V(0f, 5f, -2f));
            var pinion = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.4f, 0.4f, 0.1f)), gravityScale: 0f);
            var rack = Dyn(world, V(0f, 4f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.1f, 0.1f)), gravityScale: 0f);
            var hinge = DetJoint(world, ctx, ground, pinion, AuraJointDefinition.CreateHinge(ground, pinion, V(0f, 5f, 0f), V(0f, 5f, 0f), AuraVector3.UnitZ, AuraVector3.UnitZ));
            var slider = DetJoint(world, ctx, ground, rack, AuraJointDefinition.CreateSlider(ground, rack, V(0f, 4f, 0f), V(0f, 4f, 0f), AuraVector3.UnitX, AuraVector3.UnitX));
            DetJoint(world, ctx, pinion, rack, AuraJointDefinition.CreateRackAndPinion(pinion, rack, AuraVector3.UnitZ, AuraVector3.UnitX, 2f, hinge, slider));
            return step =>
            {
                if (step == 2) Ok(world.JointControl.SetMotor(slider, AuraJointMotorDefinition.Velocity(1f, 1.0e5f)), "drive rack");
                if (step == 100) Ok(world.JointControl.SetMotor(slider, AuraJointMotorDefinition.Velocity(-1.5f, 1.0e5f)), "reverse rack");
            };
        }

        private static Action<int> DetWheelCar(AuraSimulationWorld world, DetCtx ctx)
        {
            B2Static(world, 0f, -0.5f, AuraPhysicsShapeDefinition.Box(V(40f, 0.5f, 0.5f)));
            var chassis = Dyn(world, V(0f, 1.4f, 0f), AuraPhysicsShapeDefinition.Box(V(1.2f, 0.2f, 0.5f)));
            var wheels = new List<AuraJointId>();
            foreach (var x in new[] { -0.9f, 0.9f })
            {
                var wheel = Dyn(world, V(x, 0.6f, 0f), AuraPhysicsShapeDefinition.Sphere(0.4f));
                wheels.Add(DetJoint2D(world, ctx, AuraJointDefinition.CreateWheel(chassis, wheel, V(x, 0.6f, 0f), V(x, 0.6f, 0f), V(0f, 1f, 0f),
                    springFrequency: 4f, springDamping: 0.7f, enableLimit: true, minLimit: -0.3f, maxLimit: 0.3f, motorEnabled: true, motorSpeed: -8f, maxMotorTorque: 5000f)));
            }

            return step =>
            {
                if (step == 120) foreach (var joint in wheels) Ok(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Velocity(5f, 5000f)), "wheel reverse");
                if (step == 200) foreach (var joint in wheels) Ok(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Off), "wheel off");
            };
        }

        private static Action<int> DetMouse(AuraSimulationWorld world, DetCtx ctx)
        {
            var body = Dyn(world, V(0f, 0f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), gravityScale: 0f);
            var anchor = B2Static(world, -10f, 0f, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.5f)));
            var joint = DetJoint2D(world, ctx, AuraJointDefinition.CreateMouse(anchor, body, V(0f, 0f, 0f), 5f, 0.7f, 1.0e5f));
            var free = Dyn(world, V(4f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var freeJoint = DetJoint2D(world, ctx, AuraJointDefinition.CreateMouse(PhysicsBodyId.Invalid, free, V(4f, 0f, 0f), 3f, 0.5f, 5000f));
            var target = (IPhysicsJointTarget)world.Physics;
            return step =>
            {
                if (step == 30) Ok(target.SetJointTarget(joint, V(3f, 2f, 0f)), "target 1");
                if (step == 100) Ok(target.SetJointTarget(joint, V(-2f, -1f, 0f)), "target 2");
                if (step == 60) Ok(target.SetJointTarget(freeJoint, V(6f, 3f, 0f)), "free target");
                if (step == 160) Ok(target.SetJointTarget(freeJoint, V(2f, -2f, 0f)), "free target 2");
            };
        }

        private static Action<int> DetRope(AuraSimulationWorld world, DetCtx ctx)
        {
            var anchor = B2Static(world, 0f, 5f, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.5f)));
            var weight = Dyn(world, V(0f, 4f, 0f), AuraPhysicsShapeDefinition.Box(V(0.2f, 0.2f, 0.5f)));
            DetJoint2D(world, ctx, AuraJointDefinition.CreateRope(anchor, weight, V(0f, 5f, 0f), V(0f, 4f, 0f), 2f));
            var tail = Dyn(world, V(1f, 4f, 0f), AuraPhysicsShapeDefinition.Sphere(0.2f));
            DetJoint2D(world, ctx, AuraJointDefinition.CreateRope(weight, tail, V(0f, 4f, 0f), V(1f, 4f, 0f), 1.2f));
            return step =>
            {
                if (step == 20) Ok(world.BodyControl.AddImpulse(weight, V(6f, 0f, 0f)), "swing");
            };
        }

        // ---- characters -------------------------------------------------------------------------

        private static Action<int> DetChar2DWalkJump(AuraSimulationWorld world, DetCtx ctx)
        {
            StaticBox2D(world, 0f, -0.5f, 30f, 0.5f);
            StaticBox2D(world, 6f, 0.125f, 3f, 0.125f);
            StaticBox2D(world, 12f, 3f, 0.5f, 3f);
            var id = NewCharacter(world, 0f, 1f, 45f, 0.3f);
            ctx.Characters.Add(id);
            return step =>
            {
                var vx = step < 180 ? 3f : -2.5f;
                var jump = step == 40 || step == 120 || step == 200 ? 8f : 0f;
                world.MoveCharacter(id, new AuraVector3(vx * Dt, jump * Dt, 0f), Dt);
            };
        }

        private static Action<int> DetChar2DSlope(AuraSimulationWorld world, DetCtx ctx)
        {
            const float angle = 30f * MathF.PI / 180f;
            const float length = 8f;
            StaticBox2D(world, 0f, -0.5f, 3f, 0.5f);
            StaticBox2D(world, 2f + length * MathF.Cos(angle) + 0.25f * MathF.Sin(angle), length * MathF.Sin(angle) - 0.25f * MathF.Cos(angle), length, 0.25f, angle);
            StaticBox2D(world, -8f, 2f, 0.5f, 3f, -0.4f);
            var id = NewCharacter(world, 0f, 1.2f, 45f);
            ctx.Characters.Add(id);
            return step =>
            {
                var vx = step < 200 ? 3f : -3f;
                world.MoveCharacter(id, new AuraVector3(vx * Dt, 0f, 0f), Dt);
            };
        }

        private static Action<int> DetChar2DOneWay(AuraSimulationWorld world, DetCtx ctx)
        {
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            StaticBox2D(world, 0f, 2f, 3f, 0.1f, 0f, true);
            var id = NewCharacter(world, 0f, 1f);
            var second = NewCharacter(world, 0.5f, 6f);
            ctx.Characters.Add(id);
            ctx.Characters.Add(second);
            var ball = Dyn(world, V(-1f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.3f));
            world.BodyControl.SetLinearVelocity(ball, V(0f, 9f, 0f));
            return step =>
            {
                var jump = step == 60 || step == 160 ? 10f : 0f;
                world.MoveCharacter(id, new AuraVector3(step > 100 ? 1.5f * Dt : 0f, jump * Dt, 0f), Dt);
                world.MoveCharacter(second, new AuraVector3(0f, 0f, 0f), Dt);
            };
        }

        private static Action<int> DetChar2DPlatform(AuraSimulationWorld world, DetCtx ctx)
        {
            var platform = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(V(0f, -0.5f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(8f, 0.5f, 0.5f))));
            Ok(world.BodyControl.SetLinearVelocity(platform, V(2f, 0f, 0f)), "platform velocity");
            var id = NewCharacter(world, 0f, 1f);
            ctx.Characters.Add(id);
            Dyn(world, V(1.5f, 1f, 0f), AuraPhysicsShapeDefinition.Box(V(0.3f, 0.3f, 0.5f)), mass: 0.5f);
            return step =>
            {
                if (step == 100) Ok(world.BodyControl.SetLinearVelocity(platform, V(0f, 1.5f, 0f)), "platform up");
                if (step == 180) Ok(world.BodyControl.SetLinearVelocity(platform, V(-1f, -1f, 0f)), "platform down");
                var jump = step == 230 ? 6f : 0f;
                world.MoveCharacter(id, new AuraVector3(step > 130 ? 1f * Dt : 0f, jump * Dt, 0f), Dt);
            };
        }

        private static AuraCharacterId NewCharacter3D(AuraSimulationWorld world, float x, float y, float z, float stepHeight)
        {
            var id = world.CreateCharacter(new AuraCharacterDefinition(
                new AuraPose(V(x, y, z), AuraQuaternion.Identity), CharRadius, CharHeight,
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, 70f, 45f * MathF.PI / 180f, stepHeight));
            Check(id.IsValid, "3D character creation failed.");
            return id;
        }

        private static Action<int> DetChar3D(AuraSimulationWorld world, DetCtx ctx)
        {
            Ground(world);
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(6f, 2f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(0.5f, 2f, 4f))));
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(2f, 0.125f, 2f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(1.5f, 0.125f, 1.5f))));
            Dyn(world, V(3f, 1f, 0f), AuraPhysicsShapeDefinition.Box(V(0.4f, 0.4f, 0.4f)), mass: 0.8f);
            Dyn(world, V(4f, 1f, -1f), AuraPhysicsShapeDefinition.Sphere(0.4f), mass: 0.8f);
            var id = NewCharacter3D(world, 0f, 1f, 0f, 0.3f);
            ctx.Characters.Add(id);
            return step =>
            {
                var vx = step < 200 ? 3f : -2f;
                var vz = step >= 100 && step < 160 ? 2f : step >= 260 ? -2f : 0f;
                var jump = step == 50 || step == 180 || step == 300 ? 8f : 0f;
                world.MoveCharacter(id, new AuraVector3(vx * Dt, jump * Dt, vz * Dt), Dt);
            };
        }

        private static Action<int> DetChar3DSlopePlatform(AuraSimulationWorld world, DetCtx ctx)
        {
            const float angle = 25f * MathF.PI / 180f;
            Ground(world);
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(5f, 1f, 0f), DetAngleZ(angle)), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(4f, 0.25f, 3f))));
            var platform = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(V(-6f, 0.25f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(2f, 0.25f, 2f))));
            Ok(world.BodyControl.SetLinearVelocity(platform, V(1f, 0f, 0f)), "platform velocity");
            var riding = NewCharacter3D(world, -6f, 1.2f, 0f, 0f);
            var climber = NewCharacter3D(world, 1f, 1f, 1f, 0f);
            ctx.Characters.Add(riding);
            ctx.Characters.Add(climber);
            return step =>
            {
                if (step == 120) Ok(world.BodyControl.SetLinearVelocity(platform, V(0f, 1f, 0.5f)), "platform up");
                if (step == 200) Ok(world.BodyControl.SetLinearVelocity(platform, V(-1f, -0.5f, 0f)), "platform back");
                world.MoveCharacter(riding, new AuraVector3(0f, 0f, 0f), Dt);
                world.MoveCharacter(climber, new AuraVector3((step < 150 ? 3f : -3f) * Dt, 0f, (step % 80 < 40 ? 0.5f : -0.5f) * Dt), Dt);
            };
        }

        // ---- configuration contract -------------------------------------------------------------

        private sealed class DetConfigScene
        {
            public AuraSimulationWorld World;
            public PhysicsBodyId Ball;
            public PhysicsBodyId Mover;
            public PhysicsBodyId Arm;
            public AuraJointId Hinge;
            public bool HasHinge;
        }

        /* withHinge false: contact-free bodies only (bit-exact comparison). true: a hinge arm, whose constraint warm start
           is not in the snapshot, so it is compared with a tolerance. */
        private static DetConfigScene DetBuildConfigScene(AuraPhysicsMode mode, bool withHinge)
        {
            var scene = new DetConfigScene { World = NewWorld(mode) };
            var world = scene.World;
            Ground(world);
            scene.Ball = Dyn(world, V(-3f, 4f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            scene.Mover = Dyn(world, V(3f, 6f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)));
            if (withHinge)
            {
                var rig = HingeRig(world, 1f);
                scene.Arm = rig.Arm;
                scene.Hinge = rig.Joint;
                scene.HasHinge = true;
            }

            return scene;
        }

        /* Everything the native ABI documents as configuration (not snapshot state). */
        private static void DetApplyConfig(DetConfigScene scene)
        {
            var world = scene.World;
            Ok(world.BodyControl.SetGravityScale(scene.Ball, 0.2f), "gravity scale");
            Ok(world.BodyControl.SetBodyType(scene.Mover, AuraBodyType.Kinematic), "motion type");
            Ok(world.BodyControl.SetLinearVelocity(scene.Mover, V(0f, -0.5f, 0f)), "mover velocity");
            Ok(world.BodyControl.SetLayer(scene.Ball, new AuraPhysicsLayer(3), AuraPhysicsLayerMask.All), "layer");
            if (scene.HasHinge)
            {
                Ok(world.JointControl.SetMotor(scene.Hinge, AuraJointMotorDefinition.Velocity(2f, 60f)), "motor");
                Ok(world.JointControl.SetLimits(scene.Hinge, true, -0.5f, 0.5f), "limits");
                Ok(world.JointControl.SetBreakThreshold(scene.Hinge, 1.0e6f, 1.0e6f), "break threshold");
            }

            Field(world, BoxField(AuraForceFieldKind.Directional, V(0f, 3f, 0f), V(20f, 20f, 20f), V(2f, 6f, 0f), 0f));
        }

        private static void DetContinue(AuraSimulationWorld world, int from, int to)
        {
            for (var step = from; step < to; step++)
                world.Step(new SimulationStep(new SimulationTick((uint)step + 1u), Dt));
        }

        private static float DetMaxDelta(float[] a, float[] b)
        {
            Check(a.Length == b.Length, "body count differs.");
            var worst = 0f;
            for (var index = 0; index < a.Length; index++)
                worst = Math.Max(worst, Math.Abs(a[index] - b[index]));
            return worst;
        }

        /* Documents the snapshot contract: gravity scale, motion type, layer, motors, limits, break thresholds and
           force fields are configuration, so a snapshot restored into a world without them continues differently, and
           re-applying the configuration after RestoreState reproduces the original continuation. */
        private static void DetConfigContract(AuraPhysicsMode mode, bool withHinge)
        {
            const int split = 40;
            const int end = 100;
            byte[] saved;
            ulong originalHash;
            float[] original;
            var configured = DetBuildConfigScene(mode, withHinge);
            using (configured.World)
            {
                DetApplyConfig(configured);
                DetContinue(configured.World, 0, split);
                saved = configured.World.SaveState();
                DetContinue(configured.World, split, end);
                originalHash = configured.World.ComputeStateHash();
                original = DetPositions(configured.World);
            }

            var bare = DetBuildConfigScene(mode, withHinge);
            using (bare.World)
            {
                bare.World.RestoreState(saved);
                DetContinue(bare.World, split, end);
                Check(DetMaxDelta(original, DetPositions(bare.World)) > 0.05f,
                    "a snapshot restored without re-applying configuration unexpectedly matched; the snapshot contract in Native-Abi.md changed.");
            }

            var reapplied = DetBuildConfigScene(mode, withHinge);
            using (reapplied.World)
            {
                reapplied.World.RestoreState(saved);
                DetApplyConfig(reapplied);
                DetContinue(reapplied.World, split, end);
                if (!withHinge)
                    Check(reapplied.World.ComputeStateHash() == originalHash, "re-applying configuration after RestoreState did not reproduce the original continuation hash.");
                var delta = DetMaxDelta(original, DetPositions(reapplied.World));
                if (Environment.GetEnvironmentVariable("AURA_DET_DUMP") == "1")
                    Console.WriteLine($"DET config contract {mode} hinge={withHinge} max position delta {delta:R}");
                Check(delta <= (withHinge ? 0.05f : 0f), $"re-applying configuration after RestoreState drifted by {delta}.");
            }
        }
    }
}
