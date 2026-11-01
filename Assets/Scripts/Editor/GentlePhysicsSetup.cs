using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GentlePhysicsSetup
{
    [InitializeOnLoadMethod]
    private static void Register() => EditorApplication.update += Check;

    private static void Check()
    {
        const string request = "Temp/GentlePhysicsAudit.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool apply = File.ReadAllText(request).Trim() == "apply";
        File.Delete(request);
        var report = new StringBuilder();
        int count = 0;
        foreach (Rigidbody body in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (body.name != "Pan" && body.name != "Bucket" && body.name != "Basket") continue;
            report.AppendLine($"{body.name} mass={body.mass} damping={body.linearDamping}/{body.angularDamping} depen={body.maxDepenetrationVelocity} CCD={body.collisionDetectionMode} scale={body.transform.lossyScale}");
            foreach (Collider c in body.GetComponentsInChildren<Collider>(true))
            {
                if (c.attachedRigidbody != body) continue;
                report.AppendLine($" {c.name}: {c.GetType().Name} enabled={c.enabled} trigger={c.isTrigger} material={(c.sharedMaterial == null ? "Default" : c.sharedMaterial.name)} worldCenter={c.bounds.center} worldSize={c.bounds.size}");
                if (c is MeshCollider m) report.AppendLine(" convex=" + m.convex);
            }
            // Probe the middle of the cavity from above, ignoring trigger zones.
            var solids = body.GetComponentsInChildren<Collider>(true);
            Bounds bounds = new Bounds(body.position, Vector3.zero);
            bool found = false;
            foreach (var c in solids)
                if (c.enabled && !c.isTrigger && c.attachedRigidbody == body)
                { if (!found) { bounds = c.bounds; found = true; } else bounds.Encapsulate(c.bounds); }
            if (found)
            {
                Ray ray = new Ray(new Vector3(bounds.center.x, bounds.max.y + 0.1f, bounds.center.z), Vector3.down);
                float nearest = float.PositiveInfinity;
                foreach (var c in solids)
                    if (c.enabled && !c.isTrigger && c.attachedRigidbody == body && c.Raycast(ray, out RaycastHit hit, bounds.size.y + 0.2f))
                        nearest = Mathf.Min(nearest, hit.distance);
                report.AppendLine(" cavity center depth from highest collider=" + (nearest - 0.1f));
            }
            if (apply)
            {
                var tuning = body.GetComponent<GentleItemPhysics>();
                if (tuning == null) tuning = Undo.AddComponent<GentleItemPhysics>(body.gameObject);
                Undo.RecordObject(body, "Sanfte Gefaessphysik");
                foreach (var col in solids) Undo.RecordObject(col, "Sanfte Kontakte");
                var settings = new SerializedObject(tuning);
                settings.FindProperty("vessel").boolValue = true;
                settings.ApplyModifiedProperties();
                tuning.ApplySettings();
                PrefabUtility.RecordPrefabInstancePropertyModifications(tuning);
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                foreach (var col in solids) PrefabUtility.RecordPrefabInstancePropertyModifications(col);
                EditorSceneManager.MarkSceneDirty(body.gameObject.scene);
                report.AppendLine($" APPLIED: damping={body.linearDamping}/{body.angularDamping} depen={body.maxDepenetrationVelocity} CCD={body.collisionDetectionMode}");
                count++;
            }
        }
        report.AppendLine("Applied vessels: " + count);
        if (apply)
        {
            try { report.AppendLine(GentlePhysicsChecks.Validate()); }
            catch (Exception error) { report.AppendLine("VALIDATION FAILED: " + error); }
        }
        File.WriteAllText("Temp/GentlePhysicsAudit.txt", report.ToString());
    }
}
