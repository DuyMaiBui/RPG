namespace AuraEngine.Core
{
    public readonly struct AuraSoftBodyDefinition
    {
        public AuraSoftBodyDefinition(AuraPose initialPose, AuraPhysicsLayer objectLayer, AuraVector3[] vertices, uint[] faces, float[] inverseMass = null)
        {
            InitialPose = initialPose;
            ObjectLayer = objectLayer;
            Vertices = vertices;
            Faces = faces;
            InverseMass = inverseMass;
        }
        public AuraPose InitialPose { get; }
        public AuraPhysicsLayer ObjectLayer { get; }
        public AuraVector3[] Vertices { get; }
        public uint[] Faces { get; }
        public float[] InverseMass { get; }
    }
}
