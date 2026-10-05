using System;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal static class SoakQueryOps
    {
        public static void Run(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            var kind = rng.Next(14);
            var capacity = rng.Pick(new[] { 0, 1, 2, 3, 8 });
            var filter = SoakValues.Filter(rng, world);
            var w = world.Handle;
            var center = SoakValues.Vec(rng, 15f);
            var dir = SoakValues.UnitVec(rng);
            var maxDistance = SoakValues.Float(rng, 0f, 60f);
            var radius = SoakValues.Float(rng, 0f, 3f);
            var half = SoakValues.PositiveVec(rng, 0.1f, 2f);
            var rotation = SoakValues.Quat(rng);
            var second = SoakValues.Vec(rng, 15f);
            if (world.Is2D)
            {
                center.Z = 0f;
                second.Z = 0f;
            }

            using (var scope = new SoakNativeScope())
            using (var buffer = new SoakGuardBuffer<NativeQueryHit>(capacity, SoakSettings.TolerateKnown && world.Is2D ? 300 : 0))
            {
                var shape = SoakShapes.Make(rng, scope, world.Is2D);
                var pose = new NativePose { Position = center, Rotation = rotation };
                var ray = new NativeRay { Origin = center, Direction = dir };
                string label;
                int code;
                uint count = 0;
                byte hasHit = 0;
                NativeQueryHit single = default;
                var isSingle = false;
                switch (kind)
                {
                    case 0: label = "Raycast"; isSingle = true; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_Raycast(w, ref ray, maxDistance, ref filter, out single, out hasHit); break;
                    case 1: label = "RaycastAll"; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_RaycastAll(w, ref ray, maxDistance, ref filter, buffer.Pointer, (uint)capacity, out count); break;
                    case 2: label = "OverlapSphere"; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_OverlapSphere(w, center, radius, ref filter, buffer.Pointer, (uint)capacity, out count); break;
                    case 3: label = "OverlapPoint"; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_OverlapPoint(w, center, ref filter, buffer.Pointer, (uint)capacity, out count); break;
                    case 4: label = "OverlapBox"; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_OverlapBox(w, center, half, rotation, ref filter, buffer.Pointer, (uint)capacity, out count); break;
                    case 5: label = "OverlapCapsule"; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_OverlapCapsule(w, center, second, radius, ref filter, buffer.Pointer, (uint)capacity, out count); break;
                    case 6: label = "OverlapShape"; e.Begin(Text(label, world, capacity) + " shape=" + shape.Type); code = NativeMethods.Aura_OverlapShape(w, ref shape, ref pose, ref filter, buffer.Pointer, (uint)capacity, out count); break;
                    case 7: label = "SphereCast"; isSingle = true; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_SphereCast(w, center, radius, dir, maxDistance, ref filter, out single, out hasHit); break;
                    case 8: label = "CapsuleCast"; isSingle = true; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_CapsuleCast(w, center, second, radius, dir, maxDistance, ref filter, out single, out hasHit); break;
                    case 9: label = "BoxCast"; isSingle = true; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_BoxCast(w, center, half, rotation, dir, maxDistance, ref filter, out single, out hasHit); break;
                    case 10: label = "ShapeCast"; isSingle = true; e.Begin(Text(label, world, capacity) + " shape=" + shape.Type); code = NativeMethods.Aura_ShapeCast(w, ref shape, ref pose, dir, maxDistance, ref filter, out single, out hasHit); break;
                    case 11: label = "OverlapSphere"; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_OverlapSphere(w, center, 50f, ref filter, buffer.Pointer, (uint)capacity, out count); break;
                    case 12: label = "RaycastAll"; e.Begin(Text(label, world, capacity)); ray.Origin = new NativeVector3 { Y = 30f }; ray.Direction = new NativeVector3 { Y = -1f }; code = NativeMethods.Aura_RaycastAll(w, ref ray, 100f, ref filter, buffer.Pointer, (uint)capacity, out count); break;
                    default: label = "OverlapBox"; e.Begin(Text(label, world, capacity)); code = NativeMethods.Aura_OverlapBox(w, default, new NativeVector3 { X = 30f, Y = 30f, Z = 30f }, new NativeQuaternion { W = 1f }, ref filter, buffer.Pointer, (uint)capacity, out count); break;
                }

                e.End(code);
                SoakChecks.World(e, world, label, code);
                buffer.Verify(label);
                var overran = buffer.WroteIntoSlack();
                if (overran)
                    SoakSettings.KnownOverruns++;
                if (code != SoakCodes.Success)
                    return;
                if (isSingle)
                {
                    e.Mix(hasHit);
                    if (hasHit != 0)
                        CheckHit(e, world, label, single);
                    return;
                }

                if (count > capacity && !overran)
                    e.Fail(label + " reported " + count + " hits for capacity " + capacity);
                e.Mix(count);
                for (var i = 0; i < Math.Min((int)count, capacity); i++)
                    CheckHit(e, world, label, buffer[i]);
            }
        }

        private static string Text(string label, SoakWorld world, int capacity) => label + " " + world + " cap=" + capacity;

        private static void CheckHit(SoakEpisode e, SoakWorld world, string label, NativeQueryHit hit)
        {
            var key = SoakWorld.Key(hit.Body);
            if (!world.Tainted && world.BodyByHandle.TryGetValue(key, out var model) && model.State == SoakHandleState.Dead)
                e.Fail("HANDLE CONFUSION: " + label + " returned destroyed body " + hit.Body.Index + "/" + hit.Body.Generation + " in " + world);
            SoakChecks.Finite(e, world, label + " distance", hit.Distance);
            SoakChecks.Finite(e, world, label + " point", hit.Point);
            SoakChecks.Finite(e, world, label + " normal", hit.Normal);
            e.Mix(SoakDigest.MixFloat(key, hit.Distance));
        }
    }
}
