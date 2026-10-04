using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeBodyHandle
    {
        public uint Index;
        public uint Generation;

        public PhysicsBodyId ToManaged() => new PhysicsBodyId((int)Index, (int)Generation);

        public static NativeBodyHandle From(PhysicsBodyId id) =>
            new NativeBodyHandle { Index = (uint)id.Index, Generation = (uint)id.Generation };
    }
}
