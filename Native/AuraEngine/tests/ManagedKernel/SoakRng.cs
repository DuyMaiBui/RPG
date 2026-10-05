using System;

namespace AuraEngine.KernelTests
{
    /* Deterministic xoroshiro128+ generator seeded through splitmix64, so a soak run depends only on its seed. */
    internal sealed class SoakRng
    {
        private ulong _s0;
        private ulong _s1;

        public SoakRng(ulong seed)
        {
            _s0 = Mix(ref seed);
            _s1 = Mix(ref seed);
            if (_s0 == 0 && _s1 == 0)
                _s1 = 1;
        }

        public static ulong Mix(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextU64()
        {
            var s0 = _s0;
            var s1 = _s1;
            var result = s0 + s1;
            s1 ^= s0;
            _s0 = ((s0 << 24) | (s0 >> 40)) ^ s1 ^ (s1 << 16);
            _s1 = (s1 << 37) | (s1 >> 27);
            return result;
        }

        /* Uniform integer in [0, bound). */
        public int Next(int bound) => bound <= 1 ? 0 : (int)(NextU64() % (ulong)bound);

        /* Uniform integer in [lo, hi]. */
        public int Range(int lo, int hi) => lo + Next(hi - lo + 1);

        public float Unit() => (NextU64() >> 40) * (1f / 16777216f);

        public float Range(float lo, float hi) => lo + (hi - lo) * Unit();

        public bool Chance(float probability) => Unit() < probability;

        public T Pick<T>(T[] values) => values[Next(values.Length)];
    }
}
