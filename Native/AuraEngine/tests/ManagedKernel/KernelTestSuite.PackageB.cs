using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package B: Box2D queries and joints (wheel, mouse, rope). */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageBTests()
        {
            return new (string Name, Action Body)[]
            {
                ("b2_overlap_point", B2_OverlapPoint),
                ("b2_overlap_box_rotated", B2_OverlapBoxRotated),
                ("b2_overlap_capsule", B2_OverlapCapsule),
                ("b2_overlap_sphere_and_shape", B2_OverlapSphereAndShape),
                ("b2_native_shape_entry_points", B2_NativeShapeEntryPoints),
                ("b2_overlap_capacity_and_dedupe", B2_OverlapCapacityAndDedupe),
                ("b2_overlap_filters", B2_OverlapFilters),
                ("b2_casts_hit_expected_body", B2_CastsHitExpectedBody),
                ("b2_cast_filters", B2_CastFilters),
                ("b2_query_invalid_and_stale", B2_QueryInvalidAndStale),
                ("b2_wheel_motor_drives_wheel", B2_WheelMotorDrivesWheel),
                ("b2_wheel_car_drives_forward", B2_WheelCarDrivesForward),
                ("b2_wheel_limits_and_control", B2_WheelLimitsAndControl),
                ("b2_mouse_joint_pulls_body", B2_MouseJointPullsBody),
                ("b2_mouse_joint_invalid_and_stale", B2_MouseJointInvalidAndStale),
                ("b2_rope_limits_length_only", B2_RopeLimitsLengthOnly),
                ("b2_new_joints_unsupported_in_3d", B2_NewJointsUnsupportedIn3D),
            };
        }

        private static readonly AuraPhysicsLayer B2Layer3 = new AuraPhysicsLayer(3);

        private static PhysicsBodyId B2Static(AuraSimulationWorld world, float x, float y, AuraPhysicsShapeDefinition shape) =>
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(x, y, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, shape));

        private static AuraPhysicsShapeDefinition B2Capsule(float radius, float height) =>
            new AuraPhysicsShapeDefinition(AuraShapeType.Capsule, AuraPose.Identity, false, AuraPhysicsMaterialDefinition.Default, AuraPhysicsLayer.Default, AuraShapeGeometry.Capsule(radius, height));

        private sealed class B2Scene
        {
            public PhysicsBodyId Box;      /* (0,0) half 1x1 */
            public PhysicsBodyId Circle;   /* (6,0) r 1 */
            public PhysicsBodyId Capsule;  /* (12,0) r 0.5, height 3 (vertical) */
            public PhysicsBodyId Trigger;  /* (18,0) half 1 sensor */
            public PhysicsBodyId Layer3;   /* (24,0) half 1, shape layer 3 */
        }

        private static B2Scene B2Build(AuraSimulationWorld world) => new B2Scene
        {
            Box = B2Static(world, 0f, 0f, AuraPhysicsShapeDefinition.Box(V(1f, 1f, 1f))),
            Circle = B2Static(world, 6f, 0f, AuraPhysicsShapeDefinition.Sphere(1f)),
            Capsule = B2Static(world, 12f, 0f, B2Capsule(0.5f, 3f)),
            Trigger = B2Static(world, 18f, 0f, AuraPhysicsShapeDefinition.Box(V(1f, 1f, 1f)).AsTrigger()),
            Layer3 = B2Static(world, 24f, 0f, AuraPhysicsShapeDefinition.Box(V(1f, 1f, 1f)).WithLayer(B2Layer3)),
        };

        private static void B2ExpectOnly(AuraPhysicsQueryHit[] hits, int count, PhysicsBodyId expected, string label)
        {
            Check(count == 1, $"{label}: expected exactly 1 hit, got {count}.");
            Check(hits[0].Body == expected, $"{label}: hit {hits[0].Body}, expected {expected}.");
        }

        private static void B2_OverlapPoint()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var scene = B2Build(world);
            var hits = new AuraPhysicsQueryHit[8];
            B2ExpectOnly(hits, world.Physics.OverlapPoint(V(0.2f, 0.3f, 0f), AuraPhysicsQueryFilter.All, hits), scene.Box, "point in box");
            B2ExpectOnly(hits, world.Physics.OverlapPoint(V(6f, 0.9f, 0f), AuraPhysicsQueryFilter.All, hits), scene.Circle, "point in circle");
            B2ExpectOnly(hits, world.Physics.OverlapPoint(V(12f, 1.4f, 0f), AuraPhysicsQueryFilter.All, hits), scene.Capsule, "point in capsule cap");
            Check(world.Physics.OverlapPoint(V(3f, 0f, 0f), AuraPhysicsQueryFilter.All, hits) == 0, "point in empty space must miss.");
            Check(world.Physics.OverlapPoint(V(0f, 1.3f, 0f), AuraPhysicsQueryFilter.All, hits) == 0, "point just above the box must miss.");
        }

        private static void B2_OverlapBoxRotated()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var scene = B2Build(world);
            var hits = new AuraPhysicsQueryHit[8];
            var thin = V(1.5f, 0.1f, 0.5f);
            Check(world.Physics.OverlapBox(V(0f, 2.2f, 0f), thin, AuraQuaternion.Identity, AuraPhysicsQueryFilter.All, hits) == 0, "flat box above must miss.");
            var quarter = AuraQuaternion.FromAxisAngle(AuraVector3.UnitZ, MathF.PI * 0.5f);
            B2ExpectOnly(hits, world.Physics.OverlapBox(V(0f, 2.2f, 0f), thin, quarter, AuraPhysicsQueryFilter.All, hits), scene.Box, "box rotated 90 degrees");
            B2ExpectOnly(hits, world.Physics.OverlapBox(V(6.5f, 0f, 0f), V(0.4f, 0.4f, 0.5f), AuraQuaternion.Identity, AuraPhysicsQueryFilter.All, hits), scene.Circle, "box on circle");
            Check(world.Physics.OverlapBox(V(3f, 0f, 0f), V(0.5f, 0.5f, 0.5f), AuraQuaternion.Identity, AuraPhysicsQueryFilter.All, hits) == 0, "box in gap must miss.");
        }

        private static void B2_OverlapCapsule()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var scene = B2Build(world);
            var hits = new AuraPhysicsQueryHit[8];
            B2ExpectOnly(hits, world.Physics.OverlapCapsule(V(6f, -3f, 0f), V(6f, -0.8f, 0f), 0.5f, AuraPhysicsQueryFilter.All, hits), scene.Circle, "capsule under circle");
            Check(world.Physics.OverlapCapsule(V(3f, -1f, 0f), V(3f, 1f, 0f), 0.5f, AuraPhysicsQueryFilter.All, hits) == 0, "capsule in gap must miss.");
            B2ExpectOnly(hits, world.Physics.OverlapCapsule(V(12f, 2f, 0f), V(13f, 2f, 0f), 0.6f, AuraPhysicsQueryFilter.All, hits), scene.Capsule, "horizontal capsule on capsule top");
        }

        private static void B2_OverlapSphereAndShape()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var scene = B2Build(world);
            var hits = new AuraPhysicsQueryHit[8];
            B2ExpectOnly(hits, world.Physics.OverlapSphere(V(1.3f, 0f, 0f), 0.5f, AuraPhysicsQueryFilter.All, hits), scene.Box, "circle overlap");
            Check(world.Physics.OverlapSphere(V(3f, 0f, 0f), 0.5f, AuraPhysicsQueryFilter.All, hits) == 0, "circle in gap must miss.");

            var box = AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f));
            B2ExpectOnly(hits, world.Physics.OverlapShape(box, new AuraPose(V(12.9f, 1.6f, 0f), AuraQuaternion.Identity), AuraPhysicsQueryFilter.All, hits), scene.Capsule, "box shape");
            var sphere = AuraPhysicsShapeDefinition.Sphere(0.4f);
            B2ExpectOnly(hits, world.Physics.OverlapShape(sphere, new AuraPose(V(4.7f, 0f, 0f), AuraQuaternion.Identity), AuraPhysicsQueryFilter.All, hits), scene.Circle, "sphere shape");
            B2ExpectOnly(hits, world.Physics.OverlapShape(B2Capsule(0.3f, 2f), new AuraPose(V(-1.2f, 0f, 0f), AuraQuaternion.Identity), AuraPhysicsQueryFilter.All, hits), scene.Box, "capsule shape");
        }

        /* The managed binding does not call Aura_OverlapShape / Aura_ShapeCast, so exercise the ABI directly:
           local pose composition, shape types, capacity and argument validation. */
        private static void B2_NativeShapeEntryPoints()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var scene = B2Build(world);
            var handle = (AuraEngine.Physics.Native.NativeWorldHandle)typeof(AuraEngine.Physics.Native.NativePhysicsWorld)
                .GetField("_world", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(world.Physics);
            var filter = AuraEngine.Physics.Native.NativeQueryFilter.From(AuraPhysicsQueryFilter.All);
            var raw = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<AuraEngine.Physics.Native.NativeQueryHit>() * 4);
            try
            {
                var box = AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f));
                var offset = AuraEngine.Physics.Native.NativeShapeDesc.From(box.WithLocalPose(new AuraPose(V(6f, 0f, 0f), AuraQuaternion.Identity)));
                var pose = AuraEngine.Physics.Native.NativePose.From(new AuraPose(V(0f, 0.2f, 0f), AuraQuaternion.Identity));
                Expect((AuraResult)AuraEngine.Physics.Native.NativeMethods.Aura_OverlapShape(handle, ref offset, ref pose, ref filter, raw, 4, out var count), AuraResult.Success, "OverlapShape local pose");
                Check(count == 1, $"local-pose overlap found {count}, expected 1.");
                var hit = System.Runtime.InteropServices.Marshal.PtrToStructure<AuraEngine.Physics.Native.NativeQueryHit>(raw);
                Check(hit.Body.ToManaged() == scene.Circle, "local pose overlap hit the wrong body.");

                Expect((AuraResult)AuraEngine.Physics.Native.NativeMethods.Aura_OverlapShape(handle, ref offset, ref pose, ref filter, IntPtr.Zero, 4, out count), AuraResult.InvalidWorld, "OverlapShape null buffer (rejected by the C API)");
                Expect((AuraResult)AuraEngine.Physics.Native.NativeMethods.Aura_OverlapShape(handle, ref offset, ref pose, ref filter, raw, 0, out count), AuraResult.InvalidDefinition, "OverlapShape zero capacity");
                var plane = AuraEngine.Physics.Native.NativeShapeDesc.From(new AuraPhysicsShapeDefinition(AuraShapeType.Plane, AuraPose.Identity, false, AuraPhysicsMaterialDefinition.Default, AuraPhysicsLayer.Default, AuraShapeGeometry.Plane(AuraVector3.UnitY)));
                Expect((AuraResult)AuraEngine.Physics.Native.NativeMethods.Aura_OverlapShape(handle, ref plane, ref pose, ref filter, raw, 4, out count), AuraResult.UnsupportedShape, "OverlapShape plane");
                var badBox = AuraEngine.Physics.Native.NativeShapeDesc.From(AuraPhysicsShapeDefinition.Box(V(-1f, 1f, 1f)));
                Expect((AuraResult)AuraEngine.Physics.Native.NativeMethods.Aura_OverlapShape(handle, ref badBox, ref pose, ref filter, raw, 4, out count), AuraResult.InvalidDefinition, "OverlapShape negative extents");

                var sphere = AuraEngine.Physics.Native.NativeShapeDesc.From(AuraPhysicsShapeDefinition.Sphere(0.3f).WithLocalPose(new AuraPose(V(0f, 0.5f, 0f), AuraQuaternion.Identity)));
                var start = AuraEngine.Physics.Native.NativePose.From(new AuraPose(V(-5f, -0.5f, 0f), AuraQuaternion.Identity));
                Expect((AuraResult)AuraEngine.Physics.Native.NativeMethods.Aura_ShapeCast(handle, ref sphere, ref start, AuraEngine.Physics.Native.NativeVector3.From(V(1f, 0f, 0f)), 20f, ref filter, out var castHit, out var hasHit), AuraResult.Success, "ShapeCast local pose");
                Check(hasHit == 1 && castHit.Body.ToManaged() == scene.Box, "local-pose cast must hit the box.");
                Near(castHit.Distance, 3.7f, 0.05f, "local-pose cast distance");
                Expect((AuraResult)AuraEngine.Physics.Native.NativeMethods.Aura_ShapeCast(handle, ref sphere, ref start, AuraEngine.Physics.Native.NativeVector3.From(V(1f, 0f, 0f)), 0f, ref filter, out castHit, out hasHit), AuraResult.InvalidDefinition, "ShapeCast zero distance");
                Expect((AuraResult)AuraEngine.Physics.Native.NativeMethods.Aura_ShapeCast(handle, ref plane, ref start, AuraEngine.Physics.Native.NativeVector3.From(V(1f, 0f, 0f)), 5f, ref filter, out castHit, out hasHit), AuraResult.UnsupportedShape, "ShapeCast plane");
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(raw);
            }
        }

        private static void B2_OverlapCapacityAndDedupe()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            for (var index = 0; index < 4; index++)
                B2Static(world, 40f + index, 0f, AuraPhysicsShapeDefinition.Box(V(0.45f, 0.45f, 0.5f)));
            /* Two overlapping shapes on one body must be reported once. */
            var multi = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(44f, 0f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(0.45f, 0.45f, 0.5f)),
                AuraPhysicsShapeDefinition.Sphere(0.5f)));
            var all = new AuraPhysicsQueryHit[16];
            var count = world.Physics.OverlapBox(V(42f, 0f, 0f), V(3f, 1f, 0.5f), AuraQuaternion.Identity, AuraPhysicsQueryFilter.All, all);
            Check(count == 5, $"expected 5 unique bodies, got {count}.");
            var seen = new HashSet<int>();
            for (var index = 0; index < count; index++)
                Check(seen.Add(all[index].Body.Index), "duplicate body in overlap results.");
            Check(seen.Contains(multi.Index), "multi-shape body missing.");

            var small = new AuraPhysicsQueryHit[2];
            count = world.Physics.OverlapBox(V(42f, 0f, 0f), V(3f, 1f, 0.5f), AuraQuaternion.Identity, AuraPhysicsQueryFilter.All, small);
            Check(count == 2, $"capacity 2 must truncate to 2, got {count}.");
            Check(small[0].Body.IsValid && small[1].Body.IsValid && small[0].Body != small[1].Body, "truncated hits must be valid and distinct.");
            count = world.Physics.OverlapSphere(V(42f, 0f, 0f), 3f, AuraPhysicsQueryFilter.All, new AuraPhysicsQueryHit[1]);
            Check(count == 1, $"capacity 1 sphere overlap must truncate to 1, got {count}.");
            Check(world.Physics.OverlapBox(V(42f, 0f, 0f), V(3f, 1f, 0.5f), AuraQuaternion.Identity, AuraPhysicsQueryFilter.All, Span<AuraPhysicsQueryHit>.Empty) == 0, "empty buffer returns 0.");
        }

        private static void B2_OverlapFilters()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var scene = B2Build(world);
            var hits = new AuraPhysicsQueryHit[8];
            var wide = V(30f, 2f, 0.5f);
            var center = V(12f, 0f, 0f);

            /* Layer mask: layer 3 only finds the layer-3 body; default layer finds the others. */
            var layer3Only = new AuraPhysicsQueryFilter(new AuraPhysicsLayerMask(1UL << 3));
            B2ExpectOnly(hits, world.Physics.OverlapBox(center, wide, AuraQuaternion.Identity, layer3Only, hits), scene.Layer3, "layer mask");
            var count = world.Physics.OverlapBox(center, wide, AuraQuaternion.Identity, new AuraPhysicsQueryFilter(new AuraPhysicsLayerMask(1UL)), hits);
            Check(count == 4, $"default-layer mask should find 4 bodies, got {count}.");

            /* Triggers: Ignore hides the sensor, Collide and UseGlobal include it. */
            var ignoreTriggers = new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, AuraTriggerInteraction.Ignore);
            Check(world.Physics.OverlapPoint(V(18f, 0f, 0f), ignoreTriggers, hits) == 0, "Ignore must skip sensor shapes.");
            B2ExpectOnly(hits, world.Physics.OverlapPoint(V(18f, 0f, 0f), new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, AuraTriggerInteraction.Collide), hits), scene.Trigger, "Collide includes trigger");
            B2ExpectOnly(hits, world.Physics.OverlapPoint(V(18f, 0f, 0f), AuraPhysicsQueryFilter.All, hits), scene.Trigger, "UseGlobal includes trigger");

            /* Ignored body. */
            var ignoreBox = new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, AuraTriggerInteraction.UseGlobal, default, scene.Box, AuraQueryFlags.IgnoreSelf);
            Check(world.Physics.OverlapPoint(V(0f, 0f, 0f), ignoreBox, hits) == 0, "ignored body must not be reported.");
            B2ExpectOnly(hits, world.Physics.OverlapPoint(V(6f, 0f, 0f), ignoreBox, hits), scene.Circle, "other bodies stay visible");
        }

        private static void B2_CastsHitExpectedBody()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var scene = B2Build(world);
            var east = V(1f, 0f, 0f);
            var from = V(-5f, 0f, 0f);
            var all = AuraPhysicsQueryFilter.All;

            Check(world.Physics.SphereCast(from, 0.3f, east, 20f, all, out var hit), "sphere cast must hit.");
            Check(hit.Body == scene.Box, "sphere cast hit the wrong body.");
            Near(hit.Distance, 3.7f, 0.05f, "sphere cast distance");
            Check(hit.Normal.X < -0.9f, $"sphere cast normal {hit.Normal} should face the caster.");
            Check(!world.Physics.SphereCast(from, 0.3f, east, 2f, all, out _), "short sphere cast must miss.");
            Check(!world.Physics.SphereCast(from, 0.3f, V(-1f, 0f, 0f), 20f, all, out _), "sphere cast away must miss.");
            Check(!world.Physics.SphereCast(from, 0.3f, V(0f, 1f, 0f), 20f, all, out _), "sphere cast past the box must miss.");

            Check(world.Physics.BoxCast(from, V(0.2f, 0.2f, 0.5f), AuraQuaternion.Identity, east, 20f, all, out hit), "box cast must hit.");
            Check(hit.Body == scene.Box, "box cast hit the wrong body.");
            Near(hit.Distance, 3.8f, 0.05f, "box cast distance");
            var diamond = AuraQuaternion.FromAxisAngle(AuraVector3.UnitZ, MathF.PI * 0.25f);
            Check(world.Physics.BoxCast(from, V(0.2f, 0.2f, 0.5f), diamond, east, 20f, all, out hit), "rotated box cast must hit.");
            Near(hit.Distance, 5f - 1f - 0.2f * MathF.Sqrt(2f), 0.05f, "rotated box cast distance");

            Check(world.Physics.CapsuleCast(V(-5f, -0.5f, 0f), V(-5f, 0.5f, 0f), 0.2f, east, 20f, all, out hit), "capsule cast must hit.");
            Check(hit.Body == scene.Box, "capsule cast hit the wrong body.");
            Near(hit.Distance, 3.8f, 0.05f, "capsule cast distance");

            Check(world.Physics.ShapeCast(AuraPhysicsShapeDefinition.Sphere(0.3f), new AuraPose(from, AuraQuaternion.Identity), east, 20f, all, out hit), "shape cast must hit.");
            Check(hit.Body == scene.Box, "shape cast hit the wrong body.");
            Near(hit.Distance, 3.7f, 0.05f, "shape cast distance");

            /* Casting at the circle from above finds it, with a downward-facing... upward normal. */
            Check(world.Physics.SphereCast(V(6f, 6f, 0f), 0.2f, V(0f, -1f, 0f), 20f, all, out hit), "downward cast must hit the circle.");
            Check(hit.Body == scene.Circle, "downward cast hit the wrong body.");
            Check(hit.Normal.Y > 0.9f, $"downward cast normal {hit.Normal} should point up.");
            Near(hit.Distance, 6f - 1f - 0.2f, 0.05f, "downward cast distance");
        }

        private static void B2_CastFilters()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var scene = B2Build(world);
            var east = V(1f, 0f, 0f);
            var from = V(-5f, 0f, 0f);

            var ignoreBox = new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, AuraTriggerInteraction.UseGlobal, default, scene.Box, AuraQueryFlags.IgnoreSelf);
            Check(world.Physics.SphereCast(from, 0.3f, east, 40f, ignoreBox, out var hit), "cast ignoring the box must hit the next body.");
            Check(hit.Body == scene.Circle, "ignored body still blocked the cast.");
            Near(hit.Distance, 9.7f, 0.05f, "distance to circle");

            var layer3Only = new AuraPhysicsQueryFilter(new AuraPhysicsLayerMask(1UL << 3));
            Check(world.Physics.SphereCast(from, 0.3f, east, 40f, layer3Only, out hit), "layer-3 cast must hit.");
            Check(hit.Body == scene.Layer3, "layer filter hit the wrong body.");
            Near(hit.Distance, 27.7f, 0.05f, "distance to layer 3 box");

            var ignoreTriggers = new AuraPhysicsQueryFilter(new AuraPhysicsLayerMask(1UL), AuraTriggerInteraction.Ignore);
            Check(world.Physics.BoxCast(V(14f, 0f, 0f), V(0.2f, 0.2f, 0.5f), AuraQuaternion.Identity, east, 40f, ignoreTriggers, out _) == false, "trigger must be skipped by Ignore.");
            Check(world.Physics.BoxCast(V(14f, 0f, 0f), V(0.2f, 0.2f, 0.5f), AuraQuaternion.Identity, east, 40f, new AuraPhysicsQueryFilter(new AuraPhysicsLayerMask(1UL), AuraTriggerInteraction.Collide), out hit), "trigger must be hit with Collide.");
            Check(hit.Body == scene.Trigger, "trigger cast hit the wrong body.");
        }

        private static void B2_QueryInvalidAndStale()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var scene = B2Build(world);
            var hits = new AuraPhysicsQueryHit[4];
            var all = AuraPhysicsQueryFilter.All;
            var nan = float.NaN;

            Check(world.Physics.OverlapPoint(V(nan, 0f, 0f), all, hits) == 0, "NaN point.");
            Check(world.Physics.OverlapSphere(V(0f, 0f, 0f), nan, all, hits) == 0, "NaN radius.");
            Check(world.Physics.OverlapSphere(V(0f, 0f, 0f), -1f, all, hits) == 0, "negative radius.");
            Check(world.Physics.OverlapBox(V(0f, 0f, 0f), V(-1f, 1f, 1f), AuraQuaternion.Identity, all, hits) == 0, "negative half extents.");
            Check(world.Physics.OverlapCapsule(V(0f, 0f, 0f), V(nan, 0f, 0f), 1f, all, hits) == 0, "NaN capsule point.");
            Check(!world.Physics.SphereCast(V(0f, 5f, 0f), 0.3f, V(0f, -1f, 0f), 0f, all, out _), "zero distance cast.");
            Check(!world.Physics.SphereCast(V(0f, 5f, 0f), 0.3f, V(0f, 0f, 0f), 5f, all, out _), "zero direction cast.");
            Check(!world.Physics.SphereCast(V(0f, 5f, 0f), nan, V(0f, -1f, 0f), 5f, all, out _), "NaN radius cast.");
            Check(!world.Physics.BoxCast(V(0f, 5f, 0f), V(1f, 1f, 1f), AuraQuaternion.Identity, V(nan, -1f, 0f), 5f, all, out _), "NaN direction cast.");
            var plane = new AuraPhysicsShapeDefinition(AuraShapeType.Plane, AuraPose.Identity, false, AuraPhysicsMaterialDefinition.Default, AuraPhysicsLayer.Default, AuraShapeGeometry.Plane(AuraVector3.UnitY));
            Check(world.Physics.OverlapShape(plane, AuraPose.Identity, all, hits) == 0, "unsupported overlap shape.");
            Check(!world.Physics.ShapeCast(plane, AuraPose.Identity, V(1f, 0f, 0f), 5f, all, out _), "unsupported cast shape.");

            /* Stale ignored-body handles and destroyed bodies are harmless. */
            var staleIgnore = new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, AuraTriggerInteraction.UseGlobal, default, new PhysicsBodyId(900, 3), AuraQueryFlags.IgnoreSelf);
            Check(world.Physics.OverlapPoint(V(0f, 0f, 0f), staleIgnore, hits) == 1, "a stale ignored handle must not hide real bodies.");
            Ok(world.Physics.DestroyBody(scene.Box), "destroy body");
            Check(world.Physics.OverlapPoint(V(0f, 0f, 0f), all, hits) == 0, "destroyed body still reported by overlap.");
            Check(!world.Physics.SphereCast(V(-5f, 0f, 0f), 0.3f, V(1f, 0f, 0f), 3f, all, out _), "destroyed body still hit by cast.");
            var ignoreDestroyed = new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, AuraTriggerInteraction.UseGlobal, default, scene.Box, AuraQueryFlags.IgnoreSelf);
            Check(world.Physics.OverlapPoint(V(6f, 0f, 0f), ignoreDestroyed, hits) == 1, "stale ignored body must not hide others.");
        }

        private static AuraJointFeedback B2Feedback(AuraSimulationWorld world, AuraJointId joint)
        {
            Ok(world.JointControl.GetFeedback(joint, out var feedback), "GetFeedback");
            return feedback;
        }

        private static void B2_WheelMotorDrivesWheel()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var chassis = B2Static(world, 0f, 2f, AuraPhysicsShapeDefinition.Box(V(0.5f, 0.2f, 0.5f)));
            var wheel = Dyn(world, V(0f, 1f, 0f), AuraPhysicsShapeDefinition.Sphere(0.4f), gravityScale: 0f);
            var joint = world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateWheel(chassis, wheel, V(0f, 1f, 0f), V(0f, 1f, 0f), V(0f, 1f, 0f),
                springFrequency: 4f, springDamping: 0.7f, motorEnabled: true, motorSpeed: 6f, maxMotorTorque: 1.0e6f));
            Check(joint.IsValid, "wheel joint creation failed.");
            Step(world, 90);
            Near(StateOf(world, wheel).AngularVelocity.Z, 6f, 0.3f, "wheel spin follows the motor");
            Check(B2Feedback(world, joint).MotorMode == AuraJointMotorMode.Velocity, "feedback motor mode.");

            Ok(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Velocity(-3f, 1.0e6f)), "SetMotor reverse");
            Step(world, 90, 90u);
            Near(StateOf(world, wheel).AngularVelocity.Z, -3f, 0.3f, "wheel reverses");
            Ok(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Off), "SetMotor off");
            Check(B2Feedback(world, joint).MotorMode == AuraJointMotorMode.Off, "motor did not switch off.");
        }

        private static void B2_WheelCarDrivesForward()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            B2Static(world, 0f, -0.5f, AuraPhysicsShapeDefinition.Box(V(40f, 0.5f, 0.5f)));
            var chassis = Dyn(world, V(0f, 1.4f, 0f), AuraPhysicsShapeDefinition.Box(V(1.2f, 0.2f, 0.5f)));
            var joints = new List<AuraJointId>();
            foreach (var x in new[] { -0.9f, 0.9f })
            {
                var wheel = Dyn(world, V(x, 0.6f, 0f), AuraPhysicsShapeDefinition.Sphere(0.4f));
                var joint = world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateWheel(chassis, wheel, V(x, 0.6f, 0f), V(x, 0.6f, 0f), V(0f, 1f, 0f),
                    springFrequency: 4f, springDamping: 0.7f, enableLimit: true, minLimit: -0.3f, maxLimit: 0.3f, motorEnabled: true, motorSpeed: -8f, maxMotorTorque: 5000f));
                Check(joint.IsValid, "wheel joint creation failed.");
                joints.Add(joint);
            }

            Step(world, 60);
            var startX = StateOf(world, chassis).Pose.Position.X;
            Step(world, 180, 60u);
            var state = StateOf(world, chassis);
            Check(state.Pose.Position.X - startX > 1.0f, $"car did not drive forward (moved {state.Pose.Position.X - startX}).");
            Check(state.Pose.Position.Y > 0.6f && state.Pose.Position.Y < 2.0f, $"suspension let the chassis fall or launch (y={state.Pose.Position.Y}).");
            Check(Math.Abs(B2Feedback(world, joints[0]).Position) <= 0.45f, "suspension travel exceeded its limits.");
        }

        private static void B2_WheelLimitsAndControl()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var chassis = B2Static(world, 0f, 2f, AuraPhysicsShapeDefinition.Box(V(0.5f, 0.2f, 0.5f)));
            var wheel = Dyn(world, V(0f, 1f, 0f), AuraPhysicsShapeDefinition.Sphere(0.4f));
            var joint = world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateWheel(chassis, wheel, V(0f, 1f, 0f), V(0f, 1f, 0f), V(0f, 1f, 0f), springFrequency: 2f, springDamping: 0.5f));
            Check(joint.IsValid, "wheel joint creation failed.");
            Expect(world.JointControl.SetLimits(joint, true, 0.2f, 0.5f), AuraResult.InvalidDefinition, "limits must straddle zero");
            Ok(world.JointControl.SetLimits(joint, true, -0.1f, 0.1f), "SetLimits");
            Step(world, 240);
            Near(StateOf(world, wheel).Pose.Position.Y, 0.9f, 0.15f, "wheel stays within suspension travel");
            Ok(world.JointControl.SetLimits(joint, false, 0f, 0f), "SetLimits off");
            Expect(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Velocity(1f, 0f)), AuraResult.InvalidDefinition, "motor without torque");
            Expect(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Position(1f, 10f)), AuraResult.UnsupportedOperation, "position motor");
            Ok(world.JointControl.SetBreakThreshold(joint, 1.0e9f, 1.0e9f), "break threshold on wheel");
            Ok(world.DestroyJoint(joint), "destroy wheel joint");
            Expect(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Off), AuraResult.InvalidHandle, "stale wheel SetMotor");
        }

        private static void B2_MouseJointPullsBody()
        {
            foreach (var staticAnchor in new[] { false, true })
            {
                using var world = NewWorld(AuraPhysicsMode.Plane2D);
                var body = Dyn(world, V(0f, 0f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), gravityScale: 0f);
                var anchor = staticAnchor ? B2Static(world, -10f, 0f, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.5f))) : PhysicsBodyId.Invalid;
                var joint = world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateMouse(anchor, body, V(0f, 0f, 0f), 5f, 0.7f, 1.0e5f));
                Check(joint.IsValid, $"mouse joint creation failed (staticAnchor={staticAnchor}).");
                var target = (IPhysicsJointTarget)world.Physics;
                Step(world, 30);
                Near(StateOf(world, body).Pose.Position.X, 0f, 0.05f, "mouse joint holds the body at its initial target");

                Ok(target.SetJointTarget(joint, V(3f, 2f, 0f)), "SetJointTarget");
                Step(world, 240, 30u);
                var position = StateOf(world, body).Pose.Position;
                Near(position.X, 3f, 0.3f, "body x follows the target");
                Near(position.Y, 2f, 0.3f, "body y follows the target");

                Ok(target.SetJointTarget(joint, V(-2f, -1f, 0f)), "SetJointTarget again");
                Step(world, 240, 270u);
                position = StateOf(world, body).Pose.Position;
                Near(position.X, -2f, 0.3f, "body x follows the moved target");
                Near(position.Y, -1f, 0.3f, "body y follows the moved target");
                Check(B2Feedback(world, joint).Force >= 0f, "mouse joint feedback.");
            }
        }

        private static void B2_MouseJointInvalidAndStale()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var body = Dyn(world, V(0f, 0f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), gravityScale: 0f);
            var other = Dyn(world, V(3f, 0f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), gravityScale: 0f);
            var target = (IPhysicsJointTarget)world.Physics;

            Check(!world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateMouse(PhysicsBodyId.Invalid, body, V(0f, 0f, 0f), 5f, 0.7f, 0f)).IsValid, "mouse joint needs a positive max force.");
            Check(!world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateMouse(PhysicsBodyId.Invalid, new PhysicsBodyId(700, 1), V(0f, 0f, 0f), 5f, 0.7f, 10f)).IsValid, "mouse joint on a stale body.");
            Check(!world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateMouse(new PhysicsBodyId(700, 1), body, V(0f, 0f, 0f), 5f, 0.7f, 10f)).IsValid, "mouse joint with a stale fixed body.");

            var joint = world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateMouse(PhysicsBodyId.Invalid, body, V(0f, 0f, 0f), 5f, 0.7f, 1.0e4f));
            Check(joint.IsValid, "mouse joint creation failed.");
            Expect(target.SetJointTarget(joint, V(float.NaN, 0f, 0f)), AuraResult.InvalidDefinition, "NaN target");
            Expect(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Velocity(1f, 1f)), AuraResult.UnsupportedOperation, "motor on mouse joint");
            Expect(world.JointControl.SetLimits(joint, true, -1f, 1f), AuraResult.UnsupportedOperation, "limits on mouse joint");

            var distance = world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateDistance(body, other, V(0f, 0f, 0f), V(3f, 0f, 0f), 3f));
            Check(distance.IsValid, "distance joint creation failed.");
            Expect(target.SetJointTarget(distance, V(1f, 1f, 0f)), AuraResult.UnsupportedOperation, "target on a distance joint");

            foreach (var stale in new[] { AuraJointId.Invalid, new AuraJointId(0, 77), new AuraJointId(4000, 0) })
                Expect(target.SetJointTarget(stale, V(1f, 1f, 0f)), AuraResult.InvalidHandle, "stale SetJointTarget");

            /* Destroying the dragged body takes the joint with it; the id goes stale. */
            Ok(world.Physics.DestroyBody(body), "destroy dragged body");
            Expect(target.SetJointTarget(joint, V(1f, 1f, 0f)), AuraResult.InvalidHandle, "target after body destroy");
            Step(world, 5);
            Ok(world.Physics.DestroyBody(other), "destroy other body");
            Step(world, 5);
        }

        private static void B2_RopeLimitsLengthOnly()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var anchor = B2Static(world, 0f, 5f, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.5f)));
            var weight = Dyn(world, V(0f, 4f, 0f), AuraPhysicsShapeDefinition.Box(V(0.2f, 0.2f, 0.5f)));
            var rope = world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateRope(anchor, weight, V(0f, 5f, 0f), V(0f, 4f, 0f), 2f));
            Check(rope.IsValid, "rope creation failed.");
            Step(world, 6);
            Check(StateOf(world, weight).Pose.Position.Y < 3.99f, "a slack rope must let the weight fall.");
            Step(world, 240, 6u);
            var position = StateOf(world, weight).Pose.Position;
            var length = AuraVector3.Distance(position, V(0f, 5f, 0f));
            Check(length > 1.85f && length < 2.15f, $"hanging rope length {length}, expected about 2.");
            Ok(world.JointControl.GetFeedback(rope, out var feedback), "rope feedback");
            Check(feedback.Force > 0f, "a taut rope must report a load.");

            using var space = NewWorld(AuraPhysicsMode.Plane2D);
            var a = B2Static(space, 0f, 0f, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.5f)));
            var b = Dyn(space, V(1f, 0f, 0f), AuraPhysicsShapeDefinition.Box(V(0.2f, 0.2f, 0.5f)), gravityScale: 0f);
            Check(space.Physics.Joints.CreateJoint(AuraJointDefinition.CreateRope(a, b, V(0f, 0f, 0f), V(1f, 0f, 0f), 3f)).IsValid, "slack rope creation failed.");
            Step(space, 120);
            Near(StateOf(space, b).Pose.Position.X, 1f, 0.05f, "a slack rope must not push or pull");

            Check(!space.Physics.Joints.CreateJoint(AuraJointDefinition.CreateRope(a, b, V(0f, 0f, 0f), V(1f, 0f, 0f), 0f)).IsValid, "zero-length rope must be rejected.");
            Check(!space.Physics.Joints.CreateJoint(AuraJointDefinition.CreateRope(a, a, V(0f, 0f, 0f), V(0f, 0f, 0f), 1f)).IsValid, "rope from a body to itself must be rejected.");
        }

        private static void B2_NewJointsUnsupportedIn3D()
        {
            using var world = NewWorld();
            var a = Dyn(world, V(0f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var b = Dyn(world, V(2f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Check(!world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateWheel(a, b, V(0f, 0f, 0f), V(2f, 0f, 0f), V(0f, 1f, 0f))).IsValid, "wheel joint must be unsupported in 3D.");
            Check(!world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateMouse(PhysicsBodyId.Invalid, b, V(2f, 0f, 0f), 5f, 0.7f, 10f)).IsValid, "mouse joint must be unsupported in 3D.");
            Check(!world.Physics.Joints.CreateJoint(AuraJointDefinition.CreateRope(a, b, V(0f, 0f, 0f), V(2f, 0f, 0f), 2f)).IsValid, "rope joint must be unsupported in 3D.");
            Step(world, 5);
        }
    }
}
