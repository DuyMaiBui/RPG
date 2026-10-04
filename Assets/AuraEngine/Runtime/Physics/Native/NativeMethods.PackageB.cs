using System;
using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    /* Package B: Box2D mouse-joint target and the generic shape overlap / shape cast entry points. */
    internal static partial class NativeMethods
    {
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_OverlapShape(NativeWorldHandle world, ref NativeShapeDesc shape, ref NativePose pose, ref NativeQueryFilter filter, IntPtr buffer, uint capacity, out uint outCount);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_ShapeCast(NativeWorldHandle world, ref NativeShapeDesc shape, ref NativePose pose, NativeVector3 direction, float maxDistance, ref NativeQueryFilter filter, out NativeQueryHit outHit, out byte outHasHit);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetJointTarget(NativeWorldHandle world, ulong joint, NativeVector3 target);
    }
}
