using System.Collections.Generic;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal sealed class SoakWorld
    {
        public int Serial;
        public bool Is2D;
        public NativeWorldHandle Handle;
        public int BodyCap = 60;

        /* Set after a deliberately corrupted snapshot was accepted: finite-state checks no longer apply. */
        public bool Tainted;

        public readonly List<SoakBody> Bodies = new List<SoakBody>();
        public readonly Dictionary<ulong, SoakBody> BodyByHandle = new Dictionary<ulong, SoakBody>();
        public readonly List<SoakJoint> Joints = new List<SoakJoint>();
        public readonly Dictionary<ulong, SoakJoint> JointByHandle = new Dictionary<ulong, SoakJoint>();
        public readonly List<SoakCharacter> Characters = new List<SoakCharacter>();
        public readonly Dictionary<ulong, SoakCharacter> CharacterByHandle = new Dictionary<ulong, SoakCharacter>();
        public readonly List<SoakField> Fields = new List<SoakField>();
        public readonly Dictionary<ulong, SoakField> FieldByHandle = new Dictionary<ulong, SoakField>();
        public byte[] Snapshot;

        /* Body states captured just before the last Step, printed when that Step produces a non-finite state. */
        public NativeBodyState[] PreStep = new NativeBodyState[0];

        public float PreStepDt;

        public static ulong Key(NativeBodyHandle handle) => ((ulong)handle.Index << 32) | handle.Generation;

        public override string ToString() => (Is2D ? "w2D#" : "w3D#") + Serial;
    }
}
