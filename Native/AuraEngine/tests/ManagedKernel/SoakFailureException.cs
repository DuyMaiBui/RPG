using System;

namespace AuraEngine.KernelTests
{
    /* A soak invariant violation: non-finite state, handle confusion or an out-of-bounds buffer write. */
    internal sealed class SoakFailureException : Exception
    {
        public SoakFailureException(string message) : base(message)
        {
        }
    }
}
