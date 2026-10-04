using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    /* Jolt 3D constraint set entry points (SixDof / SwingTwist per-axis control). */
    internal static partial class NativeMethods
    {
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_SetJointAxisLimits(NativeWorldHandle world, ulong joint, uint axis, ref NativeJointAxisLimit limit);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_SetJointAxisMotor(NativeWorldHandle world, ulong joint, uint axis, ref NativeJointMotorDesc motor);
    }
}
