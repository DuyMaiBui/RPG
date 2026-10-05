using System;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal static class SoakJointOps
    {
        private const ulong InvalidRef = ulong.MaxValue;

        public static void BodyDestroyed(SoakWorld world, NativeBodyHandle body, int op)
        {
            foreach (var joint in world.Joints)
            {
                if (joint.State == SoakHandleState.Live
                    && ((joint.BodyA.Index == body.Index && joint.BodyA.Generation == body.Generation)
                        || (joint.BodyB.Index == body.Index && joint.BodyB.Generation == body.Generation)))
                    joint.Mark(SoakHandleState.Unknown, "L17", op);
            }

            // Gear / rack-and-pinion constraints follow the joints they reference.
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var joint in world.Joints)
                {
                    if (joint.State != SoakHandleState.Live || (joint.RefA == InvalidRef && joint.RefB == InvalidRef))
                        continue;
                    if (IsUnknown(world, joint.RefA) || IsUnknown(world, joint.RefB))
                        joint.Mark(SoakHandleState.Unknown, "L28", op);
                }
            }
        }

        private static bool TooFarApart(NativeBodyHandle a, NativeBodyHandle b, SoakWorld world)
        {
            if (NativeMethods.Aura_GetBodyState(world.Handle, a, out var sa) != SoakCodes.Success
                || NativeMethods.Aura_GetBodyState(world.Handle, b, out var sb) != SoakCodes.Success)
                return false;
            var dx = sa.Pose.Position.X - sb.Pose.Position.X;
            var dy = sa.Pose.Position.Y - sb.Pose.Position.Y;
            return dx * dx + dy * dy > 16f;
        }

        private static bool IsDisabled(SoakWorld world, NativeBodyHandle handle) =>
            world.BodyByHandle.TryGetValue(SoakWorld.Key(handle), out var body) && body.Disabled;

        private static bool IsUnknown(SoakWorld world, ulong handle) =>
            handle != InvalidRef && world.JointByHandle.TryGetValue(handle, out var joint) && joint.State == SoakHandleState.Unknown;

        public static SoakHandleState Pick(SoakEpisode e, SoakWorld world, out ulong handle)
        {
            var rng = e.Rng;
            var roll = rng.Unit();
            if (roll < 0.85f && world.Joints.Count > 0)
            {
                var joint = world.Joints[rng.Next(world.Joints.Count)];
                handle = joint.Handle;
                return joint.State;
            }

            switch (rng.Next(3))
            {
                case 0: handle = InvalidRef; break;
                case 1: handle = 0; break;
                default: handle = ((ulong)rng.Range(0, 3) << 32) | (uint)rng.Range(0, 300); break;
            }

            return world.JointByHandle.TryGetValue(handle, out var known) ? known.State : SoakHandleState.Dead;
        }

        private static ulong PickRef(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            if (world.Joints.Count > 0 && rng.Chance(0.85f))
            {
                // Prefer hinges and sliders so gear and rack constraints sometimes succeed.
                for (var attempt = 0; attempt < 6; attempt++)
                {
                    var joint = world.Joints[rng.Next(world.Joints.Count)];
                    if (attempt == 5 || joint.Type == 2 || joint.Type == 4)
                        return joint.Handle;
                }
            }

            return rng.Chance(0.5f) ? InvalidRef : ((ulong)rng.Range(0, 3) << 32) | (uint)rng.Range(0, 300);
        }

        private static NativeVector3 Perp(SoakRng rng, NativeVector3 axis)
        {
            if (rng.Chance(0.1f))
                return SoakValues.UnitVec(rng);
            var other = Math.Abs(axis.Y) < 0.9f ? new NativeVector3 { Y = 1f } : new NativeVector3 { X = 1f };
            var c = new NativeVector3
            {
                X = axis.Y * other.Z - axis.Z * other.Y,
                Y = axis.Z * other.X - axis.X * other.Z,
                Z = axis.X * other.Y - axis.Y * other.X,
            };
            var l = (float)Math.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z);
            if (l < 1e-4f)
                return new NativeVector3 { X = 1f };
            return new NativeVector3 { X = c.X / l, Y = c.Y / l, Z = c.Z / l };
        }

        public static void Create(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            if (world.Joints.Count > 150)
                return;
            var type = SoakSettings.Bad(rng, 0.03f) ? rng.Pick(new[] { -1, 96, 16 }) : rng.Range(0, 14);
            var stateA = SoakBodyOps.Pick(e, world, 0.9f, out var a);
            var stateB = SoakBodyOps.Pick(e, world, 0.9f, out var b);
            if (rng.Chance(0.08f))
            {
                b = a;
                stateB = stateA;
            }

            var axisA = SoakValues.UnitVec(rng);
            var axisB = rng.Chance(0.5f) ? axisA : SoakValues.UnitVec(rng);
            var desc = new NativeJointDesc
            {
                Type = type,
                BodyA = a,
                BodyB = b,
                AnchorA = SoakValues.Vec(rng, 1f),
                AnchorB = SoakValues.Vec(rng, 1f),
                AxisA = axisA,
                AxisB = axisB,
                NormalAxisA = Perp(rng, axisA),
                NormalAxisB = Perp(rng, axisB),
                FixedPoint = SoakValues.Vec(rng, 4f),
                Distance = SoakValues.Float(rng, 0f, 3f),
                MinLimit = SoakValues.Float(rng, -2.5f, 0f),
                MaxLimit = SoakValues.Float(rng, 0f, 2.5f),
                SwingLimit = SoakValues.Float(rng, 0f, 2f),
                MotorTargetVelocity = SoakValues.Float(rng, -5f, 5f),
                MaxMotorForce = SoakValues.Float(rng, 0f, 100f),
                SpringFrequency = SoakValues.Float(rng, 0f, 8f),
                SpringDamping = SoakValues.Float(rng, 0f, 1f),
                EnableLimit = (byte)(rng.Chance(0.5f) ? 1 : 0),
                MotorEnabled = (byte)(rng.Chance(0.3f) ? 1 : 0),
                FixedPointB = SoakValues.Vec(rng, 4f),
                Ratio = rng.Chance(0.1f) ? 0f : SoakValues.Float(rng, -3f, 3f),
                PlaneSwingLimit = SoakValues.Float(rng, 0f, 2f),
                MaxFriction = SoakValues.Float(rng, 0f, 5f),
                PyramidSwing = (byte)(rng.Chance(0.5f) ? 1 : 0),
            };
            var refA = InvalidRef;
            var refB = InvalidRef;
            if (type == 13 || type == 14 || rng.Chance(0.05f))
            {
                refA = PickRef(e, world);
                refB = rng.Chance(0.1f) ? refA : PickRef(e, world);
                desc.JointRefA = new NativeJointRef { Index = (uint)refA, Generation = (uint)(refA >> 32) };
                desc.JointRefB = new NativeJointRef { Index = (uint)refB, Generation = (uint)(refB >> 32) };
            }
            else
            {
                desc.JointRefA = new NativeJointRef { Index = uint.MaxValue, Generation = uint.MaxValue };
                desc.JointRefB = desc.JointRefA;
            }

            desc.Axis0 = Axis(rng); desc.Axis1 = Axis(rng); desc.Axis2 = Axis(rng);
            desc.Axis3 = Axis(rng); desc.Axis4 = Axis(rng); desc.Axis5 = Axis(rng);

            if (SoakSettings.TolerateKnown && !world.Is2D && (IsDisabled(world, a) || IsDisabled(world, b)))
            {
                // Known crash: a joint on a disabled body segfaults the next Aura_Step in Jolt.
                e.Begin("CreateJoint(skipped,disabled-body) " + world + " type=" + type);
                e.End(SoakCodes.InvalidDefinition);
                return;
            }

            if (SoakSettings.TolerateKnown && world.Is2D && (type == 0 || type == 4 || type == 12 || TooFarApart(a, b, world)))
            {
                // Known defect: Box2D distance, rope and prismatic (slider) joints can blow up to 1e7..NaN, and any 2D joint created between
                // bodies far outside its constraint can produce NaN poses within a few steps.
                e.Begin("CreateJoint(skipped,far-apart-2D) " + world + " type=" + type);
                e.End(SoakCodes.InvalidDefinition);
                return;
            }

            e.Begin("CreateJoint " + world + " type=" + type + " A=" + SoakBodyOps.Name(a) + " B=" + SoakBodyOps.Name(b)
                + " refs=" + (refA == InvalidRef ? "-" : refA.ToString()) + "," + (refB == InvalidRef ? "-" : refB.ToString())
                + " ratio=" + desc.Ratio + " dist=" + desc.Distance + " limit=" + desc.EnableLimit + "[" + desc.MinLimit + "," + desc.MaxLimit + "]"
                + " fixedA=" + desc.FixedPoint.X + "," + desc.FixedPoint.Y + "," + desc.FixedPoint.Z + " fixedB=" + desc.FixedPointB.X + "," + desc.FixedPointB.Y + "," + desc.FixedPointB.Z
                + " anchorA=" + desc.AnchorA.X + "," + desc.AnchorA.Y + "," + desc.AnchorA.Z + " anchorB=" + desc.AnchorB.X + "," + desc.AnchorB.Y + "," + desc.AnchorB.Z
                + " spring=" + desc.SpringFrequency + "/" + desc.SpringDamping + " swing=" + desc.SwingLimit);
            var code = e.End(NativeMethods.Aura_CreateJoint(world.Handle, ref desc, out var handle));
            SoakChecks.World(e, world, "CreateJoint", code);
            if (code != SoakCodes.Success)
                return;
            e.Mix(handle);
            e.Log.Append("handle=" + handle);
            if (type != 11 && (stateA == SoakHandleState.Dead && world.BodyByHandle.ContainsKey(SoakWorld.Key(a))
                || stateB == SoakHandleState.Dead && world.BodyByHandle.ContainsKey(SoakWorld.Key(b))))
                e.Fail("HANDLE CONFUSION: CreateJoint type " + type + " accepted a destroyed body in " + world);
            if (world.JointByHandle.TryGetValue(handle, out var existing))
                e.Fail(existing.State == SoakHandleState.Dead
                    ? "HANDLE CONFUSION: CreateJoint reissued destroyed joint handle " + handle + " in " + world
                    : "HANDLE CONFUSION: CreateJoint returned handle " + handle + " that is already in use in " + world);
            var joint = new SoakJoint { Handle = handle, Type = type, BodyA = a, BodyB = b, RefA = refA, RefB = refB };
            world.Joints.Add(joint);
            world.JointByHandle[handle] = joint;
        }

        private static NativeJointAxisLimit Axis(SoakRng rng) => new NativeJointAxisLimit
        {
            Mode = (byte)(!SoakSettings.Bad(rng, 0.05f) ? rng.Range(0, 2) : 3),
            MinLimit = SoakValues.Float(rng, -1.5f, 0f),
            MaxLimit = SoakValues.Float(rng, 0f, 1.5f),
            MaxFriction = SoakValues.Float(rng, 0f, 3f),
        };

        public static void Destroy(SoakEpisode e, SoakWorld world)
        {
            var state = Pick(e, world, out var handle);
            e.Begin("DestroyJoint " + world + " " + handle);
            var code = e.End(NativeMethods.Aura_DestroyJoint(world.Handle, handle));
            Resolve(e, world, handle, state, "DestroyJoint", code);
            if (code == SoakCodes.Success && world.JointByHandle.TryGetValue(handle, out var joint))
                joint.Mark(SoakHandleState.Dead, "L222", e.OpIndex);
        }

        private static void Resolve(SoakEpisode e, SoakWorld world, ulong handle, SoakHandleState state, string op, int code)
        {
            if (state == SoakHandleState.Unknown)
            {
                SoakChecks.World(e, world, op, code);
                if (world.JointByHandle.TryGetValue(handle, out var joint))
                {
                    if (code == SoakCodes.InvalidHandle)
                    {
                        if (IsBroken(world, handle))
                            SoakSettings.BrokenJointRejections++;
                        else
                            joint.Mark(SoakHandleState.Dead, "L237", e.OpIndex);
                    }
                    else if (code == SoakCodes.Success)
                        joint.Mark(SoakHandleState.Live, "L240", e.OpIndex);
                }

                return;
            }

            if (state == SoakHandleState.Live && code == SoakCodes.InvalidHandle && IsBroken(world, handle))
            {
                // Known contract gap: control calls and HasJoint reject a broken joint although the handle stays valid.
                SoakSettings.BrokenJointRejections++;
                if (world.JointByHandle.TryGetValue(handle, out var broken))
                    broken.Mark(SoakHandleState.Unknown, "L251", e.OpIndex);
                return;
            }

            if (state == SoakHandleState.Dead && code == SoakCodes.Success && world.JointByHandle.TryGetValue(handle, out var stale))
                e.Fail("HANDLE CONFUSION: " + op + " succeeded on joint " + handle + " the model considers destroyed; history:" + stale.History);
            SoakChecks.Handle(e, world, state, op, code);
        }

        private static bool IsBroken(SoakWorld world, ulong handle) =>
            NativeMethods.Aura_IsJointBroken(world.Handle, handle, out var flag) == SoakCodes.Success && flag != 0;

        public static void Control(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            var state = Pick(e, world, out var h);
            var w = world.Handle;
            var op = rng.Next(10);
            string label;
            int code;
            switch (op)
            {
                case 0:
                    {
                        label = "HasJoint";
                        e.Begin(label + " " + world + " " + h);
                        code = e.End(NativeMethods.Aura_HasJoint(w, h, out var has));
                        SoakChecks.World(e, world, label, code);
                        e.Mix(has);
                        if (code == SoakCodes.Success)
                        {
                            if (state == SoakHandleState.Dead && has != 0)
                                e.Fail("HANDLE CONFUSION: HasJoint reports a stale or never issued joint handle " + h + " alive in " + world);
                            if (state == SoakHandleState.Live && has == 0 && IsBroken(world, h))
                            {
                                SoakSettings.BrokenJointRejections++;
                                if (world.JointByHandle.TryGetValue(h, out var brokenJoint))
                                    brokenJoint.Mark(SoakHandleState.Unknown, "L286", e.OpIndex);
                            }
                            else if (state == SoakHandleState.Live && has == 0)
                                e.Fail("HANDLE LOST: HasJoint reports live joint " + h + " missing in " + world);
                            if (state == SoakHandleState.Unknown && world.JointByHandle.TryGetValue(h, out var joint))
                                if (has != 0)
                                    joint.Mark(SoakHandleState.Live, "has", e.OpIndex);
                                else if (!IsBroken(world, h))
                                    joint.Mark(SoakHandleState.Dead, "has0", e.OpIndex);
                        }

                        return;
                    }
                case 1:
                    {
                        label = "SetJointMotor";
                        var motor = new NativeJointMotorDesc
                        {
                            Mode = !SoakSettings.Bad(rng, 0.05f) ? rng.Range(0, 2) : rng.Range(-1, 5),
                            Target = SoakValues.Float(rng, -6f, 6f),
                            MaxForce = rng.Chance(0.1f) ? 0f : SoakValues.Float(rng, 0f, 80f),
                            SpringFrequency = SoakValues.Float(rng, 0f, 6f),
                            SpringDamping = SoakValues.Float(rng, 0f, 1f),
                        };
                        e.Begin(label + " " + world + " " + h + " mode=" + motor.Mode);
                        code = NativeMethods.Aura_SetJointMotor(w, h, ref motor);
                        break;
                    }
                case 2:
                    label = "SetJointLimits";
                    e.Begin(label + " " + world + " " + h);
                    code = NativeMethods.Aura_SetJointLimits(w, h, (byte)rng.Range(0, 1), SoakValues.Float(rng, -3.5f, 0.2f), SoakValues.Float(rng, -0.2f, 3.5f));
                    break;
                case 3:
                    label = "SetJointBreakThreshold";
                    e.Begin(label + " " + world + " " + h);
                    code = NativeMethods.Aura_SetJointBreakThreshold(w, h, SoakValues.Float(rng, -1f, 400f), SoakValues.Float(rng, -1f, 400f));
                    break;
                case 4:
                    {
                        label = "IsJointBroken";
                        e.Begin(label + " " + world + " " + h);
                        code = NativeMethods.Aura_IsJointBroken(w, h, out var broken);
                        e.Mix(broken);
                        break;
                    }
                case 5:
                    {
                        label = "GetJointFeedback";
                        e.Begin(label + " " + world + " " + h);
                        code = NativeMethods.Aura_GetJointFeedback(w, h, out var fb);
                        if (code == SoakCodes.Success)
                        {
                            if (SoakSettings.ContinueOnCorruption && !world.Tainted
                                && !SoakChecks.IsFinite(fb.Force + fb.Torque + fb.MotorLoad + fb.Position))
                            {
                                var type = world.JointByHandle.TryGetValue(h, out var fbJoint) ? fbJoint.Type : -1;
                                SoakSettings.Record("fb" + e.Index, "NON-FINITE joint feedback (force=" + fb.Force + " torque=" + fb.Torque + " motorLoad=" + fb.MotorLoad
                                    + " position=" + fb.Position + ") joint " + h + " type " + type + " in " + world + " episode " + e.Index + " op " + e.OpIndex
                                    + " (reproduce: soak 0 <seed> " + e.Index + ")");
                                world.Tainted = true;
                                break;
                            }

                            SoakChecks.Finite(e, world, "joint feedback force", fb.Force);
                            SoakChecks.Finite(e, world, "joint feedback torque", fb.Torque);
                            SoakChecks.Finite(e, world, "joint feedback motorLoad", fb.MotorLoad);
                            SoakChecks.Finite(e, world, "joint feedback position", fb.Position);
                            e.Mix(SoakDigest.MixFloat(SoakDigest.MixFloat(0, fb.Force), fb.Position));
                        }

                        break;
                    }
                case 6:
                    {
                        label = "SetJointAxisLimits";
                        var limit = Axis(rng);
                        var axis = (uint)(rng.Chance(0.95f) ? rng.Range(0, 5) : rng.Range(6, 12));
                        e.Begin(label + " " + world + " " + h + " axis=" + axis);
                        code = NativeMethods.Aura_SetJointAxisLimits(w, h, axis, ref limit);
                        break;
                    }
                case 7:
                    {
                        label = "SetJointAxisMotor";
                        var motor = new NativeJointMotorDesc
                        {
                            Mode = !SoakSettings.Bad(rng, 0.05f) ? rng.Range(0, 2) : rng.Range(-1, 5),
                            Target = SoakValues.Float(rng, -3f, 3f),
                            MaxForce = SoakValues.Float(rng, 0f, 60f),
                            SpringFrequency = SoakValues.Float(rng, 0f, 6f),
                            SpringDamping = SoakValues.Float(rng, 0f, 1f),
                        };
                        var axis = (uint)(rng.Chance(0.95f) ? rng.Range(0, 5) : rng.Range(6, 12));
                        e.Begin(label + " " + world + " " + h + " axis=" + axis);
                        code = NativeMethods.Aura_SetJointAxisMotor(w, h, axis, ref motor);
                        break;
                    }
                case 8:
                    label = "SetJointTarget";
                    e.Begin(label + " " + world + " " + h);
                    code = NativeMethods.Aura_SetJointTarget(w, h, SoakValues.Vec(rng, 10f));
                    break;
                default:
                    label = "SetJointBreakThreshold";
                    e.Begin(label + " " + world + " " + h);
                    code = NativeMethods.Aura_SetJointBreakThreshold(w, h, 0f, 0f);
                    break;
            }

            e.End(code);
            Resolve(e, world, h, state, label, code);
        }
    }
}
