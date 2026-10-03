using System;
using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeShapeDesc
    {
        public int Type;
        public NativePose LocalPose;
        public byte IsTrigger;
        public byte Pad0;
        public byte Pad1;
        public byte Pad2;
        public float Friction;
        public float Restitution;
        public float Density;
        public uint Layer;
        public NativeVector3 HalfExtents;
        public float Radius;
        public float Height;
        public int MeshAsset;
        public NativeVector3 PlaneNormal;
        public float TopRadius;
        public IntPtr Vertices;
        public uint VertexCount;
        public IntPtr Indices;
        public uint IndexCount;
        public IntPtr MaterialIndices;
        public uint MaterialIndexCount;
        public uint ShapeFilterGroup;
        public uint ShapeFilterMask;
        public int ActiveEdgeMode;
        public float ActiveEdgeCosThresholdAngle;

        public static NativeShapeDesc From(in AuraPhysicsShapeDefinition shape)
        {
            var vertices = shape.Geometry.MeshVertices;
            var indices = shape.Geometry.MeshIndices;
            var heights = shape.Geometry.HeightSamples;
            var heightField = shape.Type == AuraShapeType.HeightField;

            return new NativeShapeDesc
            {
                Type = (int)shape.Type,
                LocalPose = NativePose.From(shape.LocalPose),
                IsTrigger = shape.IsTrigger ? (byte)1 : (byte)0,
                Friction = shape.Material.Friction,
                Restitution = shape.Material.Restitution,
                Density = shape.Material.Density,
                Layer = (uint)shape.Layer.Value,
                HalfExtents = NativeVector3.From(heightField ? shape.Geometry.HeightScale : shape.Geometry.HalfExtents),
                Radius = shape.Geometry.Radius,
                Height = shape.Geometry.Height,
                MeshAsset = shape.Geometry.MeshAssetId,
                VertexCount = heightField
                    ? (heights != null ? (uint)heights.Length : 0u)
                    : (vertices != null ? (uint)vertices.Length : 0u),
                IndexCount = indices != null ? (uint)indices.Length : 0u,
                MaterialIndexCount = shape.Geometry.MaterialIndices != null ? (uint)shape.Geometry.MaterialIndices.Length : 0u,
                ShapeFilterGroup = 0,
                ShapeFilterMask = uint.MaxValue,
                ActiveEdgeMode = 0,
                ActiveEdgeCosThresholdAngle = 0f,
                PlaneNormal = NativeVector3.From(shape.Geometry.PlaneNormal),
                TopRadius = shape.Geometry.TopRadius,
            };
        }
    }
}
