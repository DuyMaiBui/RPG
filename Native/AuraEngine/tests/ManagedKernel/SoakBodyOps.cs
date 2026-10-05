using System;
using System.Collections.Generic;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal static class SoakBodyOps
    {
        /* Picks a handle to act on: usually a live body, otherwise a stale one, a garbage one or a null one.
           The returned state is what the model expects (never issued handles count as Dead). */
        public static SoakHandleState Pick(SoakEpisode e, SoakWorld world, float liveChance, out NativeBodyHandle handle)
        {
            var rng = e.Rng;
            var roll = rng.Unit();
            if (roll < liveChance && world.Bodies.Count > 0)
            {
                var body = world.Bodies[rng.Next(world.Bodies.Count)];
                handle = body.Handle;
                return body.State;
            }

            if (roll < liveChance + 0.15f && world.Bodies.Count > 0)
            {
                var body = world.Bodies[rng.Next(world.Bodies.Count)];
                handle = body.Handle;
                return body.State;
            }

            switch (rng.Next(4))
            {
                case 0:
                    handle = new NativeBodyHandle { Index = uint.MaxValue, Generation = uint.MaxValue };
                    break;
                case 1:
                    handle = default;
                    break;
                case 2:
                    handle = new NativeBodyHandle { Index = (uint)rng.Range(0, 400), Generation = (uint)rng.Range(0, 6) };
                    break;
                default:
                    handle = new NativeBodyHandle { Index = (uint)rng.Range(0, 400), Generation = uint.MaxValue };
                    break;
            }

            return world.BodyByHandle.TryGetValue(SoakWorld.Key(handle), out var known) ? known.State : SoakHandleState.Dead;
        }

        public static string Name(NativeBodyHandle h) => h.Index + "/" + h.Generation;

        public static void Create(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            if (world.Bodies.Count - CountDead(world) >= world.BodyCap)
            {
                Destroy(e, world);
                return;
            }

            using (var scope = new SoakNativeScope())
            {
                var shapes = new NativeShapeDesc[rng.Chance(0.03f) ? 0 : rng.Range(1, 3)];
                for (var i = 0; i < shapes.Length; i++)
                    shapes[i] = SoakShapes.Make(rng, scope, world.Is2D);
                var roll = rng.Next(100);
                var bodyType = roll < 20 ? 0 : roll < 80 ? 1 : roll < 98 || !SoakSettings.Bad(rng, 1f) ? 2 : 5;
                var pose = SoakValues.Pose(rng, 15f, world.Is2D);
                if (rng.Chance(0.6f))
                    pose.Position.Y = Math.Abs(pose.Position.Y) + 0.5f;
                var desc = new NativeBodyDesc
                {
                    Type = bodyType,
                    Layer = (uint)(!SoakSettings.Bad(rng, 0.03f) ? rng.Range(0, 7) : rng.Range(32, 200)),
                    CollisionMask = rng.Chance(0.6f) ? ulong.MaxValue : rng.NextU64(),
                    GroupIndex = rng.Chance(0.8f) ? 0 : rng.Range(-3, 3),
                    Mass = SoakValues.Float(rng, 0.1f, 10f),
                    GravityScale = SoakValues.Float(rng, -1f, 2f),
                    Friction = SoakValues.Float(rng, 0f, 1f),
                    Restitution = SoakValues.Float(rng, 0f, 1f),
                    Density = SoakValues.Float(rng, 0.1f, 5f),
                    InitialPose = pose,
                    InitialLinearVelocity = rng.Chance(0.5f) ? default : SoakValues.Vec(rng, 10f),
                    InitialAngularVelocity = rng.Chance(0.7f) ? default : SoakValues.Vec(rng, 5f),
                    Shapes = shapes.Length == 0 ? IntPtr.Zero : scope.Copy(shapes),
                    ShapeCount = (uint)shapes.Length,
                    LinearDamping = rng.Chance(0.5f) ? 0f : SoakValues.Float(rng, 0f, 2f),
                    AngularDamping = rng.Chance(0.5f) ? 0f : SoakValues.Float(rng, 0f, 2f),
                    MaxLinearVelocity = rng.Chance(0.6f) ? 0f : SoakValues.Float(rng, 1f, 200f),
                    MaxAngularVelocity = rng.Chance(0.6f) ? 0f : SoakValues.Float(rng, 1f, 100f),
                    CenterOfMass = rng.Chance(0.7f) ? default : SoakValues.Vec(rng, 0.5f),
                    InertiaMultiplier = rng.Chance(0.7f) ? 1f : SoakValues.Float(rng, 0.1f, 4f),
                    FreezeFlags = rng.Chance(0.8f) ? 0u : (uint)rng.Range(0, 255),
                    CollisionDetection = !SoakSettings.Bad(rng, 0.05f) ? rng.Range(0, 1) : rng.Range(-2, 4),
                    AllowSleeping = (byte)(rng.Chance(0.8f) ? 1 : 0),
                };
                e.Begin("AttachBody " + world + " type=" + bodyType + " shapes=" + string.Join(",", Types(shapes)));
                var code = e.End(NativeMethods.Aura_AttachBody(world.Handle, default, ref desc, out var handle));
                SoakChecks.World(e, world, "AttachBody", code);
                if (code != SoakCodes.Success)
                    return;
                e.Mix(SoakWorld.Key(handle));
                e.Log.Append("handle=" + Name(handle));
                var key = SoakWorld.Key(handle);
                if (world.BodyByHandle.TryGetValue(key, out var existing))
                {
                    e.Fail(existing.State == SoakHandleState.Live
                        ? "HANDLE CONFUSION: AttachBody returned live handle " + Name(handle) + " again in " + world
                        : "HANDLE CONFUSION: AttachBody reissued destroyed handle " + Name(handle) + " in " + world);
                }

                var body = new SoakBody { Handle = handle, Type = bodyType, Description = Describe(desc, shapes) };
                foreach (var shape in shapes)
                    if (shape.Type == 5 || shape.Type == 6 || shape.Type == 9)
                        body.StaticOnly = true;
                world.Bodies.Add(body);
                world.BodyByHandle[key] = body;
            }
        }

        private static string Describe(NativeBodyDesc desc, NativeShapeDesc[] shapes)
        {
            var text = "pos=" + desc.InitialPose.Position.X + "," + desc.InitialPose.Position.Y + "," + desc.InitialPose.Position.Z + " mass=" + desc.Mass + " freeze=" + desc.FreezeFlags + " shapes:";
            foreach (var s in shapes)
                text += " (" + s.Type + " he=" + s.HalfExtents.X + "," + s.HalfExtents.Y + "," + s.HalfExtents.Z + " r=" + s.Radius + " h=" + s.Height + " top=" + s.TopRadius + " trig=" + s.IsTrigger + " oneway=" + s.IsOneWay + ")";
            return text;
        }

        private static IEnumerable<int> Types(NativeShapeDesc[] shapes)
        {
            foreach (var s in shapes)
                yield return s.Type;
        }

        private static int CountDead(SoakWorld world)
        {
            var n = 0;
            foreach (var b in world.Bodies)
                if (b.State == SoakHandleState.Dead)
                    n++;
            return n;
        }

        public static void Destroy(SoakEpisode e, SoakWorld world)
        {
            var state = Pick(e, world, 0.8f, out var handle);
            e.Begin("DestroyBody " + world + " " + Name(handle));
            var code = e.End(NativeMethods.Aura_DestroyBody(world.Handle, handle));
            SoakChecks.Handle(e, world, state, "DestroyBody", code);
            if (code != SoakCodes.Success)
                return;
            if (world.BodyByHandle.TryGetValue(SoakWorld.Key(handle), out var body))
                body.State = SoakHandleState.Dead;
            SoakJointOps.BodyDestroyed(world, handle, e.OpIndex);
            // Keep the model list bounded: dead entries stay in the map so stale calls remain checkable.
            if (world.Bodies.Count > world.BodyCap * 3)
                world.Bodies.RemoveAll(b => b.State == SoakHandleState.Dead && e.Rng.Chance(0.5f));
        }

        public static void Control(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            var state = Pick(e, world, 0.75f, out var h);
            var op = rng.Next(18);
            var w = world.Handle;
            var v = SoakValues.Vec(rng, rng.Chance(0.1f) && !SoakSettings.TolerateKnown ? 200f : 12f);
            var name = h.Index + "/" + h.Generation;
            string label;
            int code;
            byte enable = 1;
            switch (op)
            {
                case 0: label = "SetLinearVelocity"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_SetLinearVelocity(w, h, v); break;
                case 1: label = "SetAngularVelocity"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_SetAngularVelocity(w, h, v); break;
                case 2: label = "AddForce"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_AddForce(w, h, v); break;
                case 3: label = "AddImpulse"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_AddImpulse(w, h, v); break;
                case 4: label = "AddTorque"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_AddTorque(w, h, v); break;
                case 5: label = "AddAngularImpulse"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_AddAngularImpulse(w, h, v); break;
                case 6:
                    {
                        var pose = SoakValues.Pose(rng, 15f, world.Is2D);
                        label = "SetBodyPose";
                        e.Begin(label + " " + world + " " + name + " pos=" + pose.Position.X + "," + pose.Position.Y + "," + pose.Position.Z);
                        code = NativeMethods.Aura_SetBodyPose(w, h, ref pose, (byte)rng.Range(0, 1));
                        break;
                    }
                case 7: label = "SetGravityScale"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_SetGravityScale(w, h, SoakValues.Float(rng, -2f, 3f)); break;
                case 8: label = "SetFriction"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_SetFriction(w, h, SoakValues.Float(rng, -0.2f, 2f)); break;
                case 9: label = "SetRestitution"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_SetRestitution(w, h, SoakValues.Float(rng, -0.2f, 1.5f)); break;
                case 10: label = "SetMotionType"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_SetMotionType(w, h, !SoakSettings.Bad(rng, 0.05f) ? rng.Range(0, 2) : rng.Range(-1, 5)); break;
                case 11:
                    label = "SetBodyLayer";
                    e.Begin(label + " " + world + " " + name);
                    code = NativeMethods.Aura_SetBodyLayer(w, h, (uint)(!SoakSettings.Bad(rng, 0.05f) ? rng.Range(0, 7) : rng.Range(32, 200)), rng.NextU64());
                    break;
                case 12: label = "SetBodyEnabled"; enable = (byte)(rng.Chance(0.6f) ? 1 : 0); e.Begin(label + " " + world + " " + name + " enabled=" + enable); code = NativeMethods.Aura_SetBodyEnabled(w, h, enable); break;
                case 13: label = "IsBodyEnabled"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_IsBodyEnabled(w, h, out _); break;
                case 14: label = "SetBodyCollisionDetection"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_SetBodyCollisionDetection(w, h, !SoakSettings.Bad(rng, 0.05f) ? rng.Range(0, 1) : rng.Range(-1, 3)); break;
                case 15:
                    {
                        var pose = SoakValues.Pose(rng, 15f, world.Is2D);
                        label = "SetKinematicTarget";
                        if (SoakSettings.TolerateKnown && !world.Is2D && world.BodyByHandle.TryGetValue(SoakWorld.Key(h), out var target) && target.StaticOnly)
                        {
                            // Known crash: Aura_SetKinematicTarget on a mesh/height field/plane body dereferences null motion properties.
                            e.Begin(label + "(skipped,static-only) " + world + " " + name);
                            code = SoakCodes.InvalidDefinition;
                            break;
                        }

                        e.Begin(label + " " + world + " " + name);
                        code = NativeMethods.Aura_SetKinematicTarget(w, h, ref pose);
                        break;
                    }
                case 16: label = "SetSurfaceVelocity"; e.Begin(label + " " + world + " " + name); code = NativeMethods.Aura_SetSurfaceVelocity(w, h, v); break;
                default:
                    {
                        label = "GetBodyState";
                        e.Begin(label + " " + world + " " + name);
                        code = NativeMethods.Aura_GetBodyState(w, h, out var s);
                        e.End(code);
                        SoakChecks.Handle(e, world, state, label, code);
                        if (code == SoakCodes.Success)
                        {
                            if (s.Body.Index != h.Index || s.Body.Generation != h.Generation)
                                e.Fail("HANDLE CONFUSION: GetBodyState(" + name + ") returned the state of " + Name(s.Body));
                            SoakChecks.Finite(e, world, "body pose", s.Pose);
                            SoakChecks.Finite(e, world, "body linearVelocity", s.LinearVelocity);
                            SoakChecks.Finite(e, world, "body angularVelocity", s.AngularVelocity);
                        }

                        return;
                    }
            }

            e.End(code);
            SoakChecks.Handle(e, world, state, label, code);
            if (op == 12 && code == SoakCodes.Success && world.BodyByHandle.TryGetValue(SoakWorld.Key(h), out var toggled))
                toggled.Disabled = enable == 0;
        }
    }
}
