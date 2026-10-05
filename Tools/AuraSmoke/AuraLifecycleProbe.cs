// AI-agent/editor tool: run via `unity command run_script`. Not part of any runtime assembly.
public static class AuraLifecycleProbe
{
    // Native worlds created and not yet destroyed in this process.
    public static string Live() => AuraEngine.Physics.Native.NativePhysicsDiagnostics.LiveWorldCount().ToString();
}
