using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal sealed class SoakJoint
    {
        public ulong Handle;
        public SoakHandleState State = SoakHandleState.Live;
        public int Type;
        public NativeBodyHandle BodyA;
        public NativeBodyHandle BodyB;
        public ulong RefA = ulong.MaxValue;
        public ulong RefB = ulong.MaxValue;

        public string History = string.Empty;

        public void Mark(SoakHandleState state, string why, int op)
        {
            State = state;
            History += " op" + op + ":" + state + "(" + why + ")";
        }
    }
}
