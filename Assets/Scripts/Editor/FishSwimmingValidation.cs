using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FishSwimmingValidation
{
    [MenuItem("Tools/Hotzenplotz/Fisch-Schwimmen pruefen")]
    public static void Validate()
    {
        var fish = UnityEngine.Object.FindObjectsByType<AIMove>(FindObjectsSortMode.None).First(m => m.name == "Fish");
        var water = UnityEngine.Object.FindObjectsByType<HP3_WaterBody>(FindObjectsSortMode.None).First(w => w.PondRenderer != null);
        var report = new StringBuilder();
        Vector3 position = fish.transform.position;
        Quaternion rotation = fish.visual.rotation;
        var random = UnityEngine.Random.state;
        try
        {
            float traveled = 0;
            float minSurfaceClearance = float.PositiveInfinity;
            float minGroundClearance = float.PositiveInfinity;
            var visited = new System.Collections.Generic.HashSet<Transform>();
            for (int i = 0; i < 7200; i++)
            {
                var previous = fish.transform.position;
                fish.Step(1f / 60f);
                traveled += Vector3.Distance(previous, fish.transform.position);
                Vector3 p = fish.transform.position;
                minSurfaceClearance = Mathf.Min(minSurfaceClearance, water.GetSurfaceY(p, i / 60f) - p.y);
                foreach (var terrain in Terrain.activeTerrains)
                {
                    Vector3 local = p - terrain.transform.position;
                    var size = terrain.terrainData.size;
                    if (local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z)
                        minGroundClearance = Mathf.Min(minGroundClearance, p.y - (terrain.SampleHeight(p) + terrain.transform.position.y));
                }
                foreach (var waypoint in fish.manager.Waypoints)
                    if (Vector3.Distance(p, waypoint.position) < 0.06f) visited.Add(waypoint);
            }
            report.AppendLine("120 s simulation: traveled=" + traveled + " m; visited=" + visited.Count + "/6");
            report.AppendLine("Minimum center clearance: surface=" + minSurfaceClearance + " m; terrain=" + minGroundClearance + " m");
            var animator = fish.GetComponentInChildren<Animator>();
            report.AppendLine("Animator=" + animator.runtimeAnimatorController.name + "; rootMotion=" + animator.applyRootMotion);
            var clip = animator.runtimeAnimatorController.animationClips.First();
            report.AppendLine("Animation=" + clip.name + "; length=" + clip.length + "; loop=" + clip.isLooping);
            report.AppendLine("Curve bindings=" + AnimationUtility.GetCurveBindings(clip).Length);
            foreach (var renderer in fish.GetComponentsInChildren<Renderer>())
                report.AppendLine("Renderer=" + renderer.name + " bounds=" + renderer.bounds + "; materials=" + string.Join(",", renderer.sharedMaterials.Select(m => m == null ? "MISSING" : m.name + ":" + m.shader.name)));
            if (traveled < 10 || visited.Count < 2 || minSurfaceClearance <= 0 || minGroundClearance < 0.15f)
                throw new InvalidOperationException("Route validation failed.\n" + report);
            report.AppendLine("PASS");
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); }
        finally
        {
            fish.transform.position = position;
            fish.visual.rotation = rotation;
            UnityEngine.Random.state = random;
            File.WriteAllText("Temp/FishValidation.result.txt", report.ToString());
            Selection.activeGameObject = fish.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(position, Quaternion.Euler(30, 25, 0), 2.5f);
        }
    }
}
