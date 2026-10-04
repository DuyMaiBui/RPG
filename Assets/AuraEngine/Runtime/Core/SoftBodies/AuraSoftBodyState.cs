namespace AuraEngine.Core
{
    public readonly struct AuraSoftBodyState
    {
        public AuraSoftBodyState(AuraSoftBodyId softBody, AuraVector3[] vertices)
        {
            SoftBody = softBody;
            Vertices = vertices;
        }
        public AuraSoftBodyId SoftBody { get; }
        public AuraVector3[] Vertices { get; }
    }
}
