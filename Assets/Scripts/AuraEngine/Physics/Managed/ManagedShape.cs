using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal sealed class ManagedShape
    {
        public AuraShapeType Type;
        public AuraPose LocalPose;
        public bool IsTrigger;
        public float Friction;
        public float Restitution;
        public AuraVector3 HalfExtents;
        public float Radius;
        public float Height;

        /* Triangle mesh (TriangleMesh) — world-space, already scaled. */
        public AuraVector3[] MeshVertices;
        public int[] MeshIndices;

        /* Height field (HeightField): resolution x resolution samples where a
           sample at (i,j) is at local (i * Scale.X, Sample * Scale.Y, j * Scale.Z). */
        public float[] HeightSamples;
        public int HeightResolution;
        public AuraVector3 HeightScale;
    }
}
