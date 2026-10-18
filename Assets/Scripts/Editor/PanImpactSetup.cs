using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PanImpactSetup
{
    private const string Request = "Temp/PanImpactSetup.request";

    [InitializeOnLoadMethod]
    private static void Register() => EditorApplication.update += CheckRequest;

    public static void ConfigureSavedScene()
    {
        File.Delete(Request);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Configure();
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Szene konnte nicht gespeichert werden.");
        File.WriteAllText("Temp/PanImpactSetup.result.txt", "OK: Pan eingerichtet und SampleScene gespeichert.");
    }

    private static void CheckRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch (Exception error) { File.WriteAllText("Temp/PanImpactSetup.result.txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("Tools/Hotzenplotz/Pfannen-Aufprallgeraeusche einrichten")]
    public static void Configure()
    {
        var clips = new AudioClip[4];
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Art/Audio/Metall_Sound_{i + 1}.ogg");
            if (clips[i] == null) throw new InvalidOperationException("Metall-Sound fehlt: " + (i + 1));
        }
        int count = 0;
        foreach (PickupInteractable pickup in UnityEngine.Object.FindObjectsByType<PickupInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pickup.name != "Pan" || !pickup.gameObject.scene.IsValid() || pickup.GetComponent<Rigidbody>() == null) continue;
            MetalImpactSound sound = pickup.GetComponent<MetalImpactSound>();
            if (sound == null) sound = Undo.AddComponent<MetalImpactSound>(pickup.gameObject);
            var serialized = new SerializedObject(sound);
            var sourceProperty = serialized.FindProperty("impactSource");
            var source = sourceProperty.objectReferenceValue as AudioSource;
            if (source == null) source = Undo.AddComponent<AudioSource>(pickup.gameObject);
            Undo.RecordObject(source, "Pfannenklang einstellen");
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 20f;
            source.volume = 1f;
            sourceProperty.objectReferenceValue = source;
            var array = serialized.FindProperty("clips");
            array.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(sound);
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            EditorSceneManager.MarkSceneDirty(pickup.gameObject.scene);
            count++;
        }
        if (count == 0) throw new InvalidOperationException("Keine Pan mit PickupInteractable und Rigidbody in der offenen Szene gefunden.");
        File.WriteAllText("Temp/PanImpactSetup.result.txt", "OK: " + count + " Pan eingerichtet; Szene bitte speichern.");
        Debug.Log("Pfannen-Aufprallgeraeusche eingerichtet. Szene bitte speichern.");
    }
}
