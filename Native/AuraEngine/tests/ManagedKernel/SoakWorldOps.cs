using System;
using System.Collections.Generic;
using System.Globalization;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal static class SoakWorldOps
    {
        public static void Create(SoakEpisode e)
        {
            if (e.Worlds.Count >= 4)
                return;
            var rng = e.Rng;
            var is2D = rng.Chance(0.45f);
            var serial = e.NextWorldSerial();
            var masks = new ulong[rng.Chance(0.3f) ? rng.Range(0, 70) : 0];
            for (var i = 0; i < masks.Length; i++)
                masks[i] = rng.Chance(0.5f) ? ulong.MaxValue : rng.NextU64();
            using (var scope = new SoakNativeScope())
            {
                var desc = new NativeWorldDesc
                {
                    Mode = SoakSettings.Bad(rng, 0.02f) ? 7 : is2D ? 1 : 0,
                    Gravity = is2D ? new NativeVector3 { Y = -9.81f * rng.Range(0f, 2f) } : new NativeVector3 { Y = -9.81f * rng.Range(0f, 2f), X = rng.Range(-2f, 2f) },
                    InitialBodyCapacity = (uint)rng.Pick(new[] { 0, 1, 4, 16, 64, 1000 }),
                    FixedDeltaTime = rng.Chance(0.9f) ? 1f / 60f : SoakValues.Float(rng, 0f, 0.1f),
                    CollisionMasks = masks.Length == 0 ? IntPtr.Zero : scope.Copy(masks),
                    CollisionMaskCount = (uint)masks.Length,
                };
                e.Begin("CreateWorld #" + serial + " mode=" + desc.Mode + " masks=" + masks.Length);
                var code = NativeMethods.Aura_CreateWorld(ref desc, out var handle);
                e.End(code);
                if (handle.Opaque == 0)
                    return;
                if (code != SoakCodes.Success)
                    e.Fail("CreateWorld returned code " + code + " but a non-null world");
                var world = new SoakWorld
                {
                    Serial = serial,
                    Is2D = desc.Mode == 1,
                    Handle = handle,
                    BodyCap = rng.Chance(0.2f) ? 250 : 60,
                };
                e.Worlds.Add(world);
            }
        }

        public static void Destroy(SoakEpisode e, SoakWorld world)
        {
            e.Begin("DestroyWorld " + world);
            var code = e.End(NativeMethods.Aura_DestroyWorld(world.Handle));
            SoakChecks.World(e, world, "DestroyWorld", code);
            e.Worlds.Remove(world);
        }

        public static void Step(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            float dt;
            var roll = rng.Next(100);
            if (roll < 40) dt = 1f / 60f;
            else if (roll < 55) dt = 1f / 120f;
            else if (roll < 65) dt = 1f / 30f;
            else if (roll < 85) dt = rng.Range(0.0001f, 0.1f);
            else if (roll < 90) dt = SoakSettings.TolerateKnown ? 0.001f : 0f;
            else if (roll < 94) dt = SoakSettings.TolerateKnown ? 0.1f : 0.25f;
            else dt = SoakSettings.Bad(rng, 1f) ? SoakValues.Float(rng, -1f, 0.5f) : rng.Range(0.001f, 0.05f);
            e.Tick++;
            CapturePreStep(world, dt);
            e.Begin("Step " + world + " dt=" + dt.ToString("R", CultureInfo.InvariantCulture));
            var code = e.End(NativeMethods.Aura_Step(world.Handle, e.Tick, dt));
            SoakChecks.World(e, world, "Step", code);
            Audit(e, world);
        }

        public static void Gravity(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            var g = SoakValues.Vec(rng, 30f);
            e.Begin("SetWorldGravity " + world + " " + g.X + "," + g.Y + "," + g.Z);
            SoakChecks.World(e, world, "SetWorldGravity", e.End(NativeMethods.Aura_SetWorldGravity(world.Handle, g)));
            e.Begin("GetWorldGravity " + world);
            var code = e.End(NativeMethods.Aura_GetWorldGravity(world.Handle, out var read));
            SoakChecks.World(e, world, "GetWorldGravity", code);
            if (code == SoakCodes.Success)
            {
                SoakChecks.Finite(e, world, "gravity", read);
                e.Mix(SoakDigest.MixFloat(SoakDigest.MixFloat(SoakDigest.MixFloat(0, read.X), read.Y), read.Z));
            }
        }

        /* CopyBodyStates, contacts, events and body count with undersized buffers and canaries. */
        public static void Buffers(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            var capacity = rng.Pick(new[] { 0, 1, 2, 5, 40 });
            switch (rng.Next(4))
            {
                case 0:
                    using (var buffer = new SoakGuardBuffer<NativeBodyState>(capacity))
                    {
                        e.Begin("CopyBodyStates " + world + " cap=" + capacity);
                        var code = e.End(NativeMethods.Aura_CopyBodyStates(world.Handle, buffer.Pointer, (uint)capacity, out var count));
                        SoakChecks.World(e, world, "CopyBodyStates", code);
                        buffer.Verify("CopyBodyStates");
                        if (code == SoakCodes.Success && count > capacity)
                            e.Fail("CopyBodyStates reported " + count + " entries for capacity " + capacity);
                        e.Mix(count);
                    }

                    break;
                case 1:
                    using (var buffer = new SoakGuardBuffer<NativeContact>(capacity))
                    {
                        e.Begin("CopyContacts " + world + " cap=" + capacity);
                        var code = e.End(NativeMethods.Aura_CopyContacts(world.Handle, buffer.Pointer, (uint)capacity, out var count));
                        SoakChecks.World(e, world, "CopyContacts", code);
                        buffer.Verify("CopyContacts");
                        if (code == SoakCodes.Success && count > capacity)
                            e.Fail("CopyContacts reported " + count + " entries for capacity " + capacity);
                        if (code == SoakCodes.Success)
                        {
                            for (var i = 0; i < Math.Min((int)count, capacity); i++)
                            {
                                var c = buffer[i];
                                SoakChecks.Finite(e, world, "contact.point", c.Point);
                                SoakChecks.Finite(e, world, "contact.normal", c.Normal);
                                SoakChecks.Finite(e, world, "contact.penetration", c.Penetration);
                                SoakChecks.Finite(e, world, "contact.impulse", c.Impulse);
                            }
                        }

                        e.Mix(count);
                    }

                    break;
                case 2:
                    using (var buffer = new SoakGuardBuffer<NativePhysicsEvent>(capacity))
                    {
                        e.Begin("CopyEvents " + world + " cap=" + capacity);
                        var code = e.End(NativeMethods.Aura_CopyEvents(world.Handle, buffer.Pointer, (uint)capacity, out var count));
                        SoakChecks.World(e, world, "CopyEvents", code);
                        buffer.Verify("CopyEvents");
                        if (code == SoakCodes.Success && count > capacity)
                            e.Fail("CopyEvents reported " + count + " entries for capacity " + capacity);
                        e.Mix(count);
                        if (code == SoakCodes.Success && Environment.GetEnvironmentVariable("AURA_SOAK_ORDER") == "1")
                        {
                            // Order-sensitive: exposes event/contact ordering that depends on worker thread timing.
                            for (var i = 0; i < Math.Min((int)count, capacity); i++)
                            {
                                var ev = buffer[i];
                                e.Mix(((ulong)(uint)ev.Type << 56) ^ SoakWorld.Key(ev.BodyA) ^ (SoakWorld.Key(ev.BodyB) * 31UL));
                            }
                        }
                    }

                    break;
                default:
                    e.Begin("PendingEventCount+WorldBodyCount " + world);
                    SoakChecks.World(e, world, "PendingEventCount", e.End(NativeMethods.Aura_PendingEventCount(world.Handle, out var pending)));
                    SoakChecks.World(e, world, "WorldBodyCount", NativeMethods.Aura_WorldBodyCount(world.Handle, out var bodies));
                    e.Mix(pending);
                    e.Mix(bodies);
                    break;
            }
        }

        private static void CapturePreStep(SoakWorld world, float dt)
        {
            world.PreStepDt = dt;
            if (NativeMethods.Aura_WorldBodyCount(world.Handle, out var count) != SoakCodes.Success)
                return;
            var capacity = (int)count + 4;
            using (var buffer = new SoakGuardBuffer<NativeBodyState>(capacity))
            {
                if (NativeMethods.Aura_CopyBodyStates(world.Handle, buffer.Pointer, (uint)capacity, out var written) != SoakCodes.Success)
                    return;
                world.PreStep = new NativeBodyState[Math.Min((int)written, capacity)];
                for (var i = 0; i < world.PreStep.Length; i++)
                    world.PreStep[i] = buffer[i];
            }
        }

        private static string PreStepState(SoakWorld world)
        {
            var text = new System.Text.StringBuilder("pre-step (dt=" + world.PreStepDt + ") states:\n");
            foreach (var s in world.PreStep)
                text.Append("  " + SoakBodyOps.Name(s.Body) + " pos=" + s.Pose.Position.X + "," + s.Pose.Position.Y + " rotZ=" + s.Pose.Rotation.Z + "," + s.Pose.Rotation.W
                    + " lin=" + s.LinearVelocity.X + "," + s.LinearVelocity.Y + " ang=" + s.AngularVelocity.Z + " awake=" + s.IsAwake + " flags=" + s.Flags + "\n");
            return text.ToString();
        }

        /* Joints and dynamic bodies of a world, appended to non-finite reports to ease diagnosis. */
        private static string Describe(SoakWorld world)
        {
            var text = new System.Text.StringBuilder();
            text.Append("joints:");
            foreach (var joint in world.Joints)
                if (joint.State != SoakHandleState.Dead)
                    text.Append(" [h" + joint.Handle + " type" + joint.Type + " " + SoakBodyOps.Name(joint.BodyA) + "-" + SoakBodyOps.Name(joint.BodyB) + " " + joint.State + "]");
            text.Append("\nbodies:");
            foreach (var body in world.Bodies)
                if (body.State != SoakHandleState.Dead)
                    text.Append(" [" + SoakBodyOps.Name(body.Handle) + " t" + body.Type + (body.Disabled ? " disabled" : string.Empty) + (body.StaticOnly ? " staticOnly" : string.Empty) + " " + body.Description + "]\n");
            text.Append("\n");
            return text.ToString();
        }

        /* Cross-checks the whole body list against the model and fingerprints the state. Not an operation. */
        public static void Audit(SoakEpisode e, SoakWorld world)
        {
            var codeCount = NativeMethods.Aura_WorldBodyCount(world.Handle, out var bodyCount);
            SoakChecks.World(e, world, "WorldBodyCount", codeCount);
            var capacity = (int)bodyCount + 8;
            using (var buffer = new SoakGuardBuffer<NativeBodyState>(capacity))
            {
                var code = NativeMethods.Aura_CopyBodyStates(world.Handle, buffer.Pointer, (uint)capacity, out var count);
                SoakChecks.World(e, world, "CopyBodyStates(audit)", code);
                buffer.Verify("CopyBodyStates(audit)");
                if (code != SoakCodes.Success)
                    return;
                if (count > capacity)
                    e.Fail("audit: CopyBodyStates reported " + count + " for capacity " + capacity);
                var seen = new HashSet<ulong>();
                for (var i = 0; i < count; i++)
                {
                    var s = buffer[i];
                    var key = SoakWorld.Key(s.Body);
                    if (!seen.Add(key))
                        e.Fail("HANDLE CONFUSION: duplicate body handle " + s.Body.Index + "/" + s.Body.Generation + " in CopyBodyStates of " + world);
                    world.BodyByHandle.TryGetValue(key, out var model);
                    if (model != null && model.State == SoakHandleState.Dead)
                        e.Fail("HANDLE CONFUSION: destroyed body " + s.Body.Index + "/" + s.Body.Generation + " still listed by CopyBodyStates in " + world);
                    var sum = s.Pose.Position.X + s.Pose.Position.Y + s.Pose.Position.Z + s.LinearVelocity.X + s.LinearVelocity.Y + s.LinearVelocity.Z
                        + s.AngularVelocity.X + s.AngularVelocity.Y + s.AngularVelocity.Z + s.Pose.Rotation.X + s.Pose.Rotation.Y + s.Pose.Rotation.Z + s.Pose.Rotation.W;
                    var exploded = Math.Abs(s.Pose.Position.X) > 1e6f || Math.Abs(s.Pose.Position.Y) > 1e6f || Math.Abs(s.Pose.Position.Z) > 1e6f
                        || Math.Abs(s.LinearVelocity.X) > 1e6f || Math.Abs(s.LinearVelocity.Y) > 1e6f || Math.Abs(s.LinearVelocity.Z) > 1e6f
                        || Math.Abs(s.AngularVelocity.X) > 1e6f || Math.Abs(s.AngularVelocity.Y) > 1e6f || Math.Abs(s.AngularVelocity.Z) > 1e6f;
                    if (!world.Tainted && (!SoakChecks.IsFinite(sum) || exploded) && SoakSettings.ContinueOnCorruption)
                    {
                        SoakSettings.Record("ep" + e.Index + "/" + (exploded ? "boom" : "nan"),
                            (exploded ? "EXPLODED" : "NON-FINITE") + " body " + SoakBodyOps.Name(s.Body) + " in " + world + " episode " + e.Index + " op " + e.OpIndex
                            + " (reproduce: soak 0 <seed> " + e.Index + ")");
                        world.Tainted = true;
                        return;
                    }

                    if (!world.Tainted && (!SoakChecks.IsFinite(sum) || exploded))
                        e.Fail(Describe(world) + PreStepState(world) + (exploded ? "EXPLODED (|value| > 1e6) state of body " : "non-finite state of body ") + s.Body.Index + "/" + s.Body.Generation + " (model type " + (model != null ? model.Type.ToString() : "?") + ") in " + world
                            + ": pos=" + s.Pose.Position.X + "," + s.Pose.Position.Y + "," + s.Pose.Position.Z + " rot=" + s.Pose.Rotation.X + "," + s.Pose.Rotation.Y + "," + s.Pose.Rotation.Z + "," + s.Pose.Rotation.W
                            + " lin=" + s.LinearVelocity.X + "," + s.LinearVelocity.Y + "," + s.LinearVelocity.Z + " ang=" + s.AngularVelocity.X + "," + s.AngularVelocity.Y + "," + s.AngularVelocity.Z);
                    if (SoakSettings.Trace && !e.Suppress)
                        e.Trace.Add("after op " + e.OpIndex + " " + world + " body " + s.Body.Index + "/" + s.Body.Generation + " pos=" + s.Pose.Position.X.ToString("R") + "," + s.Pose.Position.Y.ToString("R") + "," + s.Pose.Position.Z.ToString("R")
                            + " rot=" + s.Pose.Rotation.X.ToString("R") + "," + s.Pose.Rotation.Y.ToString("R") + "," + s.Pose.Rotation.Z.ToString("R") + "," + s.Pose.Rotation.W.ToString("R")
                            + " lin=" + s.LinearVelocity.X.ToString("R") + "," + s.LinearVelocity.Y.ToString("R") + "," + s.LinearVelocity.Z.ToString("R")
                            + " ang=" + s.AngularVelocity.X.ToString("R") + "," + s.AngularVelocity.Y.ToString("R") + "," + s.AngularVelocity.Z.ToString("R") + " awake=" + s.IsAwake + " flags=" + s.Flags);
                    e.Mix(SoakDigest.MixFloat(SoakDigest.MixFloat(SoakDigest.MixFloat(key, s.Pose.Position.X), s.Pose.Position.Y), s.Pose.Position.Z));
                    e.Mix(SoakDigest.MixFloat(SoakDigest.MixFloat(SoakDigest.MixFloat(s.Flags, s.LinearVelocity.X), s.LinearVelocity.Y), s.LinearVelocity.Z));
                    e.Mix(SoakDigest.MixFloat(SoakDigest.MixFloat(SoakDigest.MixFloat(s.IsAwake, s.Pose.Rotation.X), s.Pose.Rotation.W), s.AngularVelocity.Y));
                }

                foreach (var body in world.Bodies)
                {
                    if (body.State == SoakHandleState.Live && !seen.Contains(SoakWorld.Key(body.Handle)))
                        e.Fail("HANDLE LOST: live body " + body.Handle.Index + "/" + body.Handle.Generation + " missing from CopyBodyStates in " + world);
                }
            }

            if (NativeMethods.Aura_ComputeStateHash(world.Handle, out var hash) == SoakCodes.Success)
                e.Mix(hash);
            SoakCharacterOps.Audit(e, world);
        }
    }
}
