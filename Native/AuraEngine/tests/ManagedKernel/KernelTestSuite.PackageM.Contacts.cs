using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Package M oracles with contacts: restitution, friction (incline, sliding), momentum exchange, stacking,
       sleeping and the contact impulse that carries a weight. */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> MContactTests()
        {
            foreach (var e in new[] { 0f, 0.5f, 0.9f })
            {
                var restitution = e;
                var tag = ((int)(e * 10f)).ToString();
                foreach (var entry in MBoth("m_restitution_bounce_height_ratio_is_e_squared_e0" + tag, mode => MRestitution(mode, restitution))) yield return entry;
            }

            foreach (var entry in MBoth("m_incline_below_friction_angle_stays_at_rest_tan_theta_lt_mu", mode => MInclineAtRest(mode, 0.8f, 25f))) yield return entry;
            foreach (var entry in MBoth("m_incline_above_friction_angle_slides_with_g_sin_minus_mu_cos_mu03", mode => MInclineSlide(mode, 0.3f, 30f))) yield return entry;
            foreach (var entry in MBoth("m_incline_above_friction_angle_slides_with_g_sin_minus_mu_cos_mu05", mode => MInclineSlide(mode, 0.5f, 30f))) yield return entry;
            foreach (var entry in MBoth("m_sliding_box_stops_in_v0_squared_over_2_mu_g", mode => MSlideToRest(mode, 4f, 0.4f))) yield return entry;
            foreach (var entry in MBoth("m_sliding_box_stops_in_v0_squared_over_2_mu_g_low_friction", mode => MSlideToRest(mode, 3f, 0.15f))) yield return entry;
            foreach (var entry in MBoth("m_elastic_head_on_equal_masses_exchange_velocities", mode => MCollision(mode, 1f, 4f, 1f, 0f, 1f))) yield return entry;
            foreach (var entry in MBoth("m_elastic_head_on_unequal_masses_follow_1d_formulas", mode => MCollision(mode, 2f, 3f, 1f, -1f, 1f))) yield return entry;
            foreach (var entry in MBoth("m_elastic_head_on_light_hits_heavy_follow_1d_formulas", mode => MCollision(mode, 1f, 5f, 4f, 0f, 1f))) yield return entry;
            foreach (var entry in MBoth("m_inelastic_head_on_equal_masses_share_momentum", mode => MCollision(mode, 1f, 4f, 1f, 0f, 0f))) yield return entry;
            foreach (var entry in MBoth("m_inelastic_head_on_unequal_masses_move_at_p_over_m", mode => MCollision(mode, 2f, 3f, 1f, -1f, 0f))) yield return entry;
            foreach (var entry in MBoth("m_partially_elastic_head_on_relative_speed_ratio_is_e", mode => MCollision(mode, 3f, 4f, 1f, -2f, 0.5f))) yield return entry;
            foreach (var entry in MBoth("m_incline_frictionless_slides_with_g_sin_theta", mode => MInclineSlide(mode, 0f, 20f))) yield return entry;
        }

        /* Ball radius 0.5 dropped from a 2 m gap onto a static floor, both with restitution e (Average combine gives e).
           Oracle: v_out = e v_in, so the next apex h1 = e^2 h0. The ball is released at gap h0 = 2; semi-implicit
           Euler reaches the floor with a speed lower than sqrt(2 g h0) by about g h / 2 (h = Dt, Dt/4 in Box2D), and
           the sampled velocity sits one half-step off the contact instant. Both are folded into the tolerance on
           the apex ratio: dh/h = 2 dv/v with dv about g h, v about 6.3 m/s. e = 0 must not rebound at all. */
        private static void MRestitution(AuraPhysicsMode mode, float e)
        {
            using var world = MWorld(mode);
            MFloor(world, MMat(0.5f, e));
            var ball = MDynamic(world, V(0f, 2.5f, 0f), MBall(0.5f, MMat(0.5f, e)));
            var tick = 0u;
            var impact = -1;
            var vin = 0f;
            var vout = 0f;
            var apex = 0f;
            var minY = 10f;
            var vmin = 0f;
            for (var step = 0; step < 200; step++)
            {
                MStep(world, ref tick);
                var s = StateOf(world, ball);
                var vy = s.LinearVelocity.Y;
                minY = MathF.Min(minY, s.Pose.Position.Y);
                if (impact < 0)
                {
                    if (vy < vmin) vmin = vy;
                    if (vy > vmin * 0.5f && vmin < -1f) { impact = step; vin = vmin; vout = vy; }
                }
                else
                {
                    if (s.Pose.Position.Y > apex) apex = s.Pose.Position.Y;
                    if (vy < 0f && step > impact + 2) break;
                }
            }

            Check(impact >= 0, "ball never reached the floor");
            const double h0 = 2.0;
            var h1 = apex - 0.5;
            MLog($"impact step {impact}: v_in={vin} v_out={vout} h1={h1} min y={minY}");
            if (e == 0f)
            {
                MNear(h1, 0f, 0.01, "e = 0: no rebound height");
                MNear(StateOf(world, ball).LinearVelocity.Y, 0f, 0.05, "e = 0: no rebound velocity");
                /* The speculative contact lets the ball sink by at most v_in Dt during the impact step (0.1 m here),
                   then pushes it back out; the rest height is the sphere radius within the solver slop. */
                Check(minY >= 0.5f - MathF.Abs(vin) * Dt, $"e = 0: impact penetration {0.5f - minY} exceeds v_in Dt");
                MNear(StateOf(world, ball).Pose.Position.Y, 0.5f, 0.01, "e = 0: rests at contact height");
                return;
            }

            double g = 9.81;
            var h = Dt / (double)MSub(mode);
            var dv = g * Dt;
            var speed = Math.Sqrt(2 * g * h0);
            MNear(h1 / h0, e * e, e * e * 2 * (dv / speed) * 1.5 + 0.01, "h1 / h0 = e^2");
            MNear(-vout / vin, e, e * (dv / speed) * 1.5 + 0.01, "v_out / v_in = e");
        }

        /* Block on a plane tilted by theta, friction mu on both surfaces. Static friction holds while tan(theta) < mu:
           the block must neither slide nor creep (speed and displacement over 3 s stay under the solver slop). */
        private static void MInclineAtRest(AuraPhysicsMode mode, float mu, float degrees)
        {
            using var world = MWorld(mode);
            var theta = degrees * MathF.PI / 180f;
            MFloor(world, MMat(mu, 0f), theta);
            var box = MInclineBox(world, mu, theta, 1f);
            var tick = 0u;
            MStep(world, ref tick, 60);
            var start = StateOf(world, box).Pose.Position;
            MStep(world, ref tick, 180);
            var end = StateOf(world, box);
            Check(Math.Tan(theta) < mu, "test setup: tan(theta) must be below mu");
            var moved = Math.Sqrt(Math.Pow(end.Pose.Position.X - start.X, 2) + Math.Pow(end.Pose.Position.Y - start.Y, 2));
            MNear(moved, 0f, 0.01, $"tan(theta)={Math.Tan(theta):F3} < mu={mu}: displacement over 3 s");
            MNear(Math.Sqrt(end.LinearVelocity.X * end.LinearVelocity.X + end.LinearVelocity.Y * end.LinearVelocity.Y), 0f, 0.02, "speed at rest");
        }

        /* Above the friction angle the block slides with a = g (sin theta - mu cos theta). Acceleration is read from
           two downhill velocity samples one second apart, after the first second of settling, so the contact start-up
           transient cancels. Dominant error: the soft/speculative contact adds a few mm/s of normal slop, so the
           tolerance is 3 percent of a (the discrete integrator itself keeps constant acceleration exact). */
        private static void MInclineSlide(AuraPhysicsMode mode, float mu, float degrees)
        {
            using var world = MWorld(mode);
            var theta = degrees * MathF.PI / 180f;
            MFloor(world, MMat(mu, 0f), theta);
            var box = MInclineBox(world, mu, theta, 2f);
            Check(Math.Tan(theta) > mu, "test setup: tan(theta) must exceed mu");
            var tick = 0u;
            MStep(world, ref tick, 60);
            var v1 = MAlong(StateOf(world, box).LinearVelocity, -Math.Cos(theta), -Math.Sin(theta));
            MStep(world, ref tick, 60);
            var s2 = StateOf(world, box);
            var v2 = MAlong(s2.LinearVelocity, -Math.Cos(theta), -Math.Sin(theta));
            var a = (v2 - v1) / (60.0 * Dt);
            var expected = 9.81 * (Math.Sin(theta) - mu * Math.Cos(theta));
            MNear(a, expected, 0.03 * expected + 0.01, $"a = g (sin theta - mu cos theta), mu={mu}");
            var across = s2.LinearVelocity.X * Math.Sin(theta) - s2.LinearVelocity.Y * Math.Cos(theta);
            MNear(across, 0f, 0.02, "no velocity into or away from the plane");
        }

        private static PhysicsBodyId MInclineBox(AuraSimulationWorld world, float mu, float theta, float mass)
        {
            /* Local frame of the plane: x along the slope (uphill), y along its normal; the plane top is local y = 0. */
            var rotation = AuraQuaternion.FromAxisAngle(AuraVector3.UnitZ, theta);
            var centre = rotation.Rotate(V(0f, 0.5f + 0.002f, 0f));
            return MDynamic(world, centre, MBox(0.5f, 0.5f, MMat(mu, 0f)), mass: mass, angle: theta);
        }

        /* Box with initial speed v0 on a flat floor, friction mu on both surfaces: constant deceleration mu g,
           so d = v0^2 / (2 mu g) and t_stop = v0 / (mu g). Per step the velocity falls by exactly mu g Dt (Jolt) so
           the discrete path is short of d by v0 h / 2 (h = Dt, or Dt/4 in Box2D's sub-steps); that bound is the
           tolerance, plus 1 cm for the settle offset. */
        private static void MSlideToRest(AuraPhysicsMode mode, float v0, float mu)
        {
            using var world = MWorld(mode);
            MFloor(world, MMat(mu, 0f));
            var box = MDynamic(world, V(0f, 0.502f, 0f), MBox(0.5f, 0.5f, MMat(mu, 0f)));
            var tick = 0u;
            MStep(world, ref tick, 30);
            var x0 = StateOf(world, box).Pose.Position.X;
            Ok(world.BodyControl.SetLinearVelocity(box, V(v0, 0f, 0f)), "launch");
            var stopStep = -1;
            for (var step = 1; step <= 600; step++)
            {
                MStep(world, ref tick);
                if (StateOf(world, box).LinearVelocity.X <= 1e-3f) { stopStep = step; break; }
            }

            Check(stopStep > 0, "box never stopped");
            MStep(world, ref tick, 30);
            var end = StateOf(world, box);
            double g = 9.81;
            var h = Dt / (double)MSub(mode);
            MNear(end.Pose.Position.X - x0, v0 * v0 / (2 * g * mu), v0 * h / 2.0 * 1.2 + 0.01, "d = v0^2 / (2 mu g)");
            MNear(stopStep * (double)Dt, v0 / (g * mu), 2.0 * Dt, "t_stop = v0 / (mu g)");
            MNear(end.LinearVelocity.X, 0f, 1e-3, "stays stopped");
            MNear(end.Pose.Position.Y, 0.5f, 0.01, "no hopping");
        }

        /* Two frictionless balls of masses m1, m2 on the x axis, zero gravity, restitution e on both (Average
           combine gives e). Relative velocity ratio: (v2' - v1') = -e (v2 - v1). Momentum p = m1 v1 + m2 v2 is
           conserved (0.5 percent); the 1D formulas give the outgoing velocities:
             v1' = (m1 v1 + m2 v2 + m2 e (v2 - v1)) / (m1 + m2),  v2' = (m1 v1 + m2 v2 + m1 e (v1 - v2)) / (m1 + m2).
           e = 1 conserves kinetic energy (2 percent), e = 0 leaves a common velocity p / (m1 + m2). */
        private static void MCollision(AuraPhysicsMode mode, float m1, float v1, float m2, float v2, float e)
        {
            using var world = MWorld(mode, AuraVector3.Zero);
            var a = MDynamic(world, V(-3f, 0f, 0f), MBall(0.5f, MMat(0f, e)), mass: m1, velocity: V(v1, 0f, 0f));
            var b = MDynamic(world, V(3f, 0f, 0f), MBall(0.5f, MMat(0f, e)), mass: m2, velocity: V(v2, 0f, 0f));
            var tick = 0u;
            MStep(world, ref tick, 150);
            var sa = StateOf(world, a);
            var sb = StateOf(world, b);
            double total = m1 + m2;
            double p0 = m1 * v1 + m2 * v2;
            double scale = m1 * Math.Abs(v1) + m2 * Math.Abs(v2);
            double e0 = 0.5 * m1 * v1 * v1 + 0.5 * m2 * v2 * v2;
            double p1 = m1 * sa.LinearVelocity.X + m2 * sb.LinearVelocity.X;
            double e1 = 0.5 * m1 * sa.LinearVelocity.X * sa.LinearVelocity.X + 0.5 * m2 * sb.LinearVelocity.X * sb.LinearVelocity.X;
            MNear(p1, p0, 0.005 * scale, "momentum conserved (0.5 percent of sum m|v|)");
            var ea = (p0 + m2 * e * (v2 - v1)) / total;
            var eb = (p0 + m1 * e * (v1 - v2)) / total;
            var vTol = 0.01 * Math.Max(Math.Abs(v1), Math.Abs(v2)) + 0.005;
            MNear(sa.LinearVelocity.X, ea, vTol, "v1' from the 1D collision formula");
            MNear(sb.LinearVelocity.X, eb, vTol, "v2' from the 1D collision formula");
            MNear(sb.LinearVelocity.X - sa.LinearVelocity.X, -e * (v2 - v1), vTol, "relative velocity ratio is e");
            if (e == 1f)
                MNear(e1, e0, 0.02 * e0, "kinetic energy conserved (2 percent)");
            if (e == 0f)
            {
                MNear(sa.LinearVelocity.X, p0 / total, vTol, "common velocity = p / M (a)");
                MNear(sb.LinearVelocity.X, p0 / total, vTol, "common velocity = p / M (b)");
            }

            MNear(sa.LinearVelocity.Y, 0f, 1e-3, "no transverse velocity a");
            MNear(sb.LinearVelocity.Y, 0f, 1e-3, "no transverse velocity b");
        }
    }
}
