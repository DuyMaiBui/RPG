namespace AuraEngine.Physics.Native
{
    /* Process-wide diagnostics of the native physics library, for leak checks in tests and tools. */
    public static class NativePhysicsDiagnostics
    {
        /* Number of native worlds created and not yet destroyed; 0 when the library is not loadable. */
        public static uint LiveWorldCount() => NativePhysicsBackend.IsAvailable() ? NativeMethods.Aura_LiveWorldCount() : 0u;
    }
}
