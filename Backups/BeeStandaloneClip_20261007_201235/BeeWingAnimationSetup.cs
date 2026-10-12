using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BeeWingAnimationSetup
{
    const string Folder = "Assets/GameObjects/Environment/Garden/Animals";
    const string Request = "Temp/BeeWingAnimationSetup.request";
    static BeeWingAnimationSetup() { EditorApplication.update += Check; }
    static void Check()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch (Exception e) { File.WriteAllText("Temp/BeeWingAnimationSetup.result.txt", "FAILED\n" + e); Debug.LogException(e); }
    }

    [MenuItem("Tools/Hotzenplotz/Bienen-Fluegelschlag einrichten")]
    public static void Configure()
    {
        var report = new System.Text.StringBuilder();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        string backup = "Backups/BeeAnimations_20261006/SampleScene.before_bee_animation.unity";
        if (!File.Exists(backup)) File.Copy(scene.path, backup);
        int count = 0;
        foreach (string name in new[] { "Bee", "Bee_2" })
        {
            string path = Folder + "/" + name + ".fbx";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true; importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importCameras = false; importer.importLights = false;
            var defaults = importer.defaultClipAnimations;
            if (defaults.Length != 1) throw new Exception(name + ": expected one combined wingbeat take, found " + defaults.Length);
            defaults[0].name = "Wingbeat"; defaults[0].loopTime = true; defaults[0].loopPose = false;
            importer.clipAnimations = defaults; importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(a => a.name == "Wingbeat");
            if (clip.length <= 0 || !clip.isLooping) throw new Exception(name + ": missing loop");
            string controllerPath = Folder + "/" + name + "Wingbeat.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            var wingbeat = machine.AddState("Wingbeat"); wingbeat.motion = clip;
            machine.defaultState = wingbeat; EditorUtility.SetDirty(controller);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try {
                instance.name = name + "Animated";
                Bind(instance, controller);
                Verify(instance, clip, report, name);
                PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/" + name + "Animated.prefab");
            } finally { UnityEngine.Object.DestroyImmediate(instance); }
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) != path) continue;
                    Bind(t.gameObject, controller); count++;
                }
        }
        AssetDatabase.SaveAssets();
        if (count > 0) {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save scene");
        }
        report.AppendLine("Scene bee instances configured: " + count);
        File.WriteAllText("Temp/BeeWingAnimationSetup.result.txt", "SUCCESS\n" + report);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("Bee wing animations configured and verified.");
    }

    static void Bind(GameObject bee, RuntimeAnimatorController controller)
    {
        var animator = bee.GetComponent<Animator>();
        if (animator == null) animator = bee.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(animator);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
    }

    static void Verify(GameObject bee, AnimationClip clip, System.Text.StringBuilder report, string name)
    {
        var rootPosition = bee.transform.position;
        var rootRotation = bee.transform.rotation;
        var transforms = bee.GetComponentsInChildren<Transform>(true);
        clip.SampleAnimation(bee, 0);
        var before = transforms.Select(t => t.localRotation).ToArray();
        clip.SampleAnimation(bee, clip.length * 0.25f);
        int moved = Enumerable.Range(0, transforms.Length).Count(i => Quaternion.Angle(before[i], transforms[i].localRotation) > 5);
        if (moved < 2) throw new Exception(name + ": both wings must move; moved transforms = " + moved);
        if (Vector3.Distance(rootPosition, bee.transform.position) > 0.0001f || Quaternion.Angle(rootRotation, bee.transform.rotation) > 0.01f)
            throw new Exception(name + ": wingbeat moves the whole bee");
        clip.SampleAnimation(bee, clip.length);
        float seam = Enumerable.Range(0, transforms.Length).Max(i => Quaternion.Angle(before[i], transforms[i].localRotation));
        if (seam > 0.1f) throw new Exception(name + ": wingbeat loop does not match, seam=" + seam);
        report.AppendLine(name + ": " + (1f / clip.length).ToString("F1") + " wingbeats/sec; " + moved + " wing transforms animated; loop verified.");
        clip.SampleAnimation(bee, 0);
    }
}
