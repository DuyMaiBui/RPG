using System;
using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeSoftBodyDesc
    {
        public NativePose InitialPose;
        public uint ObjectLayer;
        public uint VertexCount;
        public IntPtr VertexPositions;
        public uint FaceCount;
        public IntPtr FaceIndices;
        public IntPtr InverseMass;

        public static NativeSoftBodyDesc From(in AuraSoftBodyDefinition definition) => new NativeSoftBodyDesc
        {
            InitialPose = NativePose.From(definition.InitialPose),
            ObjectLayer = (uint)definition.ObjectLayer.Value,
            VertexCount = (uint)definition.Vertices.Length,
            FaceCount = (uint)(definition.Faces.Length / 3),
        };
    }
}
