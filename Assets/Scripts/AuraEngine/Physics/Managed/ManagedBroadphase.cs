using System;
using System.Collections.Generic;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal sealed class ManagedBroadphase
    {
        private readonly List<int> _order = new List<int>();
        private readonly List<ManagedAabb> _aabbs = new List<ManagedAabb>();

        public void ComputeAabbs(List<ManagedBody> bodies, AuraPhysicsMode mode)
        {
            if (_aabbs.Count < bodies.Count)
                _aabbs.AddRange(new ManagedAabb[bodies.Count - _aabbs.Count]);

            for (var index = 0; index < bodies.Count; index++)
            {
                var body = bodies[index];
                if (!body.Occupied)
                {
                    _aabbs[index] = new ManagedAabb(new AuraVector3(float.MaxValue, float.MaxValue, float.MaxValue), new AuraVector3(float.MinValue, float.MinValue, float.MinValue));
                    continue;
                }

                var min = new AuraVector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new AuraVector3(float.MinValue, float.MinValue, float.MinValue);
                for (var shapeIndex = 0; shapeIndex < body.Shapes.Length; shapeIndex++)
                {
                    var view = ManagedShapeView.FromBodyShape(body, body.Shapes[shapeIndex]);
                    var aabb = ManagedAabb.FromShape(view);
                    min = new AuraVector3(MathF.Min(min.X, aabb.Min.X), MathF.Min(min.Y, aabb.Min.Y), MathF.Min(min.Z, aabb.Min.Z));
                    max = new AuraVector3(MathF.Max(max.X, aabb.Max.X), MathF.Max(max.Y, aabb.Max.Y), MathF.Max(max.Z, aabb.Max.Z));
                }

                body.Aabb = new ManagedAabb(min, max);
                _aabbs[index] = body.Aabb;
            }
        }

        public List<(int A, int B)> ComputePairs(List<ManagedBody> bodies)
        {
            _order.Clear();
            for (var index = 0; index < bodies.Count; index++)
                if (bodies[index].Occupied)
                    _order.Add(index);

            _order.Sort((left, right) =>
            {
                var compare = _aabbs[left].Min.X.CompareTo(_aabbs[right].Min.X);
                return compare != 0 ? compare : left.CompareTo(right);
            });

            var pairs = new List<(int A, int B)>();
            for (var i = 0; i < _order.Count; i++)
            {
                var aIndex = _order[i];
                var aabb = _aabbs[aIndex];
                for (var j = i + 1; j < _order.Count; j++)
                {
                    var bIndex = _order[j];
                    var bAabb = _aabbs[bIndex];
                    if (bAabb.Min.X > aabb.Max.X)
                        break;

                    if (aabb.Overlaps(bAabb))
                        pairs.Add((aIndex, bIndex));
                }
            }

            return pairs;
        }
    }
}
