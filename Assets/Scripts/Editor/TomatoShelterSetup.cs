using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TomatoShelterSetup
{
    [InitializeOnLoadMethod]
    private static void Register() => EditorApplication.update += Check;

    private static void Check()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnableTomatoRain();
        const string request = "Temp/DisableTomatoShelter.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        int count = 0;
        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name != "Tomato_House" || !candidate.gameObject.scene.IsValid() ||
                EditorSceneManager.IsPreviewScene(candidate.gameObject.scene)) continue;
            Undo.RecordObject(candidate.gameObject, "Tomaten-Regenschutz voruebergehend deaktivieren");
            candidate.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(candidate.gameObject);
            EditorSceneManager.MarkSceneDirty(candidate.gameObject.scene);
            count++;
        }
        File.WriteAllText("Temp/DisableTomatoShelter.result", "Deactivated Tomato_House: " + count);
        File.Delete(request);
    }

    private static void EnableTomatoRain()
    {
        const string request = "Temp/EnableTomatoRain.request";
        if (!File.Exists(request)) return;
        int count = 0;
        foreach (GardenBed bed in Object.FindObjectsByType<GardenBed>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bed.name != "GardenBed (2)" || bed.gameObject.scene.name != "SampleScene" ||
                EditorSceneManager.IsPreviewScene(bed.gameObject.scene)) continue;
            var settings = new SerializedObject(bed);
            settings.FindProperty("receivesRain").boolValue = true;
            settings.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(bed);
            EditorSceneManager.MarkSceneDirty(bed.gameObject.scene);
            count++;
        }
        if (count == 0) return;
        File.WriteAllText("Temp/EnableTomatoRain.result", "Tomato beds receiving rain: " + count);
        File.Delete(request);
    }
}
