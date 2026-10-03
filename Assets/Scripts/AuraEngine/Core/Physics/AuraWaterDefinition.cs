namespace AuraEngine.Core
{
    public readonly struct AuraWaterDefinition
    {
        public AuraWaterDefinition(float surfaceHeight, AuraVector3 surfaceNormal, float density = 1000f, float linearDrag = 0f)
        { SurfaceHeight = surfaceHeight; SurfaceNormal = surfaceNormal; Density = density; LinearDrag = linearDrag; }
        public float SurfaceHeight { get; }
        public AuraVector3 SurfaceNormal { get; }
        public float Density { get; }
        public float LinearDrag { get; }
    }
}
