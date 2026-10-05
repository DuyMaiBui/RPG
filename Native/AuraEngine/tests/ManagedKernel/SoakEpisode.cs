using System;
using System.Collections.Generic;
using System.Globalization;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    /* One seeded sequence of random operations on freshly created worlds. Replaying the same episode seed must
       reproduce the same digest of result codes, handles and world state. */
    internal sealed class SoakEpisode : IDisposable
    {
        private readonly uint _baselineWorlds;
        private int _worldSerial;

        public SoakEpisode(ulong seed, int index, string logPath)
        {
            Seed = seed;
            Index = index;
            Rng = new SoakRng(seed);
            Log = new SoakOpLog(logPath, "episode " + index + " seed " + seed);
            _baselineWorlds = NativeMethods.Aura_LiveWorldCount();
            SoakWatchdog.Attach(this);
        }

        public ulong Seed { get; }

        public int Index { get; }

        public SoakRng Rng { get; }

        public SoakOpLog Log { get; }

        public ulong Digest = SoakDigest.Seed;

        public int OpIndex;

        public uint Tick;

        public readonly List<SoakWorld> Worlds = new List<SoakWorld>();

        public readonly List<ulong> OpDigests = new List<ulong>();

        /* Audited states as text (SoakSettings.Trace), tagged with the operation index they followed. */
        public readonly List<string> Trace = new List<string>();

        public void Run(int ops)
        {
            if (Worlds.Count == 0)
                SoakWorldOps.Create(this);
            for (var i = 0; i < ops; i++)
            {
                if (Worlds.Count == 0)
                {
                    SoakWorldOps.Create(this);
                    if (Worlds.Count == 0)
                        continue;
                }

                var world = Worlds[Rng.Next(Worlds.Count)];
                Suppress = world.Tainted;
                var roll = Rng.Next(120);
                if (roll < 16) SoakWorldOps.Step(this, world);
                else if (roll < 28) SoakBodyOps.Create(this, world);
                else if (roll < 33) SoakBodyOps.Destroy(this, world);
                else if (roll < 53) SoakBodyOps.Control(this, world);
                else if (roll < 63) SoakJointOps.Create(this, world);
                else if (roll < 66) SoakJointOps.Destroy(this, world);
                else if (roll < 78) SoakJointOps.Control(this, world);
                else if (roll < 86) SoakCharacterOps.Run(this, world);
                else if (roll < 92) SoakFieldOps.Run(this, world);
                else if (roll < 95) SoakWorldOps.Gravity(this, world);
                else if (roll < 105) SoakQueryOps.Run(this, world);
                else if (roll < 108) SoakSnapshotOps.Run(this, world);
                else if (roll < 112) SoakWorldOps.Buffers(this, world);
                else if (roll < 113) SoakWorldOps.Create(this);
                else if (roll < 115) SoakWorldOps.Destroy(this, world);
                else SoakBodyOps.Create(this, world);

                if (OpIndex % SoakSettings.AuditEvery == 0 && Worlds.Contains(world))
                    SoakWorldOps.Audit(this, world);
                Suppress = false;
            }

            foreach (var world in Worlds)
            {
                Suppress = world.Tainted;
                SoakWorldOps.Audit(this, world);
            }

            Suppress = false;
        }

        /* Coverage counters over the whole process: result codes and operations by name. */
        public static readonly long[] CodeCounts = new long[32];

        public static readonly Dictionary<string, long> OpCounts = new Dictionary<string, long>();

        public void Begin(string text)
        {
            var space = text.IndexOf(' ');
            var name = space < 0 ? text : text.Substring(0, space);
            OpCounts.TryGetValue(name, out var seen);
            OpCounts[name] = seen + 1;
            OpIndex++;
            SoakWatchdog.Beat();
            Log.Add("#" + OpIndex.ToString(CultureInfo.InvariantCulture) + " " + text);
        }

        public int End(int code)
        {
            CodeCounts[(uint)code < 32u ? code : 31]++;
            Log.Append("-> " + code.ToString(CultureInfo.InvariantCulture));
            Mix((ulong)(uint)code);
            OpDigests.Add(Digest);
            return code;
        }

        /* True while operating on a world that a corrupted snapshot or an exploded state tainted: its results are not
           compared between replays (garbage in, garbage out), only crashes, hangs and buffer overruns still count. */
        public bool Suppress;

        public void Mix(ulong value)
        {
            if (!Suppress)
                Digest = SoakDigest.Mix(Digest, value);
        }

        public int NextWorldSerial() => ++_worldSerial;

        public void Fail(string message) => throw new SoakFailureException(message);

        /* Destroys every world and checks the kernel released them all. */
        public void Finish()
        {
            foreach (var world in Worlds.ToArray())
                SoakWorldOps.Destroy(this, world);
            var live = NativeMethods.Aura_LiveWorldCount();
            if (live != _baselineWorlds)
                Fail("Aura_LiveWorldCount is " + live + " after destroying every world, expected " + _baselineWorlds);
        }

        void IDisposable.Dispose() => ((IDisposable)Log).Dispose();
    }
}
