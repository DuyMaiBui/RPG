using System;
using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeContact
    {
        public NativeBodyHandle BodyA;
        public NativeBodyHandle BodyB;
        public NativeVector3 Point;
        public NativeVector3 Normal;
        public float Penetration;
        public float Impulse;
    }
}
