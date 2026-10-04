using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRay
    {
        public NativeVector3 Origin;
        public NativeVector3 Direction;

        public static NativeRay From(in AuraRay ray) =>
            new NativeRay { Origin = NativeVector3.From(ray.Origin), Direction = NativeVector3.From(ray.Direction) };
    }
}
