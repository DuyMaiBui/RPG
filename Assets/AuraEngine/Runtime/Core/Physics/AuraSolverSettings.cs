namespace AuraEngine.Core
{
    public sealed class AuraSolverSettings
    {
        public int VelocityIterations { get; set; } = 10;

        public int PositionIterations { get; set; } = 4;

        public float Baumgarte { get; set; } = 0.2f;

        public float PenetrationSlop { get; set; } = 0.005f;

        public float RestingVelocityThreshold { get; set; } = 0.05f;

        public float AngularSleepThreshold { get; set; } = 0.05f;

        public float TimeToSleep { get; set; } = 0.5f;

        public bool AllowSleep { get; set; } = true;

        public float MaxLinearVelocity { get; set; } = 1000f;

        public float MaxAngularVelocity { get; set; } = 100f;

        public int MaxContactPoints { get; set; } = 4;

        public AuraSolverSettings Clone() =>
            new AuraSolverSettings
            {
                VelocityIterations = VelocityIterations,
                PositionIterations = PositionIterations,
                Baumgarte = Baumgarte,
                PenetrationSlop = PenetrationSlop,
                RestingVelocityThreshold = RestingVelocityThreshold,
                AngularSleepThreshold = AngularSleepThreshold,
                TimeToSleep = TimeToSleep,
                AllowSleep = AllowSleep,
                MaxLinearVelocity = MaxLinearVelocity,
                MaxAngularVelocity = MaxAngularVelocity,
                MaxContactPoints = MaxContactPoints,
            };
    }
}
