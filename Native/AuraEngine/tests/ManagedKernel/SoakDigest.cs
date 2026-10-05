using System;

namespace AuraEngine.KernelTests
{
    /* FNV-1a over everything observable, used to compare two replays of the same episode. */
    internal static class SoakDigest
    {
        public const ulong Seed = 14695981039346656037UL;

        public static ulong Mix(ulong digest, ulong value)
        {
            for (var i = 0; i < 8; i++)
            {
                digest ^= (value >> (i * 8)) & 0xFF;
                digest *= 1099511628211UL;
            }

            return digest;
        }

        public static ulong MixFloat(ulong digest, float value) => Mix(digest, (ulong)(uint)BitConverter.SingleToInt32Bits(value));
    }
}
