namespace AuraEngine.Core
{
    public readonly struct AuraClothDefinition
    {
        public AuraClothDefinition(int width, int height, float spacing, AuraPose origin, float damping, float gravityScale)
        {
            Width = width;
            Height = height;
            Spacing = spacing;
            Origin = origin;
            Damping = damping;
            GravityScale = gravityScale;
        }

        public int Width { get; }
        public int Height { get; }
        public float Spacing { get; }
        public AuraPose Origin { get; }
        public float Damping { get; }
        public float GravityScale { get; }

        public AuraResult Validate()
        {
            if (Width < 2 || Height < 2)
                return AuraResult.InvalidDefinition;
            if (!(Spacing > 0f) || !float.IsFinite(Spacing))
                return AuraResult.InvalidDefinition;
            if (float.IsNaN(Damping) || Damping < 0f || Damping > 1f)
                return AuraResult.InvalidDefinition;
            if (!float.IsFinite(GravityScale) || GravityScale < 0f)
                return AuraResult.InvalidDefinition;
            return AuraResult.Success;
        }
    }
}
