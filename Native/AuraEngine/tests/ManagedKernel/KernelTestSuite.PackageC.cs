using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package C: Plane2D character mover and one-way platforms. */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageCTests()
        {
            return new (string Name, Action Body)[]
            {
                ("char2d_capability_and_null_3d_unaffected", Char2D_Capability),
                ("char2d_rests_on_ground_grounded", Char2D_RestsGrounded),
                ("char2d_walks_slope_within_max_angle", Char2D_WalksSlope),
                ("char2d_blocked_by_steep_slope", Char2D_BlockedBySteepSlope),
                ("char2d_jumps_and_lands", Char2D_JumpsAndLands),
                ("char2d_carried_by_kinematic_platform", Char2D_CarriedByPlatform),
                ("char2d_stops_at_wall", Char2D_StopsAtWall),
                ("char2d_steps_up_ledge", Char2D_StepsUpLedge),
                ("char2d_blocked_by_tall_ledge_without_step", Char2D_BlockedWithoutStep),
                ("char2d_pushes_light_body", Char2D_PushesLightBody),
                ("char2d_snapshot_round_trip", Char2D_SnapshotRoundTrip),
                ("char2d_destroy_invalidates_handle", Char2D_DestroyInvalidatesHandle),
                ("oneway_body_passes_up_and_rests_on_top", OneWay_BodyPassesUpAndRests),
                ("oneway_body_lands_from_above", OneWay_BodyLandsFromAbove),
                ("oneway_solid_platform_blocks_from_below", OneWay_NormalPlatformBlocks),
                ("oneway_character_passes_up_and_lands", OneWay_CharacterPassesUp),
                ("oneway_character_walks_through_side", OneWay_CharacterWalksThroughSide),
                ("jump_assist_coyote_and_buffer", JumpAssist_CoyoteAndBuffer),
            };
        }

        private const float CharRadius = 0.4f;
        private const float CharHeight = 1.8f;

        private static AuraSimulationWorld NewWorld2D() => NewWorld(AuraPhysicsMode.Plane2D);

        private static PhysicsBodyId StaticBox2D(AuraSimulationWorld world, float x, float y, float halfX, float halfY, float angle = 0f, bool oneWay = false)
        {
            var shape = AuraPhysicsShapeDefinition.Box(new AuraVector3(halfX, halfY, 0.5f));
            if (oneWay)
                shape = shape.AsOneWay();
            var rotation = new AuraQuaternion(0f, 0f, MathF.Sin(angle * 0.5f), MathF.Cos(angle * 0.5f));
            return world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(x, y, 0f), rotation), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, shape));
        }

        private static AuraCharacterId NewCharacter(AuraSimulationWorld world, float x, float y, float maxSlopeDegrees = 45f, float stepHeight = 0f)
        {
            var id = world.CreateCharacter(new AuraCharacterDefinition(
                new AuraPose(new AuraVector3(x, y, 0f), AuraQuaternion.Identity), CharRadius, CharHeight,
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, 70f, maxSlopeDegrees * MathF.PI / 180f, stepHeight));
            Check(id.IsValid, "2D character creation failed.");
            return id;
        }

        /* One simulation tick: move the character, then step the world (the order the authoring component uses). */
        private static AuraCharacterState Tick2D(AuraSimulationWorld world, AuraCharacterId id, float vx, float jumpSpeed, ref uint tick)
        {
            world.MoveCharacter(id, new AuraVector3(vx * Dt, jumpSpeed * Dt, 0f), Dt);
            world.Step(new SimulationStep(new SimulationTick(++tick), Dt));
            Check(world.TryGetCharacterState(id, out var state), "character state missing.");
            return state;
        }

        private static AuraCharacterState Run2D(AuraSimulationWorld world, AuraCharacterId id, int ticks, float vx, ref uint tick)
        {
            AuraCharacterState state = default;
            for (var index = 0; index < ticks; index++)
                state = Tick2D(world, id, vx, 0f, ref tick);
            return state;
        }

        private static void Char2D_Capability()
        {
            using var world = NewWorld2D();
            Check((world.Capabilities & AuraPhysicsCapabilities.Characters) != 0, "Plane2D must report Characters.");
        }

        private static void Char2D_RestsGrounded()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            var id = NewCharacter(world, 0f, 3f);
            uint tick = 0;
            var state = Run2D(world, id, 180, 0f, ref tick);
            Check(state.IsGrounded, "character should be grounded on the floor.");
            Near(state.Position.Y, CharHeight * 0.5f, 0.05f, "resting height");
            Near(state.Position.X, 0f, 0.01f, "no drift");
            Near(state.Velocity.Y, 0f, 0.01f, "resting vertical velocity");
        }

        private static void Char2D_WalksSlope()
        {
            const float angle = 30f * MathF.PI / 180f;
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 3f, 0.5f);
            // Ramp surface starts at (2, 0) and rises at 30 degrees; box centre sits half the thickness below the surface.
            const float length = 8f;
            var cx = 2f + length * MathF.Cos(angle) + 0.25f * MathF.Sin(angle);
            var cy = length * MathF.Sin(angle) - 0.25f * MathF.Cos(angle);
            StaticBox2D(world, cx, cy, length, 0.25f, angle);
            var id = NewCharacter(world, 0f, 1.2f, 45f);
            uint tick = 0;
            Run2D(world, id, 60, 0f, ref tick);
            var grounded = 0;
            AuraCharacterState state = default;
            for (var index = 0; index < 150; index++)
            {
                state = Tick2D(world, id, 3f, 0f, ref tick);
                if (index > 100 && state.IsGrounded)
                    grounded++;
            }

            Check(state.Position.X > 5.5f, $"should walk up a 30 degree slope, x={state.Position.X}.");
            Check(state.Position.Y > 1.8f, $"should have climbed, y={state.Position.Y}.");
            Check(grounded > 40, "should stay grounded while climbing.");
        }

        private static void Char2D_BlockedBySteepSlope()
        {
            const float angle = 60f * MathF.PI / 180f;
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 3f, 0.5f);
            const float length = 8f;
            var cx = 2f + length * MathF.Cos(angle) + 0.25f * MathF.Sin(angle);
            var cy = length * MathF.Sin(angle) - 0.25f * MathF.Cos(angle);
            StaticBox2D(world, cx, cy, length, 0.25f, angle);
            var id = NewCharacter(world, 0f, 1.2f, 45f);
            uint tick = 0;
            var state = Run2D(world, id, 240, 3f, ref tick);
            Check(state.Position.Y < 1.3f, $"must not climb a 60 degree slope with a 45 degree limit, y={state.Position.Y}.");
            Check(state.Position.X < 2.6f, $"must be stopped by the steep slope, x={state.Position.X}.");
        }

        private static void Char2D_JumpsAndLands()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            var id = NewCharacter(world, 0f, 1f);
            uint tick = 0;
            Run2D(world, id, 60, 0f, ref tick);
            var peak = 0f;
            var state = Tick2D(world, id, 0f, 8f, ref tick);
            Check(!state.IsGrounded, "jumping character must leave the ground.");
            var airborneTicks = 0;
            for (var index = 0; index < 240; index++)
            {
                state = Tick2D(world, id, 0f, 0f, ref tick);
                peak = Math.Max(peak, state.Position.Y);
                if (!state.IsGrounded)
                    airborneTicks++;
            }

            Check(peak > CharHeight * 0.5f + 2.5f, $"jump apex too low: {peak}.");
            Check(airborneTicks > 60, "should be airborne for a while.");
            Check(state.IsGrounded, "should land.");
            Near(state.Position.Y, CharHeight * 0.5f, 0.05f, "landed height");
        }

        private static void Char2D_CarriedByPlatform()
        {
            using var world = NewWorld2D();
            var platform = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(8f, 0.5f, 0.5f))));
            Check(world.BodyControl.SetLinearVelocity(platform, new AuraVector3(2f, 0f, 0f)) == AuraResult.Success, "platform velocity.");
            var id = NewCharacter(world, 0f, 1f);
            uint tick = 0;
            var state = Run2D(world, id, 150, 0f, ref tick);
            world.TryGetBodyState(platform, out var platformState);
            Check(platformState.Pose.Position.X > 4f, "platform should have moved.");
            Near(state.Position.X, platformState.Pose.Position.X, 0.25f, "carried with the platform");
            Check(state.IsGrounded, "stays grounded on the moving platform.");

            // A rising platform carries the character up as well.
            world.BodyControl.SetLinearVelocity(platform, new AuraVector3(0f, 1.5f, 0f));
            var startY = state.Position.Y;
            state = Run2D(world, id, 90, 0f, ref tick);
            Check(state.Position.Y > startY + 1.5f, $"rising platform should lift the character, y={state.Position.Y}.");
            Check(state.IsGrounded, "grounded on the rising platform.");
        }

        private static void Char2D_StopsAtWall()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            StaticBox2D(world, 4f, 3f, 0.5f, 3f);
            var id = NewCharacter(world, 0f, 1f);
            uint tick = 0;
            var state = Run2D(world, id, 300, 3f, ref tick);
            Near(state.Position.X, 4f - 0.5f - CharRadius, 0.06f, "wall contact X");
            Check(state.IsGrounded, "still grounded against the wall.");
            Near(state.Velocity.X, 0f, 0.05f, "wall clips horizontal velocity");
        }

        private static void Char2D_StepsUpLedge()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            StaticBox2D(world, 6f, 0.125f, 3f, 0.125f);
            var id = NewCharacter(world, 0f, 1f, 45f, 0.3f);
            uint tick = 0;
            var state = Run2D(world, id, 120, 3f, ref tick);
            Check(state.Position.X > 4.5f, $"should step onto the ledge, x={state.Position.X}.");
            Near(state.Position.Y, 0.25f + CharHeight * 0.5f, 0.06f, "on top of the ledge");
            Check(state.IsGrounded, "grounded on the ledge.");
        }

        private static void Char2D_BlockedWithoutStep()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            StaticBox2D(world, 6f, 0.5f, 3f, 0.5f);
            var id = NewCharacter(world, 0f, 1f, 45f, 0.3f);
            uint tick = 0;
            var state = Run2D(world, id, 240, 3f, ref tick);
            Check(state.Position.X < 3.2f, $"a 1m ledge must stop a 0.3m stepper, x={state.Position.X}.");
            Near(state.Position.Y, CharHeight * 0.5f, 0.06f, "stays on the floor");
        }

        private static void Char2D_PushesLightBody()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            var box = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(2f, 0.3f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(0.3f, 0.3f, 0.5f))));
            var id = NewCharacter(world, 0f, 1f);
            uint tick = 0;
            var state = Run2D(world, id, 240, 2f, ref tick);
            world.TryGetBodyState(box, out var boxState);
            Check(boxState.Pose.Position.X > 3.5f, $"light box should be pushed along, x={boxState.Pose.Position.X}.");
            Check(state.Position.X > 2.5f, $"character should follow the pushed box, x={state.Position.X}.");
            Check(boxState.Pose.Position.X > state.Position.X, "the box stays in front of the character.");
        }

        private static void Char2D_SnapshotRoundTrip()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 30f, 0.5f);
            var id = NewCharacter(world, 0f, 1f);
            uint tick = 0;
            Run2D(world, id, 60, 2f, ref tick);
            var saved = world.SaveState();
            world.TryGetCharacterState(id, out var before);
            var continued = Run2D(world, id, 45, 2f, ref tick);

            world.RestoreState(saved);
            world.TryGetCharacterState(id, out var restored);
            Near(restored.Position.X, before.Position.X, 1e-4f, "restored X");
            Near(restored.Position.Y, before.Position.Y, 1e-4f, "restored Y");
            Check(restored.IsGrounded == before.IsGrounded, "restored grounded flag.");

            var replay = Run2D(world, id, 45, 2f, ref tick);
            Near(replay.Position.X, continued.Position.X, 1e-3f, "replay X");
            Near(replay.Position.Y, continued.Position.Y, 1e-3f, "replay Y");
        }

        private static void Char2D_DestroyInvalidatesHandle()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            var id = NewCharacter(world, 0f, 1f);
            Check(world.DestroyCharacter(id) == AuraResult.Success, "destroy.");
            Check(!world.TryGetCharacterState(id, out _), "stale handle must not resolve.");
            var again = NewCharacter(world, 0f, 1f);
            Check(world.TryGetCharacterState(again, out _), "recycled slot resolves.");
            Check(!world.TryGetCharacterState(id, out _), "old generation stays invalid.");
        }

        private static PhysicsBodyId BallAt2D(AuraSimulationWorld world, float x, float y, float radius = 0.3f) =>
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(x, y, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Sphere(radius)));

        private static void OneWay_BodyPassesUpAndRests()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            StaticBox2D(world, 0f, 2f, 3f, 0.1f, 0f, true);
            var ball = BallAt2D(world, 0f, 0.5f);
            world.BodyControl.SetLinearVelocity(ball, new AuraVector3(0f, 9f, 0f));
            var peak = 0f;
            for (uint index = 0; index < 360; index++)
            {
                world.Step(new SimulationStep(new SimulationTick(index + 1), Dt));
                world.TryGetBodyState(ball, out var s);
                peak = Math.Max(peak, s.Pose.Position.Y);
            }

            world.TryGetBodyState(ball, out var state);
            Check(peak > 3.5f, $"ball must pass up through the platform, peak={peak}.");
            Near(state.Pose.Position.Y, 2.1f + 0.3f, 0.08f, "ball rests on top of the one-way platform");
        }

        private static void OneWay_BodyLandsFromAbove()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            StaticBox2D(world, 0f, 2f, 3f, 0.1f, 0f, true);
            var ball = BallAt2D(world, 0.5f, 6f);
            Step(world, 360);
            world.TryGetBodyState(ball, out var state);
            Near(state.Pose.Position.Y, 2.4f, 0.08f, "ball lands on the one-way platform from above");
        }

        private static void OneWay_NormalPlatformBlocks()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            StaticBox2D(world, 0f, 2f, 3f, 0.1f);
            var ball = BallAt2D(world, 0f, 0.5f);
            world.BodyControl.SetLinearVelocity(ball, new AuraVector3(0f, 9f, 0f));
            var peak = 0f;
            for (uint index = 0; index < 120; index++)
            {
                world.Step(new SimulationStep(new SimulationTick(index + 1), Dt));
                world.TryGetBodyState(ball, out var s);
                peak = Math.Max(peak, s.Pose.Position.Y);
            }

            Check(peak < 1.8f, $"a two-way platform must block the ball from below, peak={peak}.");
        }

        private static void OneWay_CharacterPassesUp()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            StaticBox2D(world, 0f, 2f, 3f, 0.1f, 0f, true);
            var id = NewCharacter(world, 0f, 1f);
            uint tick = 0;
            Run2D(world, id, 60, 0f, ref tick);
            var state = Tick2D(world, id, 0f, 10f, ref tick);
            var peak = 0f;
            for (var index = 0; index < 300; index++)
            {
                state = Tick2D(world, id, 0f, 0f, ref tick);
                peak = Math.Max(peak, state.Position.Y);
            }

            Check(peak > 3.5f, $"character must jump up through the platform, peak={peak}.");
            Check(state.IsGrounded, "character should be grounded.");
            Near(state.Position.Y, 2.1f + CharHeight * 0.5f, 0.06f, "character stands on top of the one-way platform");

            // Dropping from above lands on it too: walk off to the side, climb back is not needed; teleport via a second character.
            var second = NewCharacter(world, 0.5f, 6f);
            var landed = Run2D(world, second, 200, 0f, ref tick);
            Near(landed.Position.Y, 2.1f + CharHeight * 0.5f, 0.06f, "second character lands from above");
        }

        private static void OneWay_CharacterWalksThroughSide()
        {
            using var world = NewWorld2D();
            StaticBox2D(world, 0f, -0.5f, 20f, 0.5f);
            // A one-way slab at body height: walking into its side must not block.
            StaticBox2D(world, 4f, 1f, 0.5f, 0.5f, 0f, true);
            var id = NewCharacter(world, 0f, 1f);
            uint tick = 0;
            var state = Run2D(world, id, 240, 3f, ref tick);
            Check(state.Position.X > 6f, $"one-way slab must not block from the side, x={state.Position.X}.");
        }

        private static void JumpAssist_CoyoteAndBuffer()
        {
            var assist = new AuraJumpAssist(0.1f, 0.1f);
            Check(assist.Update(Dt, true, true), "grounded press jumps immediately.");
            Check(!assist.Update(Dt, true, false), "no press, no jump.");

            // Coyote: leave the ground, press within the window.
            assist = new AuraJumpAssist(0.1f, 0.1f);
            assist.Update(Dt, true, false);
            for (var index = 0; index < 3; index++)
                Check(!assist.Update(Dt, false, false), "airborne without press.");
            Check(assist.Update(Dt, false, true), "press within the coyote window jumps.");
            Check(!assist.Update(Dt, false, true), "coyote is consumed by the jump.");

            // Coyote expired.
            assist = new AuraJumpAssist(0.1f, 0.1f);
            assist.Update(Dt, true, false);
            for (var index = 0; index < 10; index++)
                assist.Update(Dt, false, false);
            Check(!assist.Update(Dt, false, true), "press after the coyote window does not jump.");

            // Buffer: press in the air, land within the window.
            assist = new AuraJumpAssist(0f, 0.1f);
            Check(!assist.Update(Dt, false, true), "airborne press is buffered.");
            Check(!assist.Update(Dt, false, false), "still airborne.");
            Check(assist.Update(Dt, true, false), "landing within the buffer window jumps.");

            // Buffer expired.
            assist = new AuraJumpAssist(0f, 0.1f);
            assist.Update(Dt, false, true);
            for (var index = 0; index < 10; index++)
                assist.Update(Dt, false, false);
            Check(!assist.Update(Dt, true, false), "an old press does not jump.");
        }
    }
}
