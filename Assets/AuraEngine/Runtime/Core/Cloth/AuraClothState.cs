namespace AuraEngine.Core
{
    public readonly struct AuraClothState
    {
        public AuraClothState(AuraClothId cloth, AuraVector3[] positions)
        {
            Cloth = cloth;
            Positions = positions;
        }
        public AuraClothId Cloth { get; }
        public AuraVector3[] Positions { get; }
    }
}
