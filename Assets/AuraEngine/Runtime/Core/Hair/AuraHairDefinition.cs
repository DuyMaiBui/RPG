namespace AuraEngine.Core
{
    public readonly struct AuraHairDefinition
    {
        public AuraHairDefinition(AuraPose initialPose, AuraVector3[] strandRoots, int pointsPerStrand, float segmentLength)
        {
            InitialPose = initialPose;
            StrandRoots = strandRoots;
            PointsPerStrand = pointsPerStrand;
            SegmentLength = segmentLength;
        }

        public AuraPose InitialPose { get; }
        public AuraVector3[] StrandRoots { get; }
        public int PointsPerStrand { get; }
        public float SegmentLength { get; }
    }
}
