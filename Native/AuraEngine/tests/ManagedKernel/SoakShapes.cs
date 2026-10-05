using System;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    /* Random shape descriptors of every AuraShapeType, with mesh data copied into the caller's scope. */
    internal static class SoakShapes
    {
        public static NativeShapeDesc Make(SoakRng rng, SoakNativeScope scope, bool is2D)
        {
            int type;
            if (SoakSettings.Bad(rng, 0.03f))
                type = rng.Range(-1, 12);
            else
            {
                var roll = rng.Next(100);
                type = roll < 22 ? 0 : roll < 40 ? 1 : roll < 52 ? 2 : roll < 60 ? 3 : roll < 70 ? 4
                    : roll < 78 ? 5 : roll < 84 ? 6 : roll < 90 ? 7 : roll < 95 ? 8 : 9;
            }

            var desc = new NativeShapeDesc
            {
                Type = type,
                LocalPose = rng.Chance(0.7f) ? IdentityPose() : SoakValues.Pose(rng, 0.5f, is2D),
                IsTrigger = (byte)(rng.Chance(0.1f) ? 1 : 0),
                IsOneWay = (byte)(rng.Chance(0.1f) ? 1 : 0),
                Friction = SoakValues.Float(rng, 0f, 1.2f),
                Restitution = SoakValues.Float(rng, 0f, 1f),
                Density = SoakValues.Float(rng, 0.1f, 4f),
                Layer = (uint)(!SoakSettings.Bad(rng, 0.03f) ? rng.Range(0, 7) : rng.Range(32, 100)),
                HalfExtents = SoakValues.PositiveVec(rng, 0.1f, 2f),
                Radius = SoakValues.Float(rng, 0.1f, 1.5f),
                Height = SoakValues.Float(rng, 0.2f, 3f),
                PlaneNormal = rng.Chance(0.8f) ? new NativeVector3 { X = 0f, Y = 1f, Z = 0f } : SoakValues.UnitVec(rng),
                ShapeFilterGroup = 0,
                ShapeFilterMask = uint.MaxValue,
                ActiveEdgeMode = rng.Range(0, 1),
                ActiveEdgeCosThresholdAngle = rng.Chance(0.3f) ? rng.Range(0f, 1f) : 0f,
            };
            desc.TopRadius = rng.Chance(0.9f) ? desc.Radius * rng.Range(0.1f, 1f) : SoakValues.Float(rng, 0f, 2f);

            if (type == 4)
            {
                var count = rng.Chance(0.1f) ? rng.Range(0, 4) : rng.Range(4, 14);
                var points = new float[count * 3];
                var degenerate = rng.Chance(0.1f);
                for (var i = 0; i < points.Length; i++)
                    points[i] = degenerate ? rng.Range(-0.001f, 0.001f) : rng.Range(-1.2f, 1.2f);
                desc.Vertices = scope.Copy(points);
                desc.VertexCount = (uint)count;
            }
            else if (type == 5)
            {
                var grid = rng.Range(2, 4);
                var vertices = new float[grid * grid * 3];
                for (var z = 0; z < grid; z++)
                {
                    for (var x = 0; x < grid; x++)
                    {
                        var k = (z * grid + x) * 3;
                        vertices[k] = (x - grid * 0.5f) * 1.5f;
                        vertices[k + 1] = rng.Range(0f, 0.6f);
                        vertices[k + 2] = (z - grid * 0.5f) * 1.5f;
                    }
                }

                var indices = new uint[(grid - 1) * (grid - 1) * 6];
                var w = 0;
                for (var z = 0; z < grid - 1; z++)
                {
                    for (var x = 0; x < grid - 1; x++)
                    {
                        var i0 = (uint)(z * grid + x);
                        var i1 = i0 + 1;
                        var i2 = i0 + (uint)grid;
                        var i3 = i2 + 1;
                        indices[w++] = i0; indices[w++] = i2; indices[w++] = i1;
                        indices[w++] = i1; indices[w++] = i2; indices[w++] = i3;
                    }
                }

                if (SoakSettings.Bad(rng, 0.05f))
                    indices[rng.Next(indices.Length)] = (uint)(grid * grid + rng.Range(0, 1000));
                desc.Vertices = scope.Copy(vertices);
                desc.VertexCount = (uint)(grid * grid);
                desc.Indices = scope.Copy(indices);
                desc.IndexCount = (uint)indices.Length;
                if (rng.Chance(0.3f))
                {
                    var materials = new uint[indices.Length / 3];
                    for (var i = 0; i < materials.Length; i++)
                        materials[i] = (uint)rng.Range(0, 3);
                    desc.MaterialIndices = scope.Copy(materials);
                    desc.MaterialIndexCount = (uint)materials.Length;
                }
            }
            else if (type == 9)
            {
                var n = rng.Pick(new[] { 2, 3, 4, 5, 8 });
                var count = rng.Chance(0.1f) ? n * n + rng.Range(1, 5) : n * n;
                var samples = new float[count];
                for (var i = 0; i < samples.Length; i++)
                    samples[i] = rng.Range(0f, 2f);
                desc.Vertices = scope.Copy(samples);
                desc.VertexCount = (uint)count;
                desc.HalfExtents = SoakValues.PositiveVec(rng, 0.1f, 1.5f);
                if (rng.Chance(0.3f))
                {
                    var materials = new uint[(n - 1) * (n - 1)];
                    for (var i = 0; i < materials.Length; i++)
                        materials[i] = (uint)rng.Range(0, 3);
                    desc.MaterialIndices = scope.Copy(materials);
                    desc.MaterialIndexCount = (uint)materials.Length;
                }
            }

            return desc;
        }

        public static NativePose IdentityPose() =>
            new NativePose { Rotation = new NativeQuaternion { W = 1f } };
    }
}
