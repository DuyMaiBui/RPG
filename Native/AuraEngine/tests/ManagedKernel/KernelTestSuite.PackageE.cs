using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Physics.Native;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package E: gravity fields, CCD and time scale. */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageETests()
        {
            return new (string Name, Action Body)[]
            {
                ("fields_desc_layout_matches_abi", Fields_DescLayoutMatchesAbi),
                ("fields_world_gravity_redirects_3d", () => Fields_WorldGravity(AuraPhysicsMode.Full3D)),
                ("fields_world_gravity_redirects_2d", () => Fields_WorldGravity(AuraPhysicsMode.Plane2D)),
                ("fields_directional_inside_only_3d", () => Fields_DirectionalInsideOnly(AuraPhysicsMode.Full3D)),
                ("fields_directional_inside_only_2d", () => Fields_DirectionalInsideOnly(AuraPhysicsMode.Plane2D)),
                ("fields_acceleration_follows_gravity_scale_3d", () => Fields_AccelerationFollowsGravityScale(AuraPhysicsMode.Full3D)),
                ("fields_acceleration_follows_gravity_scale_2d", () => Fields_AccelerationFollowsGravityScale(AuraPhysicsMode.Plane2D)),
                ("fields_force_mode_divides_by_mass_3d", Fields_ForceModeDividesByMass),
                ("fields_radial_attracts_and_repels_3d", () => Fields_RadialAttractsAndRepels(AuraPhysicsMode.Full3D)),
                ("fields_radial_attracts_and_repels_2d", () => Fields_RadialAttractsAndRepels(AuraPhysicsMode.Plane2D)),
                ("fields_radial_falloff_scales_3d", () => Fields_RadialFalloffScales(AuraPhysicsMode.Full3D)),
                ("fields_radial_falloff_scales_2d", () => Fields_RadialFalloffScales(AuraPhysicsMode.Plane2D)),
                ("fields_radial_orbit_3d", () => Fields_RadialOrbit(AuraPhysicsMode.Full3D)),
                ("fields_radial_orbit_2d", () => Fields_RadialOrbit(AuraPhysicsMode.Plane2D)),
                ("fields_wind_drag_approaches_wind_3d", () => Fields_WindDrag(AuraPhysicsMode.Full3D)),
                ("fields_wind_drag_approaches_wind_2d", () => Fields_WindDrag(AuraPhysicsMode.Plane2D)),
                ("fields_destroy_update_stop_effect_3d", () => Fields_DestroyAndUpdateStopEffect(AuraPhysicsMode.Full3D)),
                ("fields_destroy_update_stop_effect_2d", () => Fields_DestroyAndUpdateStopEffect(AuraPhysicsMode.Plane2D)),
                ("fields_stale_and_invalid_3d", () => Fields_StaleAndInvalid(AuraPhysicsMode.Full3D)),
                ("fields_stale_and_invalid_2d", () => Fields_StaleAndInvalid(AuraPhysicsMode.Plane2D)),
                ("fields_layer_mask_3d", () => Fields_LayerMask(AuraPhysicsMode.Full3D)),
                ("fields_layer_mask_2d", () => Fields_LayerMask(AuraPhysicsMode.Plane2D)),
                ("fields_wake_sleeping_body_3d", () => Fields_WakesSleepingBody(AuraPhysicsMode.Full3D)),
                ("fields_wake_sleeping_body_2d", () => Fields_WakesSleepingBody(AuraPhysicsMode.Plane2D)),
                ("fields_deterministic_3d", () => Fields_Deterministic(AuraPhysicsMode.Full3D)),
                ("fields_deterministic_2d", () => Fields_Deterministic(AuraPhysicsMode.Plane2D)),
                ("fields_null_backend_reports_unsupported", Fields_NullBackendReportsUnsupported),
                ("ccd_fast_body_tunnels_without_3d", () => Ccd_FastBody(AuraPhysicsMode.Full3D)),
                ("ccd_fast_body_tunnels_without_2d", () => Ccd_FastBody(AuraPhysicsMode.Plane2D)),
                ("ccd_runtime_setter_3d", () => Ccd_RuntimeSetter(AuraPhysicsMode.Full3D)),
                ("ccd_runtime_setter_2d", () => Ccd_RuntimeSetter(AuraPhysicsMode.Plane2D)),
                ("ccd_setter_rejects_static_and_stale_3d", () => Ccd_SetterRejects(AuraPhysicsMode.Full3D)),
                ("ccd_setter_rejects_static_and_stale_2d", () => Ccd_SetterRejects(AuraPhysicsMode.Plane2D)),
                ("time_stepper_ratios", Time_StepperRatios),
                ("time_scale_half_is_deterministic_3d", () => Time_ScaleHalf(AuraPhysicsMode.Full3D)),
                ("time_scale_half_is_deterministic_2d", () => Time_ScaleHalf(AuraPhysicsMode.Plane2D)),
                ("time_scale_zero_pauses_and_resumes", Time_ZeroPauses),
                ("hit_stop_freezes_and_restores_3d", () => HitStop_FreezesAndRestores(AuraPhysicsMode.Full3D)),
                ("hit_stop_freezes_and_restores_2d", () => HitStop_FreezesAndRestores(AuraPhysicsMode.Plane2D)),
                ("hit_stop_edge_cases", HitStop_EdgeCases),
            };
        }

        private static AuraSimulationWorld NewFieldWorld(AuraPhysicsMode mode, AuraVector3 gravity)
        {
            var world = NewWorld(mode);
            Ok(world.ForceFields.SetGravity(gravity), "SetGravity");
            return world;
        }

        private static AuraForceFieldDefinition SphereField(AuraForceFieldKind kind, AuraVector3 center, float radius, AuraVector3 vector,
            float strength, AuraForceFieldMode mode = AuraForceFieldMode.Acceleration, AuraForceFieldFalloff falloff = AuraForceFieldFalloff.None,
            float minRadius = 0f, float maxRadius = 0f, AuraPhysicsLayerMask? layers = null) =>
            new AuraForceFieldDefinition(AuraForceFieldShape.Sphere, kind, new AuraPose(center, AuraQuaternion.Identity), radius,
                V(1f, 1f, 1f), vector, strength, mode, falloff, minRadius, maxRadius, layers);

        private static AuraForceFieldDefinition BoxField(AuraForceFieldKind kind, AuraVector3 center, AuraVector3 half, AuraVector3 vector,
            float strength, AuraForceFieldMode mode = AuraForceFieldMode.Acceleration) =>
            new AuraForceFieldDefinition(AuraForceFieldShape.Box, kind, new AuraPose(center, AuraQuaternion.Identity), 1f, half, vector, strength, mode);

        private static AuraForceFieldId Field(AuraSimulationWorld world, in AuraForceFieldDefinition definition)
        {
            var id = world.ForceFields.CreateField(definition);
            Check(id.IsValid, "CreateField was rejected.");
            return id;
        }

        private static AuraVector3 VelocityOf(AuraSimulationWorld world, PhysicsBodyId body) => StateOf(world, body).LinearVelocity;

        private static void Fields_DescLayoutMatchesAbi()
        {
            Check(System.Runtime.InteropServices.Marshal.SizeOf<NativeForceFieldDesc>() == 104, "NativeForceFieldDesc must mirror the 104 byte AuraForceFieldDesc.");
            Check(System.Runtime.InteropServices.Marshal.OffsetOf<NativeForceFieldDesc>(nameof(NativeForceFieldDesc.LayerMask)).ToInt32() == 88, "LayerMask offset.");
            Check(System.Runtime.InteropServices.Marshal.OffsetOf<NativeForceFieldDesc>(nameof(NativeForceFieldDesc.Strength)).ToInt32() == 72, "Strength offset.");
        }

        private static void Fields_WorldGravity(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            Check((world.Capabilities & AuraPhysicsCapabilities.ForceFields) != 0, "ForceFields capability missing.");
            var ball = Dyn(world, V(0f, 50f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Step(world, 30);
            var falling = VelocityOf(world, ball).Y;
            Check(falling < -3f, $"ball did not fall under the default gravity ({falling}).");

            Ok(world.ForceFields.SetGravity(V(0f, 9.81f, 5f)), "SetGravity up");
            Ok(world.ForceFields.GetGravity(out var read), "GetGravity");
            Near(read.Y, 9.81f, 1e-4f, "gravity readback y");
            Near(read.Z, mode == AuraPhysicsMode.Plane2D ? 0f : 5f, 1e-4f, "gravity readback z");
            Step(world, 60, 30);
            var rising = VelocityOf(world, ball).Y;
            Check(rising > 2f, $"ball was not redirected upward ({rising}).");
            if (mode == AuraPhysicsMode.Full3D)
                Check(VelocityOf(world, ball).Z > 1f, "z gravity component had no effect.");
        }

        private static void Fields_DirectionalInsideOnly(AuraPhysicsMode mode)
        {
            using var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            var inside = Dyn(world, V(0f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var outside = Dyn(world, V(50f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Field(world, BoxField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), V(10f, 10f, 10f), V(0f, 4f, 0f), 0f));
            Step(world, 30);
            Near(VelocityOf(world, inside).Y, 2f, 0.15f, "inside zone velocity after 0.5 s");
            Near(VelocityOf(world, outside).Y, 0f, 1e-4f, "outside zone velocity");
        }

        private static void Fields_AccelerationFollowsGravityScale(AuraPhysicsMode mode)
        {
            using var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            var normal = Dyn(world, V(-5f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 1f);
            var doubled = Dyn(world, V(5f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 2f);
            var weightless = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            Field(world, BoxField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), V(20f, 20f, 20f), V(0f, 3f, 0f), 0f));
            Step(world, 20);
            var v1 = VelocityOf(world, normal).Y;
            Check(v1 > 0.9f, $"acceleration field did not move the body ({v1}).");
            Near(VelocityOf(world, doubled).Y / v1, 2f, 0.05f, "gravity scale 2 doubles the acceleration");
            Near(VelocityOf(world, weightless).Y, 0f, 1e-4f, "gravity scale 0 ignores acceleration fields");
        }

        private static void Fields_ForceModeDividesByMass()
        {
            using var world = NewFieldWorld(AuraPhysicsMode.Full3D, V(0f, 0f, 0f));
            var light = Dyn(world, V(-5f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), mass: 1f);
            var heavy = Dyn(world, V(5f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), mass: 4f, gravityScale: 0f);
            Field(world, BoxField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), V(20f, 20f, 20f), V(0f, 8f, 0f), 0f, AuraForceFieldMode.Force));
            Step(world, 30);
            Near(VelocityOf(world, light).Y, 4f, 0.3f, "force 8 on mass 1 for 0.5 s");
            Near(VelocityOf(world, heavy).Y, 1f, 0.1f, "force 8 on mass 4 for 0.5 s ignores gravity scale 0");
        }

        private static void Fields_RadialAttractsAndRepels(AuraPhysicsMode mode)
        {
            using var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            var pulled = Dyn(world, V(10f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var pushed = Dyn(world, V(0f, 10f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Field(world, SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), 30f, default, 10f));
            Step(world, 30);
            Near(VelocityOf(world, pulled).X, -5f, 0.4f, "attraction toward the centre");
            Near(VelocityOf(world, pulled).Y, 0f, 0.05f, "attraction has no sideways component");

            using var repel = NewFieldWorld(mode, V(0f, 0f, 0f));
            var away = Dyn(repel, V(10f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Field(repel, SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), 30f, default, -10f));
            Step(repel, 30);
            Near(VelocityOf(repel, away).X, 5f, 0.4f, "negative strength pushes away");
            Check(VelocityOf(world, pushed).Y < -4f, "second body was not attracted either.");
        }

        private static void Fields_RadialFalloffScales(AuraPhysicsMode mode)
        {
            // Linear: 10 * (1 - d / 20) => 7.5 at d = 5 and 2.5 at d = 15.
            using var linear = NewFieldWorld(mode, V(0f, 0f, 0f));
            var near = Dyn(linear, V(5f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.25f));
            var far = Dyn(linear, V(0f, 15f, 0f), AuraPhysicsShapeDefinition.Sphere(0.25f));
            Field(linear, SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), 30f, default, 10f, AuraForceFieldMode.Acceleration, AuraForceFieldFalloff.Linear, 0f, 20f));
            Step(linear, 1);
            var vNear = -VelocityOf(linear, near).X;
            var vFar = -VelocityOf(linear, far).Y;
            Near(vNear, 7.5f * Dt, 1e-3f, "linear near");
            Near(vFar, 2.5f * Dt, 1e-3f, "linear far");

            // Inverse square: 8 / d^2 => 2 at d = 2 and 0.5 at d = 4; clamped to minRadius 2 below that.
            using var inverse = NewFieldWorld(mode, V(0f, 0f, 0f));
            var a = Dyn(inverse, V(2f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.25f));
            var b = Dyn(inverse, V(0f, 4f, 0f), AuraPhysicsShapeDefinition.Sphere(0.25f));
            var c = Dyn(inverse, V(0f, 0f, 1f), AuraPhysicsShapeDefinition.Sphere(0.25f));
            Field(inverse, SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), 30f, default, 8f, AuraForceFieldMode.Acceleration, AuraForceFieldFalloff.InverseSquare, 2f));
            Step(inverse, 1);
            Near(-VelocityOf(inverse, a).X / Dt, 2f, 0.05f, "inverse square at 2");
            Near(-VelocityOf(inverse, b).Y / Dt, 0.5f, 0.02f, "inverse square at 4");
            if (mode == AuraPhysicsMode.Full3D)
                Near(-VelocityOf(inverse, c).Z / Dt, 2f, 0.05f, "inverse square clamped to the min radius at 1");

            // Past MaxRadius the field does nothing.
            using var capped = NewFieldWorld(mode, V(0f, 0f, 0f));
            var outside = Dyn(capped, V(12f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.25f));
            Field(capped, SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), 30f, default, 10f, maxRadius: 8f));
            Step(capped, 5);
            Near(VelocityOf(capped, outside).X, 0f, 1e-4f, "beyond max radius");
        }

        private static void Fields_RadialOrbit(AuraPhysicsMode mode)
        {
            using var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            const float gm = 100f;
            var speed = MathF.Sqrt(gm / 10f);
            var moon = world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(
                AuraBodyType.Dynamic, new AuraPose(V(10f, 0f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new[] { AuraPhysicsShapeDefinition.Sphere(0.25f) }, initialLinearVelocity: V(0f, speed, 0f)));
            Field(world, SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), 60f, default, gm, AuraForceFieldMode.Acceleration, AuraForceFieldFalloff.InverseSquare, 1f));

            var minRadius = float.MaxValue;
            var maxRadius = 0f;
            for (var index = 0; index < 700; index++)
            {
                Step(world, 1, (uint)index);
                var p = StateOf(world, moon).Pose.Position;
                var r = MathF.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
                minRadius = MathF.Min(minRadius, r);
                maxRadius = MathF.Max(maxRadius, r);
            }

            var end = StateOf(world, moon).Pose.Position;
            Check(minRadius > 8.5f && maxRadius < 11.5f, $"orbit radius drifted to [{minRadius}, {maxRadius}].");
            Check(end.X < -3f, $"the body did not swing around the centre (x = {end.X}).");
        }

        private static void Fields_WindDrag(AuraPhysicsMode mode)
        {
            using var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            var leaf = Dyn(world, V(0f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.25f));
            var rock = Dyn(world, V(0f, 10f, 0f), AuraPhysicsShapeDefinition.Sphere(0.25f));
            Ok(world.BodyControl.SetLinearVelocity(rock, V(-6f, 0f, 0f)), "launch");
            Field(world, BoxField(AuraForceFieldKind.Drag, V(0f, 5f, 0f), V(1000f, 1000f, 1000f), V(8f, 0f, 0f), 2f));
            Step(world, 300);
            Near(VelocityOf(world, leaf).X, 8f, 0.2f, "velocity approaches the wind");
            Near(VelocityOf(world, rock).X, 8f, 0.2f, "opposing velocity is dragged around to the wind");

            // Force mode: drag rate = c / m, so a heavy body converges slower than a light one.
            if (mode == AuraPhysicsMode.Full3D)
            {
                using var forceWorld = NewFieldWorld(mode, V(0f, 0f, 0f));
                var light = Dyn(forceWorld, V(0f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.25f), mass: 1f);
                var heavy = Dyn(forceWorld, V(0f, 10f, 0f), AuraPhysicsShapeDefinition.Sphere(0.25f), mass: 10f);
                Field(forceWorld, BoxField(AuraForceFieldKind.Drag, V(0f, 5f, 0f), V(1000f, 1000f, 1000f), V(8f, 0f, 0f), 2f, AuraForceFieldMode.Force));
                Step(forceWorld, 60);
                Check(VelocityOf(forceWorld, light).X > 6f, "light body should be near the wind speed after 1 s.");
                Check(VelocityOf(forceWorld, heavy).X < 3f, "heavy body should lag behind the wind.");
            }
        }

        private static void Fields_DestroyAndUpdateStopEffect(AuraPhysicsMode mode)
        {
            using var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            var ball = Dyn(world, V(0f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var field = Field(world, BoxField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), V(100f, 100f, 100f), V(0f, 5f, 0f), 0f));
            Step(world, 20);
            var pushed = VelocityOf(world, ball).Y;
            Check(pushed > 1f, "field had no effect before destroy.");

            // Disable through Update.
            var definition = BoxField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), V(100f, 100f, 100f), V(0f, 5f, 0f), 0f).WithEnabled(false);
            Ok(world.ForceFields.UpdateField(field, definition), "UpdateField disable");
            Step(world, 20, 20);
            var coasting = VelocityOf(world, ball).Y;
            Check(coasting <= pushed + 1e-3f && coasting > pushed * 0.8f, $"disabled field kept accelerating ({pushed} -> {coasting}).");

            // Re-enable, then destroy.
            Ok(world.ForceFields.UpdateField(field, definition.WithEnabled(true)), "UpdateField enable");
            Step(world, 10, 40);
            var again = VelocityOf(world, ball).Y;
            Check(again > coasting + 0.5f, "re-enabled field did not push.");
            Ok(world.ForceFields.DestroyField(field), "DestroyField");
            Step(world, 30, 50);
            var after = VelocityOf(world, ball).Y;
            Check(after <= again + 1e-3f && after > again * 0.8f, $"destroyed field kept accelerating ({again} -> {after}).");

            // Moving the zone away stops the effect too.
            var moved = Field(world, BoxField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), V(1000f, 1000f, 1000f), V(0f, 5f, 0f), 0f));
            Step(world, 10, 80);
            var inside = VelocityOf(world, ball).Y;
            Check(inside > after + 0.3f, "second field did not push.");
            Ok(world.ForceFields.UpdateField(moved, BoxField(AuraForceFieldKind.Directional, V(5000f, 0f, 0f), V(1f, 1f, 1f), V(0f, 5f, 0f), 0f)), "UpdateField move");
            Step(world, 20, 90);
            Check(VelocityOf(world, ball).Y <= inside + 1e-3f, "zone that moved away kept pushing.");
        }

        private static void Fields_StaleAndInvalid(AuraPhysicsMode mode)
        {
            using var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            var fields = world.ForceFields;
            var good = BoxField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), V(5f, 5f, 5f), V(0f, 1f, 0f), 0f);

            var first = Field(world, good);
            Ok(fields.DestroyField(first), "destroy first");
            Expect(fields.DestroyField(first), AuraResult.InvalidHandle, "double destroy");
            Expect(fields.UpdateField(first, good), AuraResult.InvalidHandle, "update destroyed field");
            Expect(fields.DestroyField(AuraForceFieldId.Invalid), AuraResult.InvalidHandle, "destroy invalid id");
            Expect(fields.UpdateField(AuraForceFieldId.Invalid, good), AuraResult.InvalidHandle, "update invalid id");
            Expect(fields.DestroyField(new AuraForceFieldId(0x7FFFFFFF00000042UL)), AuraResult.InvalidHandle, "destroy garbage id");

            var second = Field(world, good);
            Check(second.Value != first.Value, "slot reuse must bump the generation.");
            Expect(fields.UpdateField(first, good), AuraResult.InvalidHandle, "stale handle must not touch the reused slot");
            Ok(fields.UpdateField(second, good), "update live field");

            Check(!fields.CreateField(SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), 0f, default, 1f)).IsValid, "zero radius sphere accepted.");
            Check(!fields.CreateField(BoxField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), V(1f, 0f, 1f), default, 0f)).IsValid, "flat box accepted.");
            Check(!fields.CreateField(SphereField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), 1f, V(float.NaN, 0f, 0f), 0f)).IsValid, "NaN vector accepted.");
            Check(!fields.CreateField(SphereField(AuraForceFieldKind.Drag, V(0f, 0f, 0f), 1f, default, -1f)).IsValid, "negative drag accepted.");
            Check(!fields.CreateField(SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), 1f, default, 1f, minRadius: 5f, maxRadius: 2f)).IsValid, "min > max radius accepted.");
            Expect(fields.UpdateField(second, SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), -1f, default, 1f)), AuraResult.InvalidDefinition, "update with bad definition");
            Expect(fields.SetGravity(V(float.PositiveInfinity, 0f, 0f)), AuraResult.InvalidDefinition, "non-finite gravity");
            Ok(fields.DestroyField(second), "destroy second");
        }

        private static void Fields_LayerMask(AuraPhysicsMode mode)
        {
            using var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            var onZero = Dyn(world, V(-5f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var onOne = Dyn(world, V(5f, 0f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Ok(world.BodyControl.SetLayer(onOne, new AuraPhysicsLayer(1), AuraPhysicsLayerMask.All), "SetLayer");
            Field(world, SphereField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), 50f, V(0f, 5f, 0f), 0f, layers: AuraPhysicsLayerMask.FromLayer(new AuraPhysicsLayer(1))));
            Step(world, 20);
            Near(VelocityOf(world, onZero).Y, 0f, 1e-4f, "layer 0 body is excluded by the mask");
            Check(VelocityOf(world, onOne).Y > 1f, "layer 1 body is not affected.");
        }

        private static void Fields_WakesSleepingBody(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            Ground(world);
            var ball = Dyn(world, V(0f, 0.6f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Step(world, 400);
            Check(StateOf(world, ball).IsAwake == false, "ball did not fall asleep on the ground.");
            var restY = StateOf(world, ball).Pose.Position.Y;

            Field(world, SphereField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), 5f, V(0f, 40f, 0f), 0f));
            Step(world, 60, 400);
            Check(StateOf(world, ball).Pose.Position.Y > restY + 0.5f, "a field must wake a sleeping body and lift it.");
        }

        private static void Fields_Deterministic(AuraPhysicsMode mode)
        {
            // Two overlapping fields applied to several bodies: the result must repeat exactly.
            var first = RunFieldScenario(mode);
            var second = RunFieldScenario(mode);
            for (var index = 0; index < first.Length; index++)
                Check(first[index] == second[index], $"field scenario diverged at sample {index}: {first[index]} vs {second[index]}.");
        }

        private static float[] RunFieldScenario(AuraPhysicsMode mode)
        {
            using var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            var bodies = new List<PhysicsBodyId>();
            for (var index = 0; index < 6; index++)
                bodies.Add(Dyn(world, V(index * 3f - 8f, index * 0.5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.4f), gravityScale: 1f + index * 0.25f));
            Field(world, SphereField(AuraForceFieldKind.Radial, V(0f, 0f, 0f), 40f, default, 60f, AuraForceFieldMode.Acceleration, AuraForceFieldFalloff.InverseSquare, 1f));
            Field(world, BoxField(AuraForceFieldKind.Drag, V(0f, 0f, 0f), V(30f, 30f, 30f), V(2f, 1f, 0f), 0.5f));
            Step(world, 240);
            var samples = new float[bodies.Count * 3];
            for (var index = 0; index < bodies.Count; index++)
            {
                var pose = StateOf(world, bodies[index]).Pose.Position;
                samples[index * 3] = pose.X;
                samples[index * 3 + 1] = pose.Y;
                samples[index * 3 + 2] = pose.Z;
            }

            return samples;
        }

        private static void Fields_NullBackendReportsUnsupported()
        {
            using var world = new AuraSimulationWorld(NullPhysicsBackend.Instance, new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 4));
            Check((world.Capabilities & AuraPhysicsCapabilities.ForceFields) == 0, "null backend must not advertise ForceFields.");
            Expect(world.ForceFields.SetGravity(V(0f, 1f, 0f)), AuraResult.UnsupportedOperation, "null SetGravity");
            Expect(world.ForceFields.GetGravity(out _), AuraResult.UnsupportedOperation, "null GetGravity");
            Check(!world.ForceFields.CreateField(BoxField(AuraForceFieldKind.Directional, V(0f, 0f, 0f), V(1f, 1f, 1f), V(0f, 1f, 0f), 0f)).IsValid, "null CreateField must be invalid.");
            Expect(world.ForceFields.DestroyField(new AuraForceFieldId(1)), AuraResult.UnsupportedOperation, "null DestroyField");
            Expect(world.BodyControl.SetCollisionDetection(PhysicsBodyId.Invalid, AuraBodyCollisionDetection.Continuous), AuraResult.UnsupportedOperation, "null SetCollisionDetection");
        }

        // Fast projectile at 200 m/s covers 3.3 m per step; the wall is 0.1 m thick.
        // 3D: static wall (Jolt linear cast tests against static bodies). 2D: a heavy dynamic wall, because Box2D
        // already sweeps non-bullet bodies against static geometry and only bullets sweep against dynamic ones.
        private static (AuraSimulationWorld World, PhysicsBodyId Ball, PhysicsBodyId Wall) BuildCcdScene(AuraPhysicsMode mode, AuraBodyCollisionDetection detection)
        {
            var world = NewFieldWorld(mode, V(0f, 0f, 0f));
            var thin = AuraPhysicsShapeDefinition.Box(V(0.05f, 10f, 10f));
            var wallPose = new AuraPose(V(10f, 0f, 0f), AuraQuaternion.Identity);
            var wall = mode == AuraPhysicsMode.Full3D
                ? world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(wallPose, AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, thin))
                : world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(AuraBodyType.Dynamic, wallPose, AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All, new[] { thin }, mass: 1000f, gravityScale: 0f));
            var ball = world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(AuraBodyType.Dynamic,
                new AuraPose(V(0f, 0f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new[] { AuraPhysicsShapeDefinition.Sphere(0.1f) }, gravityScale: 0f, initialLinearVelocity: V(200f, 0f, 0f),
                collisionDetection: detection));
            return (world, ball, wall);
        }

        private static void Ccd_FastBody(AuraPhysicsMode mode)
        {
            var (discrete, discreteBall, _) = BuildCcdScene(mode, AuraBodyCollisionDetection.Discrete);
            using (discrete)
            {
                Step(discrete, 12);
                Check(StateOf(discrete, discreteBall).Pose.Position.X > 10.5f, "control: the discrete projectile was expected to tunnel through the wall, so the test would not prove CCD.");
            }

            var (continuous, ball, wall) = BuildCcdScene(mode, AuraBodyCollisionDetection.Continuous);
            using (continuous)
            {
                Step(continuous, 12);
                var ballX = StateOf(continuous, ball).Pose.Position.X;
                var wallX = StateOf(continuous, wall).Pose.Position.X;
                Check(ballX < wallX, $"the continuous projectile tunnelled (ball x = {ballX}, wall x = {wallX}).");
            }
        }

        private static void Ccd_RuntimeSetter(AuraPhysicsMode mode)
        {
            var (world, ball, wall) = BuildCcdScene(mode, AuraBodyCollisionDetection.Discrete);
            using (world)
            {
                Ok(world.BodyControl.SetCollisionDetection(ball, AuraBodyCollisionDetection.Continuous), "SetCollisionDetection continuous");
                Step(world, 12);
                Check(StateOf(world, ball).Pose.Position.X < StateOf(world, wall).Pose.Position.X, "projectile switched to continuous at runtime still tunnelled.");
            }

            var (back, backBall, _) = BuildCcdScene(mode, AuraBodyCollisionDetection.Continuous);
            using (back)
            {
                Ok(back.BodyControl.SetCollisionDetection(backBall, AuraBodyCollisionDetection.Discrete), "SetCollisionDetection discrete");
                Step(back, 12);
                Check(StateOf(back, backBall).Pose.Position.X > 10.5f, "projectile switched back to discrete should tunnel.");
            }
        }

        private static void Ccd_SetterRejects(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var ground = Ground(world);
            var ball = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Expect(world.BodyControl.SetCollisionDetection(ground, AuraBodyCollisionDetection.Continuous), AuraResult.InvalidDefinition, "static body");
            Expect(world.BodyControl.SetCollisionDetection(ball, (AuraBodyCollisionDetection)7), AuraResult.InvalidDefinition, "bad mode");
            Ok(world.Physics.DestroyBody(ball), "destroy ball");
            Expect(world.BodyControl.SetCollisionDetection(ball, AuraBodyCollisionDetection.Continuous), AuraResult.InvalidHandle, "stale body");
        }

        private static void Time_StepperRatios()
        {
            var stepper = new AuraTimeStepper { Scale = 0.5f };
            var pattern = new int[6];
            for (var index = 0; index < pattern.Length; index++)
                pattern[index] = stepper.Advance(Dt, Dt);
            Check(pattern[0] == 0 && pattern[1] == 1 && pattern[2] == 0 && pattern[3] == 1 && pattern[4] == 0 && pattern[5] == 1,
                $"scale 0.5 must run a step every second tick, got {string.Join(",", pattern)}.");

            stepper = new AuraTimeStepper { Scale = 0.25f };
            var total = 0;
            for (var index = 0; index < 400; index++)
                total += stepper.Advance(Dt, Dt);
            Check(total == 100, $"scale 0.25 over 400 ticks must run 100 steps, ran {total}.");

            stepper.Scale = 2f;
            Check(stepper.Advance(Dt, Dt) == 2, "scale 2 runs two steps per tick.");
            stepper.Scale = 0f;
            Check(stepper.Advance(Dt, Dt) == 0, "scale 0 runs nothing.");
            stepper.Scale = 100f;
            Check(stepper.Advance(Dt, Dt) == stepper.MaxStepsPerAdvance, "step count is capped.");

            var threw = false;
            try { stepper.Scale = -1f; } catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw, "negative scale must be rejected.");
            threw = false;
            try { stepper.Scale = float.NaN; } catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw, "NaN scale must be rejected.");
        }

        private static void Time_ScaleHalf(AuraPhysicsMode mode)
        {
            using var slow = NewWorld(mode);
            using var reference = NewWorld(mode);
            var slowBall = Dyn(slow, V(0f, 100f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var referenceBall = Dyn(reference, V(0f, 100f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            slow.TimeScale = 0.5f;

            var executed = 0;
            var progress = new List<float>();
            for (var tick = 0; tick < 120; tick++)
            {
                executed += slow.Advance(Dt, Dt);
                progress.Add(StateOf(slow, slowBall).Pose.Position.Y);
            }

            Check(executed == 60, $"120 wall ticks at scale 0.5 must run 60 steps, ran {executed}.");
            Step(reference, 60);
            Near(StateOf(slow, slowBall).Pose.Position.Y, StateOf(reference, referenceBall).Pose.Position.Y, 1e-4f, "half speed equals half the steps");
            Near(StateOf(slow, slowBall).LinearVelocity.Y, StateOf(reference, referenceBall).LinearVelocity.Y, 1e-4f, "half speed velocity");
            // Alternate ticks advance, ticks in between do not.
            Check(progress[0] == 100f, "first tick at scale 0.5 must not step.");
            Check(progress[1] < 100f, "second tick at scale 0.5 must step.");
            Check(progress[2] == progress[1], "third tick at scale 0.5 must not step.");
            Check(slow.CurrentTick.Value == 59u, $"tick numbering must count executed steps only (got {slow.CurrentTick.Value}).");

            // Running the slow-motion world twice gives identical results.
            using var again = NewWorld(mode);
            var againBall = Dyn(again, V(0f, 100f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            again.TimeScale = 0.5f;
            for (var tick = 0; tick < 120; tick++)
                again.Advance(Dt, Dt);
            Check(StateOf(again, againBall).Pose.Position.Y == StateOf(slow, slowBall).Pose.Position.Y, "time scaled runs are not deterministic.");
        }

        private static void Time_ZeroPauses()
        {
            using var world = NewWorld();
            var ball = Dyn(world, V(0f, 100f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            world.TimeScale = 0f;
            for (var tick = 0; tick < 30; tick++)
                Check(world.Advance(Dt, Dt) == 0, "paused world must not step.");
            Near(StateOf(world, ball).Pose.Position.Y, 100f, 1e-6f, "paused body");

            world.TimeScale = 1f;
            Check(world.Advance(Dt, Dt) == 1, "resumed world steps once per tick at scale 1.");
            Check(StateOf(world, ball).Pose.Position.Y < 100f, "resumed body must fall.");

            world.TimeScale = 3f;
            Check(world.PlanSteps(Dt, Dt) == 3, "scale 3 plans three steps.");
        }

        private static void HitStop_FreezesAndRestores(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var ball = world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(AuraBodyType.Dynamic,
                new AuraPose(V(0f, 50f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new[] { AuraPhysicsShapeDefinition.Sphere(0.5f) }, initialLinearVelocity: V(3f, 0f, 0f), initialAngularVelocity: V(0f, 0f, 2f)));
            Step(world, 5);
            var before = StateOf(world, ball);
            Check(before.LinearVelocity.Y < -0.3f, "ball should be falling before the hit-stop.");

            Ok(world.HitStop.Begin(ball, 12), "Begin");
            Check(world.HitStop.IsFrozen(ball) && world.HitStop.ActiveCount == 1, "freeze not registered.");
            Step(world, 12, 5);
            var frozen = StateOf(world, ball);
            Near(frozen.Pose.Position.Y, before.Pose.Position.Y, 1e-3f, "frozen y");
            Near(frozen.Pose.Position.X, before.Pose.Position.X, 1e-3f, "frozen x");
            Near(frozen.LinearVelocity.X, 0f, 1e-4f, "frozen velocity");
            Check(world.HitStop.IsFrozen(ball), "freeze must last exactly 12 steps, then release on the next one.");

            Step(world, 1, 17);
            Check(!world.HitStop.IsFrozen(ball) && world.HitStop.ActiveCount == 0, "freeze did not release.");
            var resumed = StateOf(world, ball);
            Near(resumed.LinearVelocity.X, before.LinearVelocity.X, 0.35f, "restored horizontal velocity");
            Near(resumed.LinearVelocity.Y, before.LinearVelocity.Y, 0.35f, "restored vertical velocity");
            Near(resumed.AngularVelocity.Z, 2f, 0.3f, "restored angular velocity");
            Step(world, 60, 18);
            Check(StateOf(world, ball).Pose.Position.Y < before.Pose.Position.Y - 3f, "gravity scale was not restored.");
        }

        private static void HitStop_EdgeCases()
        {
            using var world = NewWorld();
            var a = Dyn(world, V(0f, 50f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var b = Dyn(world, V(5f, 50f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Expect(world.HitStop.Begin(a, 0), AuraResult.InvalidDefinition, "zero ticks");
            Expect(world.HitStop.Begin(PhysicsBodyId.Invalid, 3), AuraResult.InvalidHandle, "invalid body");
            Expect(world.HitStop.Cancel(a), AuraResult.InvalidHandle, "cancel without freeze");

            Ok(world.HitStop.Begin(a, 3), "Begin a");
            Ok(world.HitStop.Begin(a, 6), "extend a");
            Check(world.HitStop.ActiveCount == 1, "extending must not duplicate the entry.");
            Ok(world.HitStop.Begin(b, 100), "Begin b");
            Ok(world.HitStop.Cancel(b), "Cancel b");
            Check(!world.HitStop.IsFrozen(b), "cancelled body still frozen.");
            Step(world, 3);
            Near(StateOf(world, b).LinearVelocity.Y, -9.81f * 3f * Dt, 0.1f, "cancelled body falls again after 3 steps");
            Step(world, 3, 3);
            Check(world.HitStop.IsFrozen(a), "extended freeze (6 steps) ended too early.");
            Step(world, 1, 6);
            Check(!world.HitStop.IsFrozen(a), "extended freeze must release on the seventh step.");
            var staleTarget = Dyn(world, V(9f, 50f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Ok(world.HitStop.Begin(staleTarget, 50), "Begin stale target");
            Step(world, 2, 7);

            // A body destroyed during a freeze is dropped silently.
            Ok(world.Physics.DestroyBody(staleTarget), "destroy frozen body");
            Step(world, 3, 9);
            Check(world.HitStop.ActiveCount == 0, "stale entry kept.");

            var disabled = Dyn(world, V(0f, 80f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Ok(world.BodyControl.SetEnabled(disabled, false), "disable");
            Expect(world.HitStop.Begin(disabled, 3), AuraResult.BodyDisabled, "disabled body");

            using var nullWorld = new AuraSimulationWorld(NullPhysicsBackend.Instance, new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 4));
            Check(nullWorld.HitStop.Begin(PhysicsBodyId.Invalid, 3) != AuraResult.Success, "null backend must not freeze.");
        }
    }
}
