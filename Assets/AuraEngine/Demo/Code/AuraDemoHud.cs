using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    public sealed class AuraDemoHud : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [SerializeField]
        private AuraDemoRayProbe _rayProbe;

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            var area = new Rect(12f, 12f, 640f, 160f);

            if (_instance == null || !_instance.IsCreated)
            {
                GUI.Label(area, "AuraEngine: simulation not created.", style);
                return;
            }

            var world = _instance.World;
            GUI.Label(area, $"AuraEngine demo", style);
            GUI.Label(new Rect(12f, 36f, 640f, 24f), $"Tick: {world.CurrentTick.Value}", style);
            GUI.Label(new Rect(12f, 60f, 640f, 24f), $"Entities: {world.EntityCount}   Bodies: {world.BodyCount}", style);
            GUI.Label(new Rect(12f, 84f, 640f, 24f), $"State hash: {world.ComputeStateHash():X16}", style);
            GUI.Label(new Rect(12f, 108f, 640f, 24f), $"Backend: {_instance.BackendName}   Pending events: {world.PendingEventCount}", style);

            if (_rayProbe != null)
            {
                var text = _rayProbe.HasHit
                    ? $"Raycast hit: {_rayProbe.HitEntity} @ {_rayProbe.HitDistance:F2}m"
                    : "Raycast hit: none";
                GUI.Label(new Rect(12f, 132f, 640f, 24f), text, style);
            }
        }
    }
}
