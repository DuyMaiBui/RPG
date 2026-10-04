using System.Runtime.InteropServices;
using AuraEngine.Core;
namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct NativeWaterDesc
    {
        public float SurfaceHeight; public NativeVector3 SurfaceNormal; public float Density; public float LinearDrag;
        public static NativeWaterDesc From(in AuraWaterDefinition definition) => new NativeWaterDesc
        { SurfaceHeight = definition.SurfaceHeight, SurfaceNormal = NativeVector3.From(definition.SurfaceNormal), Density = definition.Density, LinearDrag = definition.LinearDrag };
    }
}
