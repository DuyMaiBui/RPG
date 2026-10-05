using System;

namespace AuraEngine.Core
{
    public sealed class AuraWorldDefinition
    {
        public AuraWorldDefinition(
            AuraPhysicsMode mode = AuraPhysicsMode.Full3D,
            AuraVector3? gravity = null,
            AuraCollisionMatrix collisionMatrix = null,
            int initialBodyCapacity = 256,
            float fixedDeltaTime = 1f / 60f,
            AuraSolverSettings solverSettings = null)
        {
            Mode = mode;
            // null means "not specified"; an explicit zero vector is a valid zero-gravity world.
            Gravity = gravity ?? new AuraVector3(0f, -9.81f, 0f);
            CollisionMatrix = collisionMatrix ?? AuraCollisionMatrix.CreateAllCollide();
            InitialBodyCapacity = initialBodyCapacity;
            FixedDeltaTime = fixedDeltaTime;
            SolverSettings = solverSettings ?? new AuraSolverSettings();
        }

        public AuraPhysicsMode Mode { get; }
        public AuraVector3 Gravity { get; }
        public AuraCollisionMatrix CollisionMatrix { get; }
        public int InitialBodyCapacity { get; }
        public float FixedDeltaTime { get; }
        public AuraSolverSettings SolverSettings { get; }

        public AuraResult Validate()
        {
            if (InitialBodyCapacity <= 0)
                return AuraResult.InvalidDefinition;

            if (FixedDeltaTime <= 0f)
                return AuraResult.InvalidDefinition;

            return AuraResult.Success;
        }
    }
}
