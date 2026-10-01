using System;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal readonly struct ManagedAabb
    {
        public ManagedAabb(AuraVector3 min, AuraVector3 max)
        {
            Min = min;
            Max = max;
        }

        public AuraVector3 Min { get; }
        public AuraVector3 Max { get; }

        public bool Overlaps(in ManagedAabb other) =>
            Min.X <= other.Max.X && Max.X >= other.Min.X &&
            Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
            Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

        public static ManagedAabb FromShape(in ManagedShapeView view)
        {
            switch (view.Kind)
            {
                case ManagedShapeKind.Sphere:
                    return FromCenterExtents(view.Center, new AuraVector3(view.Radius, view.Radius, view.Radius));
                case ManagedShapeKind.Capsule:
                    var radius = new AuraVector3(view.Radius, view.Radius, view.Radius);
                    var min = new AuraVector3(MathF.Min(view.A.X, view.B.X), MathF.Min(view.A.Y, view.B.Y), MathF.Min(view.A.Z, view.B.Z)) - radius;
                    var max = new AuraVector3(MathF.Max(view.A.X, view.B.X), MathF.Max(view.A.Y, view.B.Y), MathF.Max(view.A.Z, view.B.Z)) + radius;
                    return new ManagedAabb(min, max);
                case ManagedShapeKind.Box:
                    return FromCenterExtents(view.Center, ManagedShapeView.RotatedExtents(view.Rotation, view.Extents));
                default:
                    return new ManagedAabb(view.Center, view.Center);
            }
        }

        private static ManagedAabb FromCenterExtents(AuraVector3 center, AuraVector3 extents) =>
            new ManagedAabb(center - extents, center + extents);
    }
}
