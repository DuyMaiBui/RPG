using System;
using System.Runtime.InteropServices;

namespace AuraEngine.KernelTests
{
    /* Output buffer with canary elements on both sides; Verify reports any write past the stated capacity. */
    internal sealed unsafe class SoakGuardBuffer<T> : IDisposable where T : unmanaged
    {
        private const int GuardElements = 4;
        private const byte Canary = 0xA5;
        private readonly IntPtr _block;
        private readonly int _totalBytes;
        private readonly int _guardBytes;
        private readonly int _capacityBytes;
        private readonly int _slackBytes;

        /* slack: extra writable elements after capacity that absorb a known kernel overrun without corrupting the heap. */
        public SoakGuardBuffer(int capacity, int slack = 0)
        {
            Capacity = capacity;
            _slackBytes = slack * sizeof(T);
            _guardBytes = GuardElements * sizeof(T);
            _capacityBytes = capacity * sizeof(T) + _slackBytes;
            _totalBytes = _capacityBytes + 2 * _guardBytes;
            _block = Marshal.AllocHGlobal(_totalBytes);
            new Span<byte>((void*)_block, _totalBytes).Fill(Canary);
        }

        public int Capacity { get; }

        public IntPtr Pointer => _block + _guardBytes;

        public T this[int index] => ((T*)Pointer)[index];

        /* True when the kernel wrote into the slack area, that is past the stated capacity. */
        public bool WroteIntoSlack()
        {
            var bytes = (byte*)_block + _guardBytes + (_capacityBytes - _slackBytes);
            for (var i = 0; i < _slackBytes; i++)
                if (bytes[i] != Canary)
                    return true;
            return false;
        }

        public void Verify(string operation)
        {
            var bytes = (byte*)_block;
            for (var i = 0; i < _guardBytes; i++)
            {
                if (bytes[i] != Canary)
                    throw new SoakFailureException(operation + ": write before the buffer start (capacity " + Capacity + ")");
                if (bytes[_guardBytes + _capacityBytes + i] != Canary)
                    throw new SoakFailureException(operation + ": write past the buffer end (capacity " + Capacity + ")");
            }
        }

        void IDisposable.Dispose() => Marshal.FreeHGlobal(_block);
    }
}
