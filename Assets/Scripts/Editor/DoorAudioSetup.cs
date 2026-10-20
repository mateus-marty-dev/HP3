using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DoorAudioSetup
{
    private const string Request = "Temp/DoorAudioSetup.request";

    [InitializeOnLoadMethod]
    private static void Register() => EditorApplication.update += CheckRequest;

    private static void CheckRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/DoorAudioSetup.result.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Hotzenplotz/Tuergeraeusch einrichten")]
    public static void Configure()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Art/Audio/Door/Door_Open.wav");
        var closing = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Door/Door_Close.wav");
        if (clip == null || closing == null) throw new InvalidOperationException("Tuer-Audioclips fehlen.");
        int count = 0;
        foreach (var door in UnityEngine.Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!door.gameObject.scene.IsValid()) continue;
            var serialized = new SerializedObject(door);
            var sourceProperty = serialized.FindProperty("doorAudio");
            var source = sourceProperty.objectReferenceValue as AudioSource;
            if (source == null) source = Undo.AddComponent<AudioSource>(door.gameObject);
            Undo.RecordObject(source, "Tuergeraeusch einstellen");
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 20f;
            source.dopplerLevel = 0f;
            source.clip = clip;
            source.volume = 0.75f;
            sourceProperty.objectReferenceValue = source;
            serialized.FindProperty("doorSound").objectReferenceValue = clip;
            serialized.FindProperty("closeSound").objectReferenceValue = closing;
            serialized.FindProperty("openArrivalTime").floatValue = 0.85f;
            serialized.FindProperty("closeArrivalTime").floatValue = 0.45f;
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
            count++;
        }
        if (count == 0) throw new InvalidOperationException("Keine DoorInteractable-Tuer in der offenen Szene gefunden.");
        File.WriteAllText("Temp/DoorAudioSetup.result.txt", "OK: " + count + " Tuer(en) eingerichtet; Szene bitte speichern.");
        Debug.Log("Tuergeraeusch eingerichtet. Szene bitte speichern.");
    }
}
