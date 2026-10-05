using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    internal static partial class NativeMethods
    {
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint Aura_LiveWorldCount();
    }
}
