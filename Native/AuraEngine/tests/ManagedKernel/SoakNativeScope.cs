using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AuraEngine.KernelTests
{
    /* Unmanaged copies of the arrays a descriptor points at, released when the call is done. */
    internal sealed unsafe class SoakNativeScope : IDisposable
    {
        private readonly List<IntPtr> _blocks = new List<IntPtr>();

        public IntPtr Copy<T>(T[] values) where T : unmanaged
        {
            var bytes = Math.Max(1, values.Length * sizeof(T));
            var block = Marshal.AllocHGlobal(bytes);
            _blocks.Add(block);
            fixed (T* source = values)
                Buffer.MemoryCopy(source, (void*)block, bytes, values.Length * sizeof(T));
            return block;
        }

        void IDisposable.Dispose()
        {
            foreach (var block in _blocks)
                Marshal.FreeHGlobal(block);
            _blocks.Clear();
        }
    }
}
