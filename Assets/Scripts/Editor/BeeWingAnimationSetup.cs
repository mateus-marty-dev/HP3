using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BeeWingAnimationSetup
{
    const string Folder = "Assets/GameObjects/Environment/Garden/Animals";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string Request = "Temp/BeeWingAnimationSetup.request";
    const float Frequency = 8f;
    const float Amplitude = 35f;

    static BeeWingAnimationSetup() { EditorApplication.update += Check; }
    static void Check()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch (Exception e)
        {
            File.WriteAllText("Temp/BeeWingAnimationSetup.result.txt", "FAILED\n" + e);
            Debug.LogException(e);
        }
    }

    [MenuItem("Tools/Hotzenplotz/Bienen-Fluegelschlag einrichten")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Please stop Play Mode first.");
        string backup = "Backups/BeeUnityWingbeat_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        Directory.CreateDirectory(backup);
        Backup(ScenePath, backup);
        var report = new StringBuilder();
        var previousActive = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        int count = 0;
        try
        {
            foreach (string name in new[] { "Bee", "Bee_2" })
            {
                string path = Folder + "/" + name + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) { report.AppendLine(name + ": not present, skipped."); continue; }
                string clipPath = Folder + "/" + (name == "Bee" ? "Wingbeat" : name + "Wingbeat") + ".anim";
                string controllerPath = Folder + "/" + name + "Wingbeat.controller";
                string prefabPath = Folder + "/" + name + "Animated.prefab";
                Backup(clipPath, backup); Backup(controllerPath, backup); Backup(prefabPath, backup);
                var previewScene = EditorSceneManager.NewPreviewScene();
                GameObject instance = null;
                AnimationClip generated = null;
                try
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(model, previewScene);
                    var wings = FindWings(instance);
                    generated = BuildClip(instance, wings);
                    Verify(instance, generated, wings, report);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null) { clip = generated; AssetDatabase.CreateAsset(clip, clipPath); generated = null; }
                    else { EditorUtility.CopySerialized(generated, clip); EditorUtility.SetDirty(clip); }
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                    if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                    if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
                    var machine = controller.layers[0].stateMachine;
                    var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Wingbeat")
                        ?? machine.AddState("Wingbeat");
                    state.motion = clip; state.speed = 1; machine.defaultState = state;
                    EditorUtility.SetDirty(state); EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
                    instance.name = name + "Animated";
                    Bind(instance, controller);
                    // Keep existing prefab overrides; update only its Animator.
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                    {
                        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                        try { Bind(contents, controller); PrefabUtility.SaveAsPrefabAsset(contents, prefabPath); }
                        finally { PrefabUtility.UnloadPrefabContents(contents); }
                    }
                    else PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                        string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                        if (source != path && source != prefabPath) continue;
                        Bind(t.gameObject, controller); count++;
                    }
                    report.AppendLine("Independent clip: " + clipPath);
                }
                finally
                {
                    if (generated != null) UnityEngine.Object.DestroyImmediate(generated);
                    if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                    EditorSceneManager.ClosePreviewScene(previewScene);
                }
            }
            AssetDatabase.SaveAssets();
            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                // Never save unrelated unsaved user edits in an already open scene.
                if (opened && !EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save scene.");
            }
            report.AppendLine("Scene instances configured: " + count);
            report.AppendLine("Backup: " + backup);
            if (!opened && count > 0) report.AppendLine("Save the open scene with Ctrl+S.");
            File.WriteAllText("Temp/BeeWingAnimationSetup.result.txt", "SUCCESS\n" + report);
            Debug.Log(report.ToString());
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }

    static Transform[] FindWings(GameObject root)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        var wings = new[] { "Sphere.001", "Sphere.002" }.Select(n =>
            all.SingleOrDefault(t => t.name == n)).ToArray();
        if (wings.Any(t => t == null || t.GetComponent<MeshFilter>() == null))
            throw new InvalidOperationException("Expected separate wing meshes Sphere.001 and Sphere.002. No clip was replaced.");
        return wings;
    }

    static AnimationClip BuildClip(GameObject root, Transform[] wings)
    {
        var clip = new AnimationClip { name = "Wingbeat", frameRate = 64, legacy = false };
        float duration = 1f / Frequency;
        foreach (var wing in wings)
        {
            string path = AnimationUtility.CalculateTransformPath(wing, root.transform);
            Quaternion rest = wing.localRotation;
            // Unity's imported bee has X lateral, Y up, Z along its body.
            Vector3 axis = wing.parent.InverseTransformDirection(root.transform.forward).normalized;
            float side = Mathf.Sign(root.transform.InverseTransformPoint(wing.position).x);
            var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            for (int i = 0; i <= 32; i++)
            {
                float angle = i == 32 ? 0 : side * Amplitude * Mathf.Sin(i * Mathf.PI * 2 / 32);
                Quaternion q = Quaternion.AngleAxis(angle, axis) * rest;
                float time = i * duration / 32;
                curves[0].AddKey(time, q.x); curves[1].AddKey(time, q.y);
                curves[2].AddKey(time, q.z); curves[3].AddKey(time, q.w);
            }
            for (int c = 0; c < 4; c++)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform),
                    "m_LocalRotation." + "xyzw"[c]), curves[c]);
        }
        clip.EnsureQuaternionContinuity();
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true; settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    static void Verify(GameObject bee, AnimationClip clip, Transform[] wings, StringBuilder report)
    {
        var all = bee.GetComponentsInChildren<Transform>(true);
        var rotations = all.Select(t => t.localRotation).ToArray();
        var positions = all.Select(t => t.localPosition).ToArray();
        var scales = all.Select(t => t.localScale).ToArray();
        try
        {
            foreach (float fraction in new[] { 0f, .25f, .5f, .75f, 1f })
            {
                clip.SampleAnimation(bee, clip.length * fraction);
                for (int i = 0; i < all.Length; i++)
                {
                    float angle = Quaternion.Angle(rotations[i], all[i].localRotation);
                    bool shouldMove = wings.Contains(all[i]) && (fraction == .25f || fraction == .75f);
                    if ((shouldMove && angle < 30) || (!shouldMove && angle > .1f)
                        || Vector3.Distance(positions[i], all[i].localPosition) > .00001f
                        || Vector3.Distance(scales[i], all[i].localScale) > .00001f)
                        throw new InvalidOperationException("Wingbeat validation failed: " + all[i].name);
                }
            }
            report.AppendLine("Verified: two wings move, body stays still, loop closes; " + Frequency + " Hz.");
        }
        finally
        {
            for (int i = 0; i < all.Length; i++)
            { all[i].localRotation = rotations[i]; all[i].localPosition = positions[i]; all[i].localScale = scales[i]; }
        }
    }

    static void Bind(GameObject bee, RuntimeAnimatorController controller)
    {
        var animator = bee.GetComponent<Animator>();
        if (animator == null) animator = bee.AddComponent<Animator>();
        animator.enabled = true; animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(animator);
        if (PrefabUtility.IsPartOfPrefabInstance(animator)) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
    }
    static void Backup(string path, string folder)
    {
        if (File.Exists(path)) File.Copy(path, Path.Combine(folder, Path.GetFileName(path)), false);
        if (File.Exists(path + ".meta")) File.Copy(path + ".meta", Path.Combine(folder, Path.GetFileName(path) + ".meta"), false);
    }
}
