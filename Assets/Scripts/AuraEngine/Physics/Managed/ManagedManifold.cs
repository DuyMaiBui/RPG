using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal sealed class ManagedManifold
    {
        public int BodyA;
        public int BodyB;
        public AuraVector3 Normal;
        public bool IsTrigger;
        public float Friction;
        public float Restitution;
        public int Count;
        public readonly ManagedContactPoint[] Points = new ManagedContactPoint[8];

        public void Add(in AuraVector3 position, float penetration, int featureId)
        {
            if (Count >= Points.Length)
                return;

            Points[Count] = new ManagedContactPoint { Position = position, Penetration = penetration, FeatureId = featureId };
            Count++;
        }

        public void Reset()
        {
            Count = 0;
            Normal = AuraVector3.UnitX;
            IsTrigger = false;
            Friction = 0f;
            Restitution = 0f;
        }
    }
}
