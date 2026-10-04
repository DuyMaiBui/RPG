// AI-agent/editor tool: run via `unity command run_script` during Play Mode. Not part of any runtime assembly.
using System.Linq;
using UnityEngine;

public static class AuraSceneProbe
{
    // Summarises the transforms under /AuraSimulation: count, NaN count, lowest y and a motion signature.
    public static string Sample()
    {
        var root = GameObject.Find("AuraSimulation");
        if (root == null)
            return "NOROOT";

        var transforms = root.GetComponentsInChildren<Transform>().Where(t => t != root.transform).ToArray();
        float signature = 0f, minY = float.MaxValue, maxExtent = 0f;
        var nan = 0;
        foreach (var t in transforms)
        {
            var p = t.position;
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.y))
            {
                nan++;
                continue;
            }

            signature += p.x * 1.3f + p.y * 1.7f + p.z * 2.1f;
            minY = Mathf.Min(minY, p.y);
            maxExtent = Mathf.Max(maxExtent, Mathf.Abs(p.x) + Mathf.Abs(p.y) + Mathf.Abs(p.z));
        }

        return $"n={transforms.Length};nan={nan};minY={minY:F2};extent={maxExtent:F1};sig={signature:F3}";
    }
}
