using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;

[InitializeOnLoad]
public static class PetrusiliusPlayerSetup
{
    const string Folder = "Assets/GameObjects/Characters/Petrusilius";
    const string ModelPath = Folder + "/Petrusilius.fbx";
    const string Request = "Temp/PetrusiliusPlayerSetup.request";
    const string Report = "Temp/PetrusiliusPlayerSetup.result.txt";

    static PetrusiliusPlayerSetup() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch (Exception e) { File.WriteAllText(Report, "FAILED\n" + e); Debug.LogException(e); }
    }

    [MenuItem("Tools/Hotzenplotz/Petrusilius als Player einrichten")]
    public static void Configure()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        var clips = importer.defaultClipAnimations;
        if (!clips.Any(c => c.name.EndsWith("Walking")) || !clips.Any(c => c.name.EndsWith("Idle")))
            throw new Exception("FBX must contain Walking and Idle clips: " + string.Join(", ", clips.Select(c => c.name)));
        importer.clipAnimations = clips.Where(c => c.name.EndsWith("Walking") || c.name.EndsWith("Idle"))
            .Select(c => { c.name = c.name.EndsWith("Walking") ? "Walking" : "Idle";
                c.loopTime = true; c.loopPose = false; return c; }).ToArray();
        importer.SaveAndReimport();
        var animations = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
        var walk = animations.Single(c => c.name == "Walking");
        var idle = animations.Single(c => c.name == "Idle");
        string controllerPath = Folder + "/Petrusilius.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.parameters = new[] { new AnimatorControllerParameter { name = "IsWalking", type = AnimatorControllerParameterType.Bool } };
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        var idleState = machine.AddState("Idle", new Vector3(220, 60)); idleState.motion = idle;
        var walkState = machine.AddState("Walking", new Vector3(470, 60)); walkState.motion = walk;
        machine.defaultState = idleState;
        var start = idleState.AddTransition(walkState);
        start.hasExitTime = false; start.hasFixedDuration = true; start.duration = 0.12f;
        start.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
        var stop = walkState.AddTransition(idleState);
        stop.hasExitTime = false; stop.hasFixedDuration = true; stop.duration = 0.15f;
        stop.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
        EditorUtility.SetDirty(controller);

        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Petrusilius.mat");
        if (material == null) {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("URP Lit shader unavailable");
            material = new Material(shader); material.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.6f));
            AssetDatabase.CreateAsset(material, Folder + "/Petrusilius.mat");
        }
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WhiteboxPlayerMovement>(true)).Single();
        var capsule = player.GetComponent<CharacterController>();
        if (capsule == null) throw new Exception("Player CharacterController missing");
        string sceneBackup = "Backups/SampleScene.before_Petrusilius.unity";
        if (!File.Exists(sceneBackup)) File.Copy(scene.path, sceneBackup);
        var old = player.transform.Find("PetrusiliusVisual");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene);
        visual.name = "PetrusiliusVisual";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length < 2) throw new Exception("Body or hat missing from FBX");
        Bounds bounds = new Bounds(); bool first = true;
        foreach (var r in renderers) {
            // Measure bounds in the Player's local space, including its existing scale.
            var b = r.bounds;
            for (int i = 0; i < 8; i++) {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = player.transform.InverseTransformPoint(corner);
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
            r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
        }
        float scale = capsule.height * 1.25f / bounds.size.y;
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = new Vector3(-bounds.center.x * scale,
            capsule.center.y - capsule.height * 0.5f - bounds.min.y * scale, -bounds.center.z * scale);
        var animator = visual.GetComponent<Animator>();
        if (animator == null) animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var playerSerialized = new SerializedObject(player);
        playerSerialized.FindProperty("characterAnimator").objectReferenceValue = animator;
        playerSerialized.ApplyModifiedPropertiesWithoutUndo();
        // Keep the original collision controller and interaction references intact.
        var capsuleRenderer = player.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null) UnityEngine.Object.DestroyImmediate(capsuleRenderer);
        var capsuleFilter = player.GetComponent<MeshFilter>();
        if (capsuleFilter != null) UnityEngine.Object.DestroyImmediate(capsuleFilter);
        // The existing game uses a first-person camera: keep the head/hat out of its view.
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        int layer = LayerMask.NameToLayer("PlayerVisual");
        if (layer < 0) {
            for (int i = 31; i >= 8; i--) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) {
                layer = i; layers.GetArrayElementAtIndex(i).stringValue = "PlayerVisual"; break;
            }
            if (layer < 0) throw new Exception("No free layer for first-person character visibility");
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (Transform t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.cullingMask &= ~(1 << layer);
        AssetDatabase.SaveAssets();
        VerifyController(visual, controller);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
        File.WriteAllText(Report, "SUCCESS\nScene: " + scene.path + "\nWalk length: " + walk.length
            + "\nRenderers: " + renderers.Length + "\nVisual scale: " + scale
            + "\nIdle / Walking transitions verified. CharacterController preserved. Root motion disabled.");
        Debug.Log("Petrusilius Player integration completed and verified.");
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    static void VerifyController(GameObject visual, AnimatorController controller)
    {
        var scratch = UnityEngine.Object.Instantiate(visual);
        scratch.hideFlags = HideFlags.HideAndDontSave;
        var graph = PlayableGraph.Create("PetrusiliusAnimationVerification");
        try {
            var animator = scratch.GetComponent<Animator>(); animator.runtimeAnimatorController = null;
            var playable = AnimatorControllerPlayable.Create(graph, controller);
            var output = AnimationPlayableOutput.Create(graph, "Character", animator); output.SetSourcePlayable(playable);
            graph.Play(); graph.Evaluate(0.01f);
            if (!playable.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new Exception("Idle state failed");
            playable.SetBool("IsWalking", true);
            for (int i = 0; i < 25; i++) graph.Evaluate(0.02f);
            if (!playable.GetCurrentAnimatorStateInfo(0).IsName("Walking")) throw new Exception("Walking transition failed");
            playable.SetBool("IsWalking", false);
            for (int i = 0; i < 25; i++) graph.Evaluate(0.02f);
            if (!playable.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new Exception("Stop transition failed");
            RenderPreview(scratch);
        } finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(scratch); }
    }

    static void RenderPreview(GameObject model)
    {
        model.transform.position = new Vector3(10000, 10000, 10000);
        model.transform.rotation = Quaternion.identity;
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        var cameraObject = new GameObject("PetrusiliusPreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
        var lightObject = new GameObject("PetrusiliusPreviewLight") { hideFlags = HideFlags.HideAndDontSave };
        var target = new RenderTexture(768, 768, 24);
        var texture = new Texture2D(768, 768, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try {
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.20f, 0.23f);
            camera.orthographic = true; camera.orthographicSize = bounds.size.y * 0.65f;
            camera.transform.position = bounds.center + new Vector3(0.8f, 0.25f, 1f).normalized * bounds.size.y * 3;
            camera.transform.LookAt(bounds.center); camera.targetTexture = target;
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 2f; light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(35, -35, 0);
            camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); texture.Apply();
            File.WriteAllBytes("Temp/PetrusiliusUnityPreview.png", texture.EncodeToPNG());
        } finally {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
