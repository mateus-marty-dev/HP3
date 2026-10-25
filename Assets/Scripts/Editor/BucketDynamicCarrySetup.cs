using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BucketDynamicCarrySetup
{
    [InitializeOnLoadMethod]
    private static void Register() => EditorApplication.update += ApplyRequest;

    private static void ApplyRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        ApplyOtherVessels();
        const string request = "Temp/BucketDynamicCarry.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        int count = 0;
        foreach (var contents in Object.FindObjectsByType<BucketContents>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (contents.name != "Bucket" || !contents.gameObject.scene.IsValid() ||
                EditorSceneManager.IsPreviewScene(contents.gameObject.scene)) continue;
            var settings = new SerializedObject(contents);
            settings.FindProperty("supportDynamicContents").boolValue = true;
            settings.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(contents);
            EditorSceneManager.MarkSceneDirty(contents.gameObject.scene);
            count++;
        }
        File.WriteAllText("Temp/BucketDynamicCarry.result", "Configured Buckets: " + count);
        File.Delete(request);
    }

    private static void ApplyOtherVessels()
    {
        const string request = "Temp/PanBasketCarry.request";
        if (!File.Exists(request)) return;
        var report = new System.Text.StringBuilder();
        int count = 0;
        foreach (var body in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if ((body.name != "Pan" && body.name != "Basket") || !body.gameObject.scene.IsValid() ||
                EditorSceneManager.IsPreviewScene(body.gameObject.scene)) continue;
            BoxCollider floor = null;
            float bestArea = 0f;
            foreach (var box in body.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger || !box.enabled || box.attachedRigidbody != body) continue;
                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                int thin = size.x < size.y ? 0 : 1;
                if (size.z < size[thin]) thin = 2;
                Vector3 normal = Vector3.zero; normal[thin] = 1f;
                if (Mathf.Abs(Vector3.Dot(box.transform.TransformDirection(normal), Vector3.up)) < 0.9f) continue;
                float area = size[(thin + 1) % 3] * size[(thin + 2) % 3];
                if (area > bestArea) { bestArea = area; floor = box; }
            }
            if (floor == null) { report.AppendLine(body.name + ": no floor found"); continue; }
            var contents = body.GetComponent<BucketContents>();
            if (contents == null) contents = Undo.AddComponent<BucketContents>(body.gameObject);
            var settings = new SerializedObject(contents);
            settings.FindProperty("supportDynamicContents").boolValue = true;
            settings.FindProperty("transportFloor").objectReferenceValue = floor;
            settings.FindProperty("rectangularContents").boolValue = body.name == "Basket";
            settings.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(contents);
            EditorSceneManager.MarkSceneDirty(body.gameObject.scene);
            report.AppendLine(body.name + ": enabled, floor=" + floor.name + ", size=" + floor.size + ", center=" + floor.center);
            count++;
        }
        File.WriteAllText("Temp/PanBasketCarry.result", report.ToString());
        if (count == 2) File.Delete(request);
    }
}
