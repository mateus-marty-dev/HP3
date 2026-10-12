using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class ViseSetup
{
    [InitializeOnLoadMethod]
    static void Register() { EditorApplication.update += Check; }

    static void Check()
    {
        if (File.Exists("Temp/ViseTest.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete("Temp/ViseTest.request");
            try { Verify(); }
            catch (System.Exception e) { File.WriteAllText("Temp/ViseTest.result.txt", "FAILED: " + e); }
        }
        if (File.Exists("Temp/ViseSetup.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete("Temp/ViseSetup.request");
            try { Install(); }
            catch (System.Exception e) { File.WriteAllText("Temp/ViseSetup.result.txt", "FAILED: " + e); Debug.LogException(e); }
        }
        const string request = "Temp/ViseInspect.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(request);
        var output = new StringBuilder();
        output.AppendLine("Selection: " + (Selection.activeGameObject == null ? "none" : Selection.activeGameObject.name));
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Wooden_Shed" && !HasShedParent(t)) continue;
            string path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            output.Append(path + " pos=" + t.position.ToString("F4") + " rot=" + t.eulerAngles.ToString("F2"));
            var r = t.GetComponent<Renderer>();
            if (r != null) output.Append(" bounds=" + r.bounds.center.ToString("F4") + " size=" + r.bounds.size.ToString("F4"));
            var f = t.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) output.Append(" localCenter=" + f.sharedMesh.bounds.center.ToString("F4") + " localSize=" + f.sharedMesh.bounds.size.ToString("F4"));
            output.AppendLine();
            if (f != null && (t.name == "Block" || t.name == "Nail.007" || t.name == "Workbench"))
            {
                foreach (var vertex in f.sharedMesh.vertices)
                    output.AppendLine("V " + t.name + " " + t.TransformPoint(vertex).ToString("F5"));
            }
        }
        File.WriteAllText("Temp/ViseInspect.result.txt", output.ToString());
    }

    [MenuItem("Tools/Hotzenplotz/Schraubstock einrichten")]
    public static void Install()
    {
        var shed = GameObject.Find("Wooden_Shed");
        if (shed == null) throw new System.InvalidOperationException("Wooden_Shed fehlt in der offenen Szene.");
        if (shed.GetComponentInChildren<ViseController>() != null)
            throw new System.InvalidOperationException("Schraubstock ist bereits eingerichtet.");
        Transform block = shed.transform.Find("Block");
        Transform handle = shed.transform.Find("Nail.007");
        Transform bench = shed.transform.Find("Workbench");
        if (block == null || handle == null || bench == null)
            throw new System.InvalidOperationException("Block, Nail.007 oder Workbench fehlen. Keine Szene geändert.");

        Vector3 axis = bench.right.normalized;
        if (Vector3.Dot(axis, bench.GetComponent<Renderer>().bounds.center - block.position) < 0f) axis = -axis;
        Vector3 origin = block.position;
        float tableEdge = float.PositiveInfinity;
        foreach (var v in bench.GetComponent<MeshFilter>().sharedMesh.vertices)
            tableEdge = Mathf.Min(tableEdge, Vector3.Dot(bench.TransformPoint(v) - origin, axis));
        float jawEnd = float.NegativeInfinity;
        float spindleEnd = float.NegativeInfinity;
        foreach (var v in block.GetComponent<MeshFilter>().sharedMesh.vertices)
        {
            Vector3 relative = block.TransformPoint(v) - origin;
            float along = Vector3.Dot(relative, axis);
            spindleEnd = Mathf.Max(spindleEnd, along);
            // Broad wooden jaw points lie outside the narrow screw radius.
            if ((relative - axis * along).magnitude > 0.055f) jawEnd = Mathf.Max(jawEnd, along);
        }
        float closed = tableEdge - jawEnd - 0.001f;
        float opened = tableEdge - spindleEnd + 0.008f;
        if (!float.IsFinite(closed) || !float.IsFinite(opened) || closed <= opened)
            throw new System.InvalidOperationException("Anschläge konnten nicht sicher ermittelt werden. Keine Szene geändert.");

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Schraubstock einrichten");
        Undo.RegisterFullObjectHierarchyUndo(shed, "Schraubstock einrichten");
        if (PrefabUtility.IsPartOfPrefabInstance(shed))
            PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(shed), PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
        var root = new GameObject("Schraubstock");
        Undo.RegisterCreatedObjectUndo(root, "Schraubstock einrichten");
        root.transform.SetParent(shed.transform, false);
        root.transform.position = origin;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = new Vector3(1f / shed.transform.lossyScale.x, 1f / shed.transform.lossyScale.y, 1f / shed.transform.lossyScale.z);
        var moving = new GameObject("Bewegliche Backe");
        Undo.RegisterCreatedObjectUndo(moving, "Schraubstock einrichten");
        moving.transform.SetParent(root.transform, false);
        var pivot = new GameObject("Drehgriff");
        Undo.RegisterCreatedObjectUndo(pivot, "Schraubstock einrichten");
        pivot.transform.SetParent(moving.transform, false);
        Undo.SetTransformParent(block, moving.transform, "Backe zuweisen");
        Undo.SetTransformParent(handle, pivot.transform, "Griff zuweisen");
        foreach (var t in new[] {block, handle})
        {
            if (t.GetComponent<Collider>() == null)
            {
                var collider = Undo.AddComponent<BoxCollider>(t.gameObject);
                var bounds = t.GetComponent<MeshFilter>().sharedMesh.bounds;
                collider.center = bounds.center;
                collider.size = bounds.size;
            }
        }
        var controller = Undo.AddComponent<ViseController>(root);
        controller.movingAssembly = moving.transform;
        controller.rotatingHandle = pivot.transform;
        controller.localTravelAxis = axis;
        controller.closedOffset = closed;
        controller.openOffset = opened;
        controller.initialOffset = 0f;
        controller.interactionCamera = Camera.main;
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(shed.scene);
        Selection.activeGameObject = root;
        File.WriteAllText("Temp/ViseSetup.result.txt", "OK: scene remains unsaved; closed=" + closed + "; open=" + opened + "; travel=" + (closed-opened) + "; axis=" + axis.ToString("F4"));
    }

    static bool HasShedParent(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.name == "Wooden_Shed") return true;
        return false;
    }

    static void Verify()
    {
        var source = Object.FindFirstObjectByType<ViseController>();
        if (source == null) throw new System.InvalidOperationException("Schraubstock fehlt.");
        var test = new GameObject("Vise verification") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var moving = new GameObject("Moving"); moving.transform.SetParent(test.transform, false);
            var handle = new GameObject("Handle"); handle.transform.SetParent(moving.transform, false);
            var c = test.AddComponent<ViseController>();
            c.movingAssembly = moving.transform; c.rotatingHandle = handle.transform;
            c.localTravelAxis = source.localTravelAxis;
            c.closedOffset = source.closedOffset; c.openOffset = source.openOffset;
            c.travelSpeed = source.travelSpeed; c.travelPerTurn = source.travelPerTurn;
            c.SendMessage("Awake");
            for (int cycle = 0; cycle < 3; cycle++)
            {
                c.Turn(true, 100f);
                Require(Vector3.Distance(moving.transform.localPosition, c.localTravelAxis * c.closedOffset) < 0.00001f, "Closed stop");
                var rotation = handle.transform.localRotation;
                c.Turn(true, 100f);
                Require(Quaternion.Angle(rotation, handle.transform.localRotation) < 0.001f, "Rotation at stop");
                c.Turn(false, 100f);
                Require(Vector3.Distance(moving.transform.localPosition, c.localTravelAxis * c.openOffset) < 0.00001f, "Open stop");
                Require(moving.transform.localRotation == Quaternion.identity, "Jaw must not rotate");
            }
            Require(source.GetComponentsInChildren<Collider>().Length >= 2, "Target colliders");
            File.WriteAllText("Temp/ViseTest.result.txt", "PASS: close/open stops, no overrun, stationary jaw rotation, repeated reversal, target colliders. Scene remains unsaved.");
        }
        finally { Object.DestroyImmediate(test); }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new System.InvalidOperationException(message);
    }
}
