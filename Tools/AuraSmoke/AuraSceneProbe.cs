// AI-agent/editor tool: run via `unity command run_script` during Play Mode. Not part of any runtime assembly.
// Returns one JSON object of named metrics: generic ones for every scene plus scene-specific ones.
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AuraSceneProbe
{
    static readonly StringBuilder Builder = new StringBuilder();

    public static string Sample()
    {
        var root = GameObject.Find("AuraSimulation");
        if (root == null)
            return "{\"error\":\"NOROOT\"}";

        Builder.Clear();
        Builder.Append("{\"scene\":\"").Append(SceneManager.GetActiveScene().name).Append("\",\"m\":{");
        var first = true;
        void M(string name, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 1e30f;
            if (!first) Builder.Append(',');
            first = false;
            Builder.Append('"').Append(name).Append("\":").Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        var transforms = root.GetComponentsInChildren<Transform>().Where(t => t != root.transform).ToArray();
        float signature = 0f, minY = float.MaxValue, extent = 0f;
        var nan = 0;
        foreach (var t in transforms)
        {
            var p = t.position;
            if (float.IsNaN(p.x + p.y + p.z) || float.IsInfinity(p.x + p.y + p.z)) { nan++; continue; }
            signature += p.x * 1.3f + p.y * 1.7f + p.z * 2.1f;
            minY = Mathf.Min(minY, p.y);
            extent = Mathf.Max(extent, Mathf.Abs(p.x) + Mathf.Abs(p.y) + Mathf.Abs(p.z));
        }

        M("dt", Time.unscaledDeltaTime);
        M("n", transforms.Length); M("nan", nan); M("minY", minY); M("extent", extent); M("sig", signature);

        Transform T(string n) { var g = GameObject.Find("AuraSimulation/" + n); return g == null ? null : g.transform; }
        void Pos(string n, string key, bool x = false, bool y = false, bool z = false)
        {
            var t = T(n);
            if (t == null) { M(key + "Missing", 1f); return; }
            if (x) M(key + "X", t.position.x);
            if (y) M(key + "Y", t.position.y);
            if (z) M(key + "Z", t.position.z);
        }
        void Angle(string n, string key, int axis)
        {
            var t = T(n);
            if (t == null) { M(key + "Missing", 1f); return; }
            var e = t.eulerAngles;
            M(key, axis == 0 ? e.x : axis == 1 ? e.y : e.z);
        }

        switch (SceneManager.GetActiveScene().name)
        {
            case "AuraDemo2D": Pos("Platform", "platform", x: true); break;
            case "AuraDemo3D": Pos("Platform", "platform", x: true); break;
            case "AuraDemoCore2D": Pos("IcePlatform", "platform", x: true); break;
            case "AuraDemoArticulation3D": Pos("Car", "car", x: true, z: true); break;
            case "AuraDemoCharacter3D":
                Pos("Walker_Stairs", "stairs", y: true); Pos("Walker_RampGentle", "gentle", y: true); Pos("Walker_RampSteep", "steep", y: true);
                Pos("Walker_Jumper", "jumper", y: true); Pos("Walker_Platform", "platform", y: true); break;
            case "AuraDemoPlatformer2D": Pos("Hero", "hero", x: true, y: true); Pos("Crate", "crate", x: true); break;
            case "AuraDemoSandbox2D": Pos("Chassis", "chassis", x: true); Pos("Crate", "crate", y: true); Pos("Plank3", "plank3", y: true); break;
            case "AuraDemoConstraints3D":
                Angle("GearA", "gearA", 2); Angle("GearB", "gearB", 2); Pos("Rack", "rack", x: true); Pos("WeightHeavy", "heavy", y: true);
                Pos("WeightLight", "light", y: true); Angle("Door", "door", 1); break;
            case "AuraDemoSpace3D":
            {
                var planet = T("Planet");
                for (var i = 0; i < 6; i++)
                {
                    var o = T("Orbiter" + i);
                    M("orbit" + i, planet != null && o != null ? Vector3.Distance(planet.position, o.position) : 1e30f);
                }
                var inst = Object.FindAnyObjectByType<AuraEngine.Unity.AuraSimulationInstance>();
                M("worldTimeScale", inst != null ? inst.World.TimeScale : 1e30f);
                break;
            }
            case "AuraDemoRagdollHit3D": Pos("Pelvis", "pelvis", y: true, z: true); break;
            case "AuraDemoWater2D":
            case "AuraDemoAdvancedWater3D":
                Pos("Cork", "cork", y: true); Pos("Wood", "wood", y: true); Pos("Neutral", "neutral", y: true);
                Pos("Stone", "stone", y: true); Pos("Ball", "ball", y: true); break;
            case "AuraDemoCloth2D":
            case "AuraDemoCloth3D":
            {
                var cloth = GameObject.Find("AuraSimulation/Cloth");
                var r = cloth != null ? cloth.GetComponent<MeshRenderer>() : null;
                M("clothCenterX", r != null ? r.bounds.center.x : 1e30f);
                M("clothSizeX", r != null ? r.bounds.size.x : 1e30f);
                M("clothSizeY", r != null ? r.bounds.size.y : 1e30f);
                break;
            }
            case "AuraDemoHair2D":
            case "AuraDemoHair3D":
            {
                var lr = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include).FirstOrDefault();
                M("hairTipX", lr != null && lr.positionCount > 0 ? lr.GetPosition(lr.positionCount - 1).x : 1e30f);
                break;
            }
        }

        Builder.Append("}}");
        return Builder.ToString();
    }
}
