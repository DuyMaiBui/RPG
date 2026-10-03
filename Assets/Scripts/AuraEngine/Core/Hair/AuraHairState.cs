namespace AuraEngine.Core
{
    public readonly struct AuraHairState
    {
        public AuraHairState(AuraHairId hair, AuraVector3[] points)
        {
            Hair = hair;
            Points = points;
        }

        public AuraHairId Hair { get; }
        public AuraVector3[] Points { get; }
    }
}
