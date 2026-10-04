using System;
using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeSoftBodyState
    {
        public NativeSoftBodyHandle SoftBody;
        public uint VertexCount;
        public IntPtr VertexPositions;
    }
}
