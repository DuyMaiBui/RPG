using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Physics.Native;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package D: the Jolt 3D constraint set (SixDof, Cone, SwingTwist, Pulley, Gear, RackAndPinion). */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageDTests()
        {
            return new (string Name, Action Body)[]
            {
                ("d_joint_desc_layout_matches_native", D_DescLayout),
                ("d_sixdof_translation_limits_and_locked_axis", D_SixDofTranslation),
                ("d_sixdof_rotation_limit", D_SixDofRotationLimit),
                ("d_sixdof_motors", D_SixDofMotors),
                ("d_sixdof_runtime_axis_limits", D_SixDofRuntimeAxisLimits),
                ("d_cone_limits_swing", D_ConeLimitsSwing),
                ("d_swingtwist_limits_swing", D_SwingTwistSwing),
                ("d_swingtwist_limits_twist", D_SwingTwistTwist),
                ("d_swingtwist_motor_and_friction", D_SwingTwistMotor),
                ("d_pulley_keeps_rope_length", D_PulleyRopeLength),
                ("d_gear_couples_angular_velocity", D_GearCouples),
                ("d_gear_lifetime_is_memory_safe", D_GearLifetime),
                ("d_rack_and_pinion_couples", D_RackAndPinion),
                ("d_rack_and_pinion_lifetime_is_memory_safe", D_RackLifetime),
                ("d_break_threshold_on_new_types", D_BreakThresholds),
                ("d_invalid_definitions_are_rejected", D_InvalidDefinitions),
                ("d_unsupported_operations_and_stale_handles", D_UnsupportedAndStale),
            };
        }

        private static PhysicsBodyId DStatic(AuraSimulationWorld world, AuraVector3 position) =>
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(position, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.1f))));

        private static PhysicsBodyId DBox(AuraSimulationWorld world, AuraVector3 position, float gravityScale = 0f, float mass = 1f) =>
            Dyn(world, position, AuraPhysicsShapeDefinition.Box(V(0.5f, 0.1f, 0.1f)), gravityScale: gravityScale, mass: mass);

        private static AuraJointId DCreate(AuraSimulationWorld world, PhysicsBodyId a, PhysicsBodyId b, in AuraJointDefinition definition)
        {
            var joint = world.CreateJoint(FindEntity(world, a), FindEntity(world, b), definition);
            Check(joint.IsValid, $"{definition.Type} joint creation failed.");
            return joint;
        }

        private static AuraJointId DHinge(AuraSimulationWorld world, PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchor, AuraVector3 axis) =>
            DCreate(world, a, b, AuraJointDefinition.CreateHinge(a, b, anchor, anchor, axis, axis));

        private static IPhysicsJointAxisControl DAxis(AuraSimulationWorld world)
        {
            var control = world.JointAxisControl;
            Check(control != null, "native joint control must expose axis control.");
            return control;
        }

        private static float DAngle(AuraQuaternion q, int axis)
        {
            var component = axis == 0 ? q.X : axis == 1 ? q.Y : q.Z;
            var angle = 2f * MathF.Atan2(component, q.W);
            return angle > MathF.PI ? angle - 2f * MathF.PI : angle;
        }

        private static AuraSixDofLimits DLocked() => default;

        private static AuraSixDofLimits DLimits(
            AuraJointAxisLimit tx = default, AuraJointAxisLimit ty = default, AuraJointAxisLimit tz = default,
            AuraJointAxisLimit rx = default, AuraJointAxisLimit ry = default, AuraJointAxisLimit rz = default) =>
            new AuraSixDofLimits(tx, ty, tz, rx, ry, rz);

        private static void D_DescLayout()
        {
            Check(Marshal.SizeOf<NativeJointDesc>() == 280, $"NativeJointDesc is {Marshal.SizeOf<NativeJointDesc>()} bytes, native AuraJointDesc is 280.");
            Check(Marshal.SizeOf<NativeJointAxisLimit>() == 16, "NativeJointAxisLimit size.");
            Check(Marshal.SizeOf<NativeJointRef>() == 8, "NativeJointRef size.");
        }

        private static void D_SixDofTranslation()
        {
            using var world = NewWorld();
            var anchor = DStatic(world, V(0f, 5f, 0f));
            var box = DBox(world, V(0f, 5f, 0f));
            var limits = DLimits(tx: AuraJointAxisLimit.Limited(-1f, 1f));
            DCreate(world, anchor, box, AuraJointDefinition.CreateSixDof(anchor, box, V(0f, 5f, 0f), V(0f, 5f, 0f), limits));
            Ok(world.BodyControl.AddImpulse(box, V(6f, 6f, 0f)), "impulse");
            var maxX = 0f;
            for (var step = 0; step < 150; step++)
            {
                Step(world, 1, (uint)step);
                maxX = Math.Max(maxX, StateOf(world, box).Pose.Position.X);
            }

            var pose = StateOf(world, box).Pose.Position;
            Check(maxX <= 1.06f, $"translation X exceeded its limit ({maxX}).");
            Check(maxX >= 0.9f, $"translation X never reached its limit ({maxX}).");
            Near(pose.Y, 5f, 0.05f, "locked translation Y");
            Near(pose.Z, 0f, 0.05f, "locked translation Z");
        }

        private static void D_SixDofRotationLimit()
        {
            using var world = NewWorld();
            var anchor = DStatic(world, V(0f, 5f, 0f));
            var arm = DBox(world, V(1f, 5f, 0f), gravityScale: 1f);
            var limits = DLimits(rz: AuraJointAxisLimit.Limited(0f, 0.5f));
            DCreate(world, anchor, arm, AuraJointDefinition.CreateSixDof(anchor, arm, V(0f, 5f, 0f), V(0f, 5f, 0f), limits));
            Step(world, 180);
            var pose = StateOf(world, arm).Pose;
            var angle = DAngle(pose.Rotation, 2);
            Check(Math.Abs(angle) <= 0.56f, $"rotation Z exceeded its swing limit ({angle}).");
            Check(Math.Abs(angle) >= 0.4f, $"arm never reached the swing limit ({angle}).");
            Near(pose.Position.Z, 0f, 0.05f, "arm stays in plane");
        }

        private static void D_SixDofMotors()
        {
            using var world = NewWorld();
            var anchor = DStatic(world, V(0f, 5f, 0f));
            var slide = DBox(world, V(0f, 5f, 0f));
            var slideJoint = DCreate(world, anchor, slide, AuraJointDefinition.CreateSixDof(anchor, slide, V(0f, 5f, 0f), V(0f, 5f, 0f),
                DLimits(tx: AuraJointAxisLimit.Free)));
            var control = DAxis(world);
            Ok(control.SetAxisMotor(slideJoint, 0, AuraJointMotorDefinition.Velocity(2f, 1.0e5f)), "translation velocity motor");
            Step(world, 60);
            Near(StateOf(world, slide).LinearVelocity.X, 2f, 0.2f, "translation motor reaches its velocity");
            Ok(control.SetAxisMotor(slideJoint, 0, AuraJointMotorDefinition.Off), "motor off");
            Ok(control.SetAxisMotor(slideJoint, 0, AuraJointMotorDefinition.Position(StateOf(world, slide).Pose.Position.X + 2f, 1.0e5f, 4f, 1f)), "translation position motor");
            var before = StateOf(world, slide).Pose.Position.X;
            Step(world, 240, 60);
            Near(StateOf(world, slide).Pose.Position.X, before + 2f, 0.15f, "translation position motor reaches its target");

            var spin = DBox(world, V(4f, 5f, 0f));
            var spinAnchor = DStatic(world, V(4f, 5f, 0f));
            var spinJoint = DCreate(world, spinAnchor, spin, AuraJointDefinition.CreateSixDof(spinAnchor, spin, V(4f, 5f, 0f), V(4f, 5f, 0f),
                DLimits(rz: AuraJointAxisLimit.Free)));
            Ok(control.SetAxisMotor(spinJoint, 5, AuraJointMotorDefinition.Velocity(1.5f, 1.0e5f)), "rotation velocity motor");
            Step(world, 60, 400);
            Near(StateOf(world, spin).AngularVelocity.Z, 1.5f, 0.2f, "rotation motor reaches its velocity");

            Expect(control.SetAxisMotor(spinJoint, 5, AuraJointMotorDefinition.Position(0.5f, 10f)), AuraResult.UnsupportedOperation, "rotation Z position motor");
            Expect(control.SetAxisMotor(spinJoint, 5, AuraJointMotorDefinition.Velocity(1f, 0f)), AuraResult.InvalidDefinition, "motor without force");
            Expect(control.SetAxisMotor(spinJoint, 6, AuraJointMotorDefinition.Off), AuraResult.InvalidDefinition, "axis out of range");
        }

        private static void D_SixDofRuntimeAxisLimits()
        {
            using var world = NewWorld();
            var anchor = DStatic(world, V(0f, 5f, 0f));
            var box = DBox(world, V(0f, 5f, 0f));
            var joint = DCreate(world, anchor, box, AuraJointDefinition.CreateSixDof(anchor, box, V(0f, 5f, 0f), V(0f, 5f, 0f),
                DLimits(ty: AuraJointAxisLimit.Free)));
            var control = DAxis(world);
            Ok(control.SetAxisLimits(joint, 1, AuraJointAxisLimit.Limited(-0.5f, 0.5f)), "SetAxisLimits");
            Ok(world.BodyControl.AddImpulse(box, V(0f, 8f, 0f)), "impulse");
            var maxY = 0f;
            for (var step = 0; step < 120; step++)
            {
                Step(world, 1, (uint)step);
                maxY = Math.Max(maxY, StateOf(world, box).Pose.Position.Y - 5f);
            }

            Check(maxY <= 0.56f && maxY >= 0.4f, $"runtime translation limit not enforced ({maxY}).");
            Ok(control.SetAxisLimits(joint, 1, AuraJointAxisLimit.Locked), "lock the axis");
            Ok(control.SetAxisLimits(joint, 3, AuraJointAxisLimit.Free), "free rotation X");
            Expect(control.SetAxisLimits(joint, 1, AuraJointAxisLimit.Limited(1f, -1f)), AuraResult.InvalidDefinition, "min > max");
            Expect(control.SetAxisLimits(joint, 3, AuraJointAxisLimit.Limited(-4f, 1f)), AuraResult.InvalidDefinition, "rotation beyond pi");
            Expect(control.SetAxisLimits(joint, 9, AuraJointAxisLimit.Locked), AuraResult.InvalidDefinition, "axis out of range");
            Expect(control.SetAxisLimits(joint, 1, new AuraJointAxisLimit(AuraJointAxisMode.Locked, 0f, 0f, -1f)), AuraResult.InvalidDefinition, "negative friction");
        }

        private static void D_ConeLimitsSwing()
        {
            using var world = NewWorld();
            var anchor = DStatic(world, V(0f, 5f, 0f));
            var bob = DBox(world, V(0f, 4f, 0f), gravityScale: 1f);
            DCreate(world, anchor, bob, AuraJointDefinition.CreateCone(anchor, bob, V(0f, 5f, 0f), V(0f, 5f, 0f),
                V(0f, -1f, 0f), V(0f, -1f, 0f), 0.4f));
            Ok(world.BodyControl.AddImpulse(bob, V(5f, 0f, 0f)), "impulse");
            var maxSwing = 0f;
            for (var step = 0; step < 180; step++)
            {
                Step(world, 1, (uint)step);
                var p = StateOf(world, bob).Pose.Position;
                maxSwing = Math.Max(maxSwing, MathF.Atan2(MathF.Sqrt(p.X * p.X + p.Z * p.Z), 5f - p.Y));
            }

            Check(maxSwing <= 0.5f, $"cone swing exceeded the half angle ({maxSwing}).");
            Check(maxSwing >= 0.3f, $"cone never reached its half angle ({maxSwing}).");
        }

        private static (PhysicsBodyId Anchor, PhysicsBodyId Arm, AuraJointId Joint) DSwingTwistRig(AuraSimulationWorld world, float gravity,
            float swing, float twistMin, float twistMax, float friction = 0f)
        {
            var anchor = DStatic(world, V(0f, 5f, 0f));
            var arm = DBox(world, V(1f, 5f, 0f), gravityScale: gravity);
            var joint = DCreate(world, anchor, arm, AuraJointDefinition.CreateSwingTwist(anchor, arm, V(0f, 5f, 0f), V(0f, 5f, 0f),
                AuraVector3.UnitX, AuraVector3.UnitX, AuraVector3.UnitY, AuraVector3.UnitY, swing, twistMin, twistMax, 0f, friction));
            return (anchor, arm, joint);
        }

        private static void D_SwingTwistSwing()
        {
            using var world = NewWorld();
            var rig = DSwingTwistRig(world, 1f, 0.5f, -0.3f, 0.3f);
            Step(world, 180);
            var p = StateOf(world, rig.Arm).Pose.Position;
            var droop = MathF.Atan2(5f - p.Y, p.X);
            Check(droop <= 0.56f, $"swing exceeded its cone ({droop}).");
            Check(droop >= 0.4f, $"arm never reached the cone ({droop}).");
        }

        private static void D_SwingTwistTwist()
        {
            using var world = NewWorld();
            var rig = DSwingTwistRig(world, 0f, 0.5f, -0.3f, 0.4f);
            Ok(world.BodyControl.SetAngularVelocity(rig.Arm, V(4f, 0f, 0f)), "spin about the twist axis");
            Step(world, 120);
            var twist = DAngle(StateOf(world, rig.Arm).Pose.Rotation, 0);
            Check(twist <= 0.45f && twist >= 0.33f, $"twist did not stop at its max limit ({twist}).");
            Ok(world.BodyControl.SetAngularVelocity(rig.Arm, V(-4f, 0f, 0f)), "spin back");
            Step(world, 120, 120);
            twist = DAngle(StateOf(world, rig.Arm).Pose.Rotation, 0);
            Check(twist >= -0.35f && twist <= -0.25f, $"twist did not stop at its min limit ({twist}).");

            Ok(world.JointControl.SetLimits(rig.Joint, true, -0.1f, 0.1f), "SetLimits narrows the twist range");
            Ok(world.BodyControl.SetAngularVelocity(rig.Arm, V(3f, 0f, 0f)), "spin again");
            Step(world, 120, 240);
            twist = DAngle(StateOf(world, rig.Arm).Pose.Rotation, 0);
            Check(twist <= 0.15f, $"runtime twist limit not enforced ({twist}).");
            Expect(world.JointControl.SetLimits(rig.Joint, true, -4f, 1f), AuraResult.InvalidDefinition, "twist beyond pi");
        }

        private static void D_SwingTwistMotor()
        {
            using var world = NewWorld();
            var rig = DSwingTwistRig(world, 0f, 0.5f, -3.1f, 3.1f);
            var control = DAxis(world);
            Ok(control.SetAxisMotor(rig.Joint, 0, AuraJointMotorDefinition.Velocity(1f, 1.0e5f)), "twist motor");
            Step(world, 40);
            Near(StateOf(world, rig.Arm).AngularVelocity.X, 1f, 0.2f, "twist motor reaches its velocity");
            Ok(world.JointControl.GetFeedback(rig.Joint, out var feedback), "feedback");
            Check(feedback.MotorLoad >= 0f, "motor load.");
            Ok(control.SetAxisMotor(rig.Joint, 0, AuraJointMotorDefinition.Off), "motor off");
            Expect(control.SetAxisMotor(rig.Joint, 1, AuraJointMotorDefinition.Position(0.2f, 10f)), AuraResult.UnsupportedOperation, "swing position motor");
            Ok(control.SetAxisMotor(rig.Joint, 1, AuraJointMotorDefinition.Velocity(0f, 100f)), "swing hold motor");
            Ok(control.SetAxisMotor(rig.Joint, 1, AuraJointMotorDefinition.Off), "swing motor off");
            Expect(control.SetAxisMotor(rig.Joint, 3, AuraJointMotorDefinition.Off), AuraResult.UnsupportedOperation, "axis 3 on swing twist");
            Ok(control.SetAxisLimits(rig.Joint, 1, AuraJointAxisLimit.Limited(0f, 0.2f)), "swing limit update");
            Expect(control.SetAxisLimits(rig.Joint, 1, AuraJointAxisLimit.Free), AuraResult.UnsupportedOperation, "swing twist axes are always limited");

            /* Friction brakes a free spin when no motor drives the joint. */
            using var friction = NewWorld();
            var spinning = DSwingTwistRig(friction, 0f, 0.5f, -3.1f, 3.1f, 5f);
            Ok(friction.BodyControl.SetAngularVelocity(spinning.Arm, V(0.5f, 0f, 0f)), "spin");
            Step(friction, 120);
            Near(StateOf(friction, spinning.Arm).AngularVelocity.X, 0f, 0.1f, "friction torque stops the twist");
        }

        private static void D_PulleyRopeLength()
        {
            using var world = NewWorld();
            var f1 = V(-1f, 10f, 0f);
            var f2 = V(1f, 10f, 0f);
            var light = Dyn(world, V(-1f, 6f, 0f), AuraPhysicsShapeDefinition.Box(V(0.2f, 0.2f, 0.2f)), mass: 1f);
            var heavy = Dyn(world, V(1f, 4f, 0f), AuraPhysicsShapeDefinition.Box(V(0.2f, 0.2f, 0.2f)), mass: 4f);
            const float ratio = 2f;
            var length = 4f + ratio * 6f;
            var joint = DCreate(world, light, heavy, AuraJointDefinition.CreatePulley(light, heavy, V(-1f, 6f, 0f), V(1f, 4f, 0f), f1, f2, ratio, length, length));
            Step(world, 90);
            var a = StateOf(world, light).Pose.Position;
            var b = StateOf(world, heavy).Pose.Position;
            var sum = Distance(a, f1) + ratio * Distance(b, f2);
            Near(sum, length, 0.1f, "pulley keeps the rope length sum");
            Ok(world.JointControl.GetFeedback(joint, out var feedback), "feedback");
            Near(feedback.Position, length, 0.1f, "feedback reports the rope length");
            Check(Math.Abs(b.Y - 4f) > 0.2f || Math.Abs(a.Y - 6f) > 0.2f, "pulley bodies never moved.");

            Ok(world.JointControl.SetLimits(joint, true, 0f, length), "rope limits");
            Expect(world.JointControl.SetLimits(joint, true, 5f, 2f), AuraResult.InvalidDefinition, "min > max");
            Expect(world.JointControl.SetLimits(joint, true, -1f, 2f), AuraResult.InvalidDefinition, "negative length");
            Expect(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Off), AuraResult.UnsupportedOperation, "pulley motor");
            Ok(world.JointControl.SetBreakThreshold(joint, 1.0e6f, 0f), "pulley force threshold");
            Expect(world.JointControl.SetBreakThreshold(joint, 1.0e6f, 1f), AuraResult.InvalidDefinition, "pulley torque threshold");
        }

        private static float Distance(AuraVector3 a, AuraVector3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private sealed class DGearRig
        {
            public PhysicsBodyId Ground;
            public PhysicsBodyId BodyA;
            public PhysicsBodyId BodyB;
            public AuraJointId HingeA;
            public AuraJointId HingeB;
            public AuraJointId Gear;
        }

        private static DGearRig DBuildGear(AuraSimulationWorld world, float ratio)
        {
            var rig = new DGearRig
            {
                Ground = DStatic(world, V(0f, 5f, -2f)),
                BodyA = Dyn(world, V(-1f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.4f, 0.4f, 0.1f)), gravityScale: 0f),
                BodyB = Dyn(world, V(1f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.4f, 0.4f, 0.1f)), gravityScale: 0f),
            };
            rig.HingeA = DHinge(world, rig.Ground, rig.BodyA, V(-1f, 5f, 0f), AuraVector3.UnitZ);
            rig.HingeB = DHinge(world, rig.Ground, rig.BodyB, V(1f, 5f, 0f), AuraVector3.UnitZ);
            rig.Gear = DCreate(world, rig.BodyA, rig.BodyB,
                AuraJointDefinition.CreateGear(rig.BodyA, rig.BodyB, AuraVector3.UnitZ, AuraVector3.UnitZ, ratio, rig.HingeA, rig.HingeB));
            return rig;
        }

        private static void D_GearCouples()
        {
            using var world = NewWorld();
            var rig = DBuildGear(world, 2f);
            Ok(world.JointControl.SetMotor(rig.HingeA, AuraJointMotorDefinition.Velocity(2f, 1.0e5f)), "drive gear A");
            Step(world, 50);
            var angleA = DAngle(StateOf(world, rig.BodyA).Pose.Rotation, 2);
            var angleB = DAngle(StateOf(world, rig.BodyB).Pose.Rotation, 2);
            Check(Math.Abs(angleA) > 0.3f && Math.Abs(angleA) < 3f, $"gear A angle out of the expected range ({angleA}).");
            Near(angleA, -2f * angleB, 0.15f + 0.1f * Math.Abs(angleA), "gear angles stay coupled by the ratio");
            Step(world, 70, 50);
            var wa = StateOf(world, rig.BodyA).AngularVelocity.Z;
            var wb = StateOf(world, rig.BodyB).AngularVelocity.Z;
            Near(wa, 2f, 0.25f, "driven gear velocity");
            Near(wb, -wa / 2f, 0.2f, "gear B turns opposite at 1/ratio");
        }

        private static void D_GearLifetime()
        {
            /* Each pass tears the rig down in a different order; the gear must never read a freed hinge. */
            for (var order = 0; order < 6; order++)
            {
                using var world = NewWorld();
                var rig = DBuildGear(world, 1.5f);
                Ok(world.JointControl.SetMotor(rig.HingeA, AuraJointMotorDefinition.Velocity(1f, 1.0e4f)), "drive");
                Step(world, 20);
                switch (order)
                {
                    case 0:
                        Ok(world.DestroyJoint(rig.HingeA), "destroy hinge A");
                        break;
                    case 1:
                        Ok(world.DestroyJoint(rig.HingeB), "destroy hinge B");
                        break;
                    case 2:
                        Ok(world.DestroyJoint(rig.Gear), "destroy gear");
                        Ok(world.DestroyJoint(rig.HingeA), "destroy hinge A");
                        Ok(world.DestroyJoint(rig.HingeB), "destroy hinge B");
                        break;
                    case 3:
                        Check(world.DestroyEntity(FindEntity(world, rig.BodyA)), "destroy gear body A");
                        break;
                    case 4:
                        Check(world.DestroyEntity(FindEntity(world, rig.Ground)), "destroy the hinge ground");
                        break;
                    default:
                        Ok(world.JointControl.SetBreakThreshold(rig.HingeB, 0.5f, 0f), "hinge B break threshold");
                        Ok(world.BodyControl.AddImpulse(rig.BodyB, V(0f, 8f, 0f)), "load hinge B");
                        break;
                }

                Step(world, 30, 20);
                if (order == 3)
                {
                    /* The gear itself touches body A, so destroying that body frees the gear handle too. */
                    Expect(world.JointControl.IsBroken(rig.Gear, out _), AuraResult.InvalidHandle, "gear freed with its body");
                }
                else if (order != 2)
                {
                    Ok(world.JointControl.IsBroken(rig.Gear, out var broken), "gear IsBroken");
                    Check(broken, $"order {order}: gear survived the loss of a referenced joint.");
                    Check(world.HasJoint(rig.Gear), $"order {order}: dissolved gear handle must stay valid until DestroyJoint.");
                    Ok(world.DestroyJoint(rig.Gear), "destroy the dissolved gear");
                }

                Step(world, 10, 50);
            }
        }

        private static void D_RackAndPinion()
        {
            using var world = NewWorld();
            var ground = DStatic(world, V(0f, 5f, -2f));
            var pinion = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.4f, 0.4f, 0.1f)), gravityScale: 0f);
            var rack = Dyn(world, V(0f, 4f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.1f, 0.1f)), gravityScale: 0f);
            var hinge = DHinge(world, ground, pinion, V(0f, 5f, 0f), AuraVector3.UnitZ);
            var slider = DCreate(world, ground, rack, AuraJointDefinition.CreateSlider(ground, rack, V(0f, 4f, 0f), V(0f, 4f, 0f), AuraVector3.UnitX, AuraVector3.UnitX));
            var coupling = DCreate(world, pinion, rack, AuraJointDefinition.CreateRackAndPinion(pinion, rack, AuraVector3.UnitZ, AuraVector3.UnitX, 2f, hinge, slider));
            Ok(world.JointControl.SetMotor(slider, AuraJointMotorDefinition.Velocity(1f, 1.0e5f)), "drive the rack");
            Step(world, 90);
            var rackVelocity = StateOf(world, rack).LinearVelocity.X;
            var pinionVelocity = StateOf(world, pinion).AngularVelocity.Z;
            Near(rackVelocity, 1f, 0.2f, "driven rack velocity");
            Near(Math.Abs(pinionVelocity), 2f * Math.Abs(rackVelocity), 0.35f, "pinion turns ratio radians per metre");
            Check(Math.Abs(DAngle(StateOf(world, pinion).Pose.Rotation, 2)) > 0.3f, "pinion never turned.");

            Ok(world.JointControl.SetBreakThreshold(coupling, 1.0e6f, 1.0e6f), "coupling threshold");
            Ok(world.JointControl.GetFeedback(coupling, out _), "coupling feedback");
        }

        private static void D_RackLifetime()
        {
            for (var order = 0; order < 4; order++)
            {
                using var world = NewWorld();
                var ground = DStatic(world, V(0f, 5f, -2f));
                var pinion = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.4f, 0.4f, 0.1f)), gravityScale: 0f);
                var rack = Dyn(world, V(0f, 4f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.1f, 0.1f)), gravityScale: 0f);
                var hinge = DHinge(world, ground, pinion, V(0f, 5f, 0f), AuraVector3.UnitZ);
                var slider = DCreate(world, ground, rack, AuraJointDefinition.CreateSlider(ground, rack, V(0f, 4f, 0f), V(0f, 4f, 0f), AuraVector3.UnitX, AuraVector3.UnitX));
                var coupling = DCreate(world, pinion, rack, AuraJointDefinition.CreateRackAndPinion(pinion, rack, AuraVector3.UnitZ, AuraVector3.UnitX, 1f, hinge, slider));
                Step(world, 10);
                switch (order)
                {
                    case 0: Ok(world.DestroyJoint(slider), "destroy slider"); break;
                    case 1: Ok(world.DestroyJoint(hinge), "destroy hinge"); break;
                    case 2: Check(world.DestroyEntity(FindEntity(world, rack)), "destroy rack"); break;
                    default: Ok(world.DestroyJoint(coupling), "destroy coupling"); break;
                }

                Step(world, 30, 10);
                if (order == 2)
                {
                    Expect(world.JointControl.IsBroken(coupling, out _), AuraResult.InvalidHandle, "coupling freed with its rack body");
                }
                else if (order != 3)
                {
                    Ok(world.JointControl.IsBroken(coupling, out var broken), "IsBroken");
                    Check(broken, $"order {order}: coupling survived the loss of a referenced joint.");
                    Ok(world.DestroyJoint(coupling), "destroy dissolved coupling");
                }

                Step(world, 10, 40);
            }
        }

        private static void D_BreakThresholds()
        {
            using var world = NewWorld();
            var anchor = DStatic(world, V(0f, 6f, 0f));
            var weight = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), mass: 10f);
            var joint = DCreate(world, anchor, weight, AuraJointDefinition.CreateSixDof(anchor, weight, V(0f, 5.5f, 0f), V(0f, 5.5f, 0f), DLocked()));
            Step(world, 60);
            Ok(world.JointControl.GetFeedback(joint, out var feedback), "feedback");
            Near(feedback.Force, 98.1f, 25f, "SixDof reaction carries the weight");
            Ok(world.JointControl.SetBreakThreshold(joint, 49f, 0f), "SixDof break threshold");
            Step(world, 10, 60);
            Ok(world.JointControl.IsBroken(joint, out var broken), "IsBroken");
            Check(broken, "SixDof joint did not break above its threshold.");
            Step(world, 30, 70);
            Check(StateOf(world, weight).Pose.Position.Y < 4f, "weight did not fall after the SixDof broke.");

            var a = DStatic(world, V(10f, 6f, 0f));
            var b = DBox(world, V(11f, 6f, 0f));
            var cone = DCreate(world, a, b, AuraJointDefinition.CreateCone(a, b, V(10f, 6f, 0f), V(10f, 6f, 0f), AuraVector3.UnitX, AuraVector3.UnitX, 0.5f));
            Ok(world.JointControl.SetBreakThreshold(cone, 1.0e6f, 1.0e6f), "cone thresholds");
            var c = DStatic(world, V(20f, 6f, 0f));
            var d = DBox(world, V(21f, 6f, 0f));
            var swingTwist = DCreate(world, c, d, AuraJointDefinition.CreateSwingTwist(c, d, V(20f, 6f, 0f), V(20f, 6f, 0f),
                AuraVector3.UnitX, AuraVector3.UnitX, AuraVector3.UnitY, AuraVector3.UnitY, 0.5f, -0.2f, 0.2f));
            Ok(world.JointControl.SetBreakThreshold(swingTwist, 1.0e6f, 1.0e6f), "swing twist thresholds");
            Ok(world.JointControl.GetFeedback(swingTwist, out _), "swing twist feedback");
            Step(world, 20, 100);
        }

        private static void D_InvalidDefinitions()
        {
            using var world = NewWorld();
            var a = DBox(world, V(0f, 5f, 0f));
            var b = DBox(world, V(1f, 5f, 0f));
            var origin = V(0f, 5f, 0f);

            void Throws<T>(Action action, string label) where T : Exception
            {
                try
                {
                    action();
                }
                catch (T)
                {
                    return;
                }

                throw new Exception(label + ": the factory accepted an invalid definition.");
            }

            Throws<ArgumentException>(() => AuraJointDefinition.CreateSixDof(a, b, origin, origin, DLimits(tx: AuraJointAxisLimit.Limited(2f, 1f))), "SixDof min > max");
            Throws<ArgumentException>(() => AuraJointDefinition.CreateSixDof(a, b, origin, origin, DLimits(rx: AuraJointAxisLimit.Limited(-4f, 1f))), "SixDof rotation X");
            Throws<ArgumentException>(() => AuraJointDefinition.CreateSixDof(a, b, origin, origin, DLimits(ry: AuraJointAxisLimit.Limited(-1f, 1f), tx: AuraJointAxisLimit.Limited(float.NaN, 1f))), "SixDof NaN");
            Throws<ArgumentException>(() => AuraJointDefinition.CreateCone(a, b, origin, origin, AuraVector3.UnitX, AuraVector3.UnitX, 4f), "cone angle");
            Throws<ArgumentException>(() => AuraJointDefinition.CreateSwingTwist(a, b, origin, origin, AuraVector3.UnitX, AuraVector3.UnitX, AuraVector3.UnitY, AuraVector3.UnitY, 0.5f, 0.3f, -0.3f), "twist min > max");
            Throws<ArgumentException>(() => AuraJointDefinition.CreateSwingTwist(a, b, origin, origin, AuraVector3.UnitX, AuraVector3.UnitX, AuraVector3.UnitY, AuraVector3.UnitY, -1f), "negative swing");
            Throws<ArgumentException>(() => AuraJointDefinition.CreatePulley(a, b, origin, origin, origin, origin, 0f, 1f, 2f), "pulley ratio 0");
            Throws<ArgumentException>(() => AuraJointDefinition.CreatePulley(a, b, origin, origin, origin, origin, 1f, 3f, 2f), "pulley min > max");
            Throws<ArgumentException>(() => AuraJointDefinition.CreateGear(a, b, AuraVector3.UnitZ, AuraVector3.UnitZ, 1f, AuraJointId.Invalid, new AuraJointId(1, 0)), "gear without joint");
            Throws<ArgumentException>(() => AuraJointDefinition.CreateGear(a, b, AuraVector3.UnitZ, AuraVector3.UnitZ, -1f, new AuraJointId(0, 0), new AuraJointId(1, 0)), "gear ratio");
            Throws<ArgumentException>(() => AuraJointDefinition.CreateGear(a, b, AuraVector3.Zero, AuraVector3.UnitZ, 1f, new AuraJointId(0, 0), new AuraJointId(1, 0)), "gear axis");
            Throws<ArgumentException>(() => AuraJointDefinition.CreateRackAndPinion(a, b, AuraVector3.UnitZ, AuraVector3.UnitX, 1f, new AuraJointId(2, 0), new AuraJointId(2, 0)), "same referenced joint");

            /* The kernel validates too: definitions built through the public constructor skip the factory checks. */
            AuraJointId Create(in AuraJointDefinition definition) => world.CreateJoint(FindEntity(world, a), FindEntity(world, b), definition);
            Check(!Create(new AuraJointDefinition(AuraJointType.Cone, a, b, origin, origin, 0f, AuraVector3.UnitX, AuraVector3.UnitX, true, 0f, 4f)).IsValid, "kernel accepted a cone angle beyond pi.");
            Check(!Create(new AuraJointDefinition(AuraJointType.SwingTwist, a, b, origin, origin, 0f, AuraVector3.UnitX, AuraVector3.UnitX, true, 0.4f, -0.4f, AuraVector3.UnitY, AuraVector3.UnitY, 0.5f)).IsValid, "kernel accepted inverted twist limits.");
            Check(!Create(new AuraJointDefinition(AuraJointType.Gear, a, b, origin, origin)).IsValid, "kernel accepted a gear without joints.");
            Check(!Create(new AuraJointDefinition(AuraJointType.Path, a, b, origin, origin)).IsValid, "Path constraints are unsupported and must be rejected.");
            Check(!Create(new AuraJointDefinition(AuraJointType.Pulley, a, b, origin, origin, -2f)).IsValid, "kernel accepted a negative rope length.");

            /* Gear referencing joints of the wrong kind or the wrong bodies. */
            var ground = DStatic(world, V(0f, 5f, -2f));
            var slider = DCreate(world, ground, a, AuraJointDefinition.CreateSlider(ground, a, origin, origin, AuraVector3.UnitX, AuraVector3.UnitX));
            var hinge = DHinge(world, ground, b, V(1f, 5f, 0f), AuraVector3.UnitZ);
            Check(!world.CreateJoint(FindEntity(world, a), FindEntity(world, b),
                AuraJointDefinition.CreateGear(a, b, AuraVector3.UnitZ, AuraVector3.UnitZ, 1f, slider, hinge)).IsValid, "gear accepted a slider as a hinge.");
            Check(!world.CreateJoint(FindEntity(world, a), FindEntity(world, b),
                AuraJointDefinition.CreateRackAndPinion(a, b, AuraVector3.UnitZ, AuraVector3.UnitX, 1f, hinge, slider)).IsValid, "rack and pinion accepted joints on the wrong bodies.");
            Check(!world.CreateJoint(FindEntity(world, a), FindEntity(world, b),
                AuraJointDefinition.CreateGear(a, b, AuraVector3.UnitZ, AuraVector3.UnitZ, 1f, new AuraJointId(300, 0), hinge)).IsValid, "gear accepted a stale joint.");
        }

        private static void D_UnsupportedAndStale()
        {
            using var world = NewWorld();
            var anchor = DStatic(world, V(0f, 5f, 0f));
            var arm = DBox(world, V(1f, 5f, 0f));
            var hinge = DHinge(world, anchor, arm, V(0f, 5f, 0f), AuraVector3.UnitZ);
            var control = DAxis(world);
            Expect(control.SetAxisLimits(hinge, 0, AuraJointAxisLimit.Locked), AuraResult.UnsupportedOperation, "axis limits on hinge");
            Expect(control.SetAxisMotor(hinge, 0, AuraJointMotorDefinition.Off), AuraResult.UnsupportedOperation, "axis motor on hinge");

            var b = DBox(world, V(5f, 5f, 0f));
            var a = DStatic(world, V(4f, 5f, 0f));
            var cone = DCreate(world, a, b, AuraJointDefinition.CreateCone(a, b, V(4f, 5f, 0f), V(4f, 5f, 0f), AuraVector3.UnitX, AuraVector3.UnitX, 0.5f));
            Expect(world.JointControl.SetMotor(cone, AuraJointMotorDefinition.Off), AuraResult.UnsupportedOperation, "cone motor");
            Expect(world.JointControl.SetLimits(cone, true, -0.1f, 0.1f), AuraResult.UnsupportedOperation, "cone limits");
            Expect(control.SetAxisMotor(cone, 0, AuraJointMotorDefinition.Off), AuraResult.UnsupportedOperation, "cone axis motor");

            foreach (var stale in new[] { AuraJointId.Invalid, new AuraJointId(0, 77), new AuraJointId(4000, 0) })
            {
                Expect(control.SetAxisLimits(stale, 0, AuraJointAxisLimit.Locked), AuraResult.InvalidHandle, "stale axis limits");
                Expect(control.SetAxisMotor(stale, 0, AuraJointMotorDefinition.Off), AuraResult.InvalidHandle, "stale axis motor");
            }

            Ok(world.DestroyJoint(hinge), "destroy hinge");
            Expect(control.SetAxisLimits(hinge, 0, AuraJointAxisLimit.Locked), AuraResult.InvalidHandle, "axis limits after destroy");
        }
    }
}
