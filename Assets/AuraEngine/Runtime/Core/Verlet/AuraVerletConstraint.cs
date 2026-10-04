namespace AuraEngine.Core
{
    public readonly struct AuraVerletConstraint
    {
        public AuraVerletConstraint(int particleA, int particleB, float restLength)
        {
            ParticleA = particleA;
            ParticleB = particleB;
            RestLength = restLength;
        }

        public int ParticleA { get; }
        public int ParticleB { get; }
        public float RestLength { get; }
    }
}
