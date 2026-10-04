using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    /* Package E: world gravity, runtime collision detection and force fields. */
    internal static partial class NativeMethods
    {
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetWorldGravity(NativeWorldHandle world, NativeVector3 gravity);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_GetWorldGravity(NativeWorldHandle world, out NativeVector3 outGravity);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetBodyCollisionDetection(NativeWorldHandle world, NativeBodyHandle body, int collisionDetection);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_CreateForceField(NativeWorldHandle world, ref NativeForceFieldDesc desc, out NativeForceFieldHandle outField);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_UpdateForceField(NativeWorldHandle world, NativeForceFieldHandle field, ref NativeForceFieldDesc desc);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_DestroyForceField(NativeWorldHandle world, NativeForceFieldHandle field);
    }
}
