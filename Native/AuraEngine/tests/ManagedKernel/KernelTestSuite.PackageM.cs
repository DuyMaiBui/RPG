using System;
using System.Collections.Generic;
using System.Linq;
using AuraEngine.Core;
using AuraEngine.Physics.Native;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Package M: state-based physics oracles. Every case builds a minimal world, runs a deterministic number of
       fixed steps (Dt = 1/60), reads the body state back and compares it with a value derived from the physical
       law in the test. Cases run on Jolt (Full3D) and Box2D (Plane2D) through one parameterised body.

       Discretisation facts the tolerances rest on (both verified against the kernel sources):
         - Jolt: one collision step per Step(Dt), semi-implicit Euler (v += g dt, then x += v dt).
         - Box2D: b2World_Step(dt, 4 sub-steps), semi-implicit Euler with h = dt / 4 per sub-step.
       Semi-implicit Euler from rest gives y_n = -g h^2 n (n + 1) / 2, i.e. the continuous -g t^2 / 2 minus
       g t h / 2: a first-order lag of g t h / 2 (h = Dt for Jolt, Dt / 4 for Box2D) that the tolerances name
       explicitly instead of widening them blindly. Set AURA_M_VERBOSE=1 to print the measured numbers and
       AURA_M_ONLY=<substring> to run only the matching package M cases. */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageMTests()
        {
            var all = MKinematicsTests()
                .Concat(MContactTests())
                .Concat(MDynamicsTests());
            var only = Environment.GetEnvironmentVariable("AURA_M_ONLY");
            if (!string.IsNullOrEmpty(only))
                all = all.Where(entry => entry.Name.Contains(only));
            return all.ToArray();
        }

        private static readonly AuraPhysicsMode[] MModes = { AuraPhysicsMode.Full3D, AuraPhysicsMode.Plane2D };

        private static string MSuffix(AuraPhysicsMode mode) => mode == AuraPhysicsMode.Full3D ? "3d" : "2d";

        /* Integrator sub-steps per Step(Dt): 1 for Jolt, 4 for Box2D. */
        private static int MSub(AuraPhysicsMode mode) => mode == AuraPhysicsMode.Full3D ? 1 : 4;

        private static IEnumerable<(string Name, Action Body)> MBoth(string name, Action<AuraPhysicsMode> body)
        {
            foreach (var mode in MModes)
            {
                var captured = mode;
                yield return (name + "_" + MSuffix(mode), () => body(captured));
            }
        }

        private static void MLog(string message)
        {
            if (Environment.GetEnvironmentVariable("AURA_M_VERBOSE") == "1")
                Console.WriteLine("    [M] " + message);
        }

        private static AuraSimulationWorld MWorld(AuraPhysicsMode mode, AuraVector3? gravity = null) =>
            new AuraSimulationWorld(new NativePhysicsBackend(), new AuraWorldDefinition(mode, gravity, null, 32));

        private static AuraPhysicsMaterialDefinition MMat(float friction, float restitution) =>
            new AuraPhysicsMaterialDefinition(friction, restitution, 1000f);

        private static AuraPhysicsShapeDefinition MBall(float radius, AuraPhysicsMaterialDefinition material) =>
            AuraPhysicsShapeDefinition.Sphere(radius).WithMaterial(material);

        private static AuraPhysicsShapeDefinition MBox(float hx, float hy, AuraPhysicsMaterialDefinition material) =>
            AuraPhysicsShapeDefinition.Box(V(hx, hy, 0.5f)).WithMaterial(material);

        private static PhysicsBodyId MDynamic(
            AuraSimulationWorld world,
            AuraVector3 position,
            AuraPhysicsShapeDefinition shape,
            float mass = 1f,
            float gravityScale = 1f,
            AuraVector3 velocity = default,
            AuraVector3 angularVelocity = default,
            float linearDamping = 0f,
            float angularDamping = 0f,
            float angle = 0f,
            bool allowSleeping = true) =>
            world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(
                AuraBodyType.Dynamic,
                new AuraPose(position, AuraQuaternion.FromAxisAngle(AuraVector3.UnitZ, angle)),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                new[] { shape },
                mass: mass,
                gravityScale: gravityScale,
                initialLinearVelocity: velocity,
                initialAngularVelocity: angularVelocity,
                linearDamping: linearDamping,
                angularDamping: angularDamping,
                allowSleeping: allowSleeping));

        /* Static slab whose top face is the plane through the origin, tilted by angle about +Z. */
        private static PhysicsBodyId MFloor(AuraSimulationWorld world, AuraPhysicsMaterialDefinition material, float angle = 0f)
        {
            var slab = AuraPhysicsShapeDefinition.Box(V(40f, 0.5f, 40f)).WithMaterial(material);
            var pose = new AuraPose(
                AuraQuaternion.FromAxisAngle(AuraVector3.UnitZ, angle).Rotate(V(0f, -0.5f, 0f)),
                AuraQuaternion.FromAxisAngle(AuraVector3.UnitZ, angle));
            return world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                pose, AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, slab));
        }

        private static void MStep(AuraSimulationWorld world, ref uint tick, int count = 1)
        {
            for (var index = 0; index < count; index++)
                world.Step(new SimulationStep(new SimulationTick(++tick), Dt));
        }

        /* Rotation about +Z in radians, valid while the body only rotates about Z. */
        private static float MAngleZ(AuraBodyState state) =>
            2f * MathF.Atan2(state.Pose.Rotation.Z, state.Pose.Rotation.W);

        private static float MWrap(float angle)
        {
            while (angle > MathF.PI) angle -= 2f * MathF.PI;
            while (angle < -MathF.PI) angle += 2f * MathF.PI;
            return angle;
        }

        private static void MNear(double actual, double expected, double tolerance, string label)
        {
            MLog($"{label}: actual={actual:G7} expected={expected:G7} diff={actual - expected:G3} tol={tolerance:G3}");
            if (!(Math.Abs(actual - expected) <= tolerance))
                throw new Exception($"{label}: expected {expected:G7} +/- {tolerance:G3}, got {actual:G7} (diff {actual - expected:G3}).");
        }

        /* Planar speed along a unit direction. */
        private static double MAlong(AuraVector3 vector, double dx, double dy) => vector.X * dx + vector.Y * dy;
    }
}
