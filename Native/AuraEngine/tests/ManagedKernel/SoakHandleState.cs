namespace AuraEngine.KernelTests
{
    /* What the soak model knows about a handle it has seen. Unknown means the kernel may have invalidated it
       implicitly (for example a joint whose body was destroyed), so no expectation is asserted. */
    internal enum SoakHandleState
    {
        Live = 0,
        Dead = 1,
        Unknown = 2
    }
}
