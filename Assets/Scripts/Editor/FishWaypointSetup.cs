using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FishWaypointSetup
{
    const string Folder = "Assets/GameObjects/Environment/Garden/Animals/";
    [MenuItem("Tools/Hotzenplotz/Fisch-Wegpunkte einrichten")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode zuerst beenden.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        if (scene.GetRootGameObjects().Any(g => g.name == "FishSwimming"))
            throw new InvalidOperationException("FishSwimming existiert bereits; Wegpunkte direkt bearbeiten.");
        var water = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HP3_WaterBody>(true)).First(w => w.PondRenderer != null);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Fish.fbx");
        if (model == null) throw new InvalidOperationException("Fish.fbx fehlt.");
        var clips = AssetDatabase.LoadAllAssetsAtPath(Folder + "Fish.fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        if (clips.Length == 0) throw new InvalidOperationException("Fish.fbx hat keinen importierten Animationsclip.");
        Directory.CreateDirectory("Backups/FishSwimming");
        File.Copy(scene.path, "Backups/FishSwimming/SampleScene_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "Fish.controller");
        File.Copy(Folder + "Fish.controller", "Backups/FishSwimming/Fish_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".controller");
        if (controller == null) throw new InvalidOperationException("Fish.controller fehlt.");
        var machine = controller.layers[0].stateMachine;
        var state = machine.defaultState ?? machine.AddState("Swim");
        var clip = UnityEngine.Object.Instantiate(clips[0]);
        clip.name = "FishSwim";
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, Folder + "FishSwim.anim");
        state.motion = clip;
        machine.defaultState = state;
        EditorUtility.SetDirty(controller);
        var root = new GameObject("FishSwimming");
        Undo.RegisterCreatedObjectUndo(root, "Fisch-Wegpunkte einrichten");
        var manager = root.AddComponent<AISpawner>();
        var bounds = water.PondRenderer.bounds;
        // Conservative central route, below the lowest wave crest, clear of the bank.
        Vector3 center = new Vector3(bounds.center.x, water.GetSurfaceY(bounds.center, 0) - 0.65f, bounds.center.z);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3;
            var point = new GameObject("Waypoint_" + (i + 1)).transform;
            point.SetParent(root.transform);
            point.position = center + new Vector3(Mathf.Cos(angle) * bounds.size.x * 0.18f, 0, Mathf.Sin(angle) * bounds.size.z * 0.18f);
            manager.Waypoints.Add(point);
        }
        var fishRoot = new GameObject("Fish");
        fishRoot.transform.SetParent(root.transform);
        fishRoot.transform.position = manager.Waypoints[0].position;
        var fish = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        fish.transform.SetParent(fishRoot.transform, false);
        var animator = fish.GetComponentInChildren<Animator>();
        if (animator == null) animator = fish.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        var renderers = fish.GetComponentsInChildren<Renderer>();
        var fishBounds = renderers[0].bounds;
        foreach (var renderer in renderers) fishBounds.Encapsulate(renderer.bounds);
        float length = Mathf.Max(fishBounds.size.x, fishBounds.size.y, fishBounds.size.z);
        fish.transform.localScale *= 0.55f / Mathf.Max(0.001f, length);
        var move = fishRoot.AddComponent<AIMove>();
        move.manager = manager;
        move.visual = fish.transform;
        // Fish.fbx points along local -Z.
        move.modelRotationOffset = new Vector3(0f, 180f, 0f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/FishSetup.result.txt", "SUCCESS\nClip: " + clips[0].name + " duration=" + clips[0].length + "\nWater bounds: " + bounds + "\nFish original bounds: " + fishBounds + "\nRoute center: " + center);
    }
}



