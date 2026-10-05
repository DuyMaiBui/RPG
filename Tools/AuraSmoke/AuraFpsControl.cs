// AI-agent/editor tool: run via `unity command run_script`. Not part of any runtime assembly.
using UnityEngine;

public static class AuraFpsControl
{
    // Forces a low frame rate so each frame runs several fixed simulation steps. Returns what was applied.
    public static string Set8() => Apply(8);

    public static string Reset() => Apply(-1);

    static string Apply(int fps)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;
        return $"targetFrameRate={Application.targetFrameRate} vSync={QualitySettings.vSyncCount}";
    }
}
