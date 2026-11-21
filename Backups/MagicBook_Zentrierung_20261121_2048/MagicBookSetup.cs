using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MagicBookSetup
{
    public const string ModelPath = "Assets/GameObjects/Environment/Study_Room/Magic_Book_Pages.fbx";
    const string RequestPath = "Temp/MagicBookSetup.request";
    const string ReportPath = "Temp/MagicBookSetup.result.txt";
    const string PrefabPath = "Assets/GameObjects/Environment/Study_Room/Magic_Book_Interactive.prefab";

    static MagicBookSetup() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying && SessionState.GetBool("MagicBook.PlaytestView",false))
        {
            SessionState.SetBool("MagicBook.PlaytestView",false);
            try { PreparePlaytestView(); File.WriteAllText(ReportPath,PlaytestStatus()); }
            catch(Exception e) { File.WriteAllText(ReportPath,"FAILED\n"+e); Debug.LogException(e); }
        }
        if (!File.Exists(RequestPath) || (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)) return;
        string request;
        try { request = File.ReadAllText(RequestPath).Trim(); }
        catch (IOException) { return; }
        if (request.Length == 0) return;
        File.Delete(RequestPath);
        try
        {
            if (request == "status") File.WriteAllText(ReportPath,PlaytestStatus());
            else if (request == "playtest")
            {
                SessionState.SetBool("MagicBook.PlaytestView",true);
                if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
            }
            else if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing the setup.");
            else if (request == "inspect") File.WriteAllText(ReportPath, Inspect());
            else if (request == "configure") Configure();
            else if (request == "verify") File.WriteAllText(ReportPath, Verify());
            else throw new InvalidOperationException("Unknown book setup request.");
        }
        catch (Exception e) { File.WriteAllText(ReportPath, "FAILED\n" + e); Debug.LogException(e); }
    }

    static void PreparePlaytestView()
    {
        var book=UnityEngine.Object.FindFirstObjectByType<ReadableBookInteractable>();
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerInteraction>();
        if(!book || !player) throw new InvalidOperationException("Scene book or player missing.");
        var camera=(Camera)new SerializedObject(player).FindProperty("playerCamera").objectReferenceValue;
        var movement=player.GetComponent<WhiteboxPlayerMovement>();
        if(movement) movement.enabled=false;
        var controller=player.GetComponent<CharacterController>();
        if(controller) controller.enabled=false;
        // Temporary Play Mode viewpoint; exiting Play Mode restores the saved player.
        Vector3 target=book.GetComponent<BoxCollider>().bounds.center;
        Vector3 view=target+new Vector3(0,0.65f,0.75f);
        player.transform.position+=view-camera.transform.position;
        camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position,Vector3.up);
        Cursor.lockState=CursorLockMode.None;
        Cursor.visible=true;
        Physics.SyncTransforms();
        Require(player.IsTargetingReadableBook,"Play Mode camera cannot target the book.");
        EditorApplication.ExecuteMenuItem("Window/General/Game");
    }

    static string PlaytestStatus()
    {
        var book=UnityEngine.Object.FindFirstObjectByType<ReadableBookInteractable>();
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerInteraction>();
        return "PLAYING "+EditorApplication.isPlaying+"\nBOOK "+(book?book.name:"missing")
            +"\nOPEN "+(book && book.IsOpen)+"\nPAGES "+(book?book.TurnedPages:-1)
            +"\nBUSY "+(book && book.IsAnimating)+"\nTARGET "+(player && player.IsTargetingReadableBook)
            +"\nPROMPT "+(book?book.interactionText:"");
    }

    [MenuItem("Tools/Hotzenplotz/Zauberbuch mit E und F einrichten")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open SampleScene first.");
        var all = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).ToArray();
        var table = all.Single(t => t.name == "Table" && t.GetComponentsInChildren<Renderer>().Any(r => r.name == "Cube.002"));
        var oldBooks = all.Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
            && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == ModelPath
            && t.GetComponentInParent<ReadableBookInteractable>() == null).ToArray();
        var existing = all.Select(t => t.GetComponent<ReadableBookInteractable>()).Where(b => b != null).ToArray();
        if (oldBooks.Length > 1 || existing.Length > 1) throw new InvalidOperationException("More than one scene book; choose the target explicitly.");
        string backup = "Backups/MagicBookUnity_20261121/Unity_" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(backup);
        if (!EditorSceneManager.SaveScene(scene, backup + "/SampleScene.open.unity", true))
            throw new IOException("Could not back up the open scene.");
        if (File.Exists(PrefabPath)) File.Copy(PrefabPath, backup + "/Magic_Book_Interactive.prefab");
        if (File.Exists(PrefabPath + ".meta")) File.Copy(PrefabPath + ".meta", backup + "/Magic_Book_Interactive.prefab.meta");

        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
        var opening = clips.Single(c => c.name == "Buch_Oeffnen");
        var pages = Enumerable.Range(1,10).Select(i => clips.Single(c => c.name == $"Page_{i:00}_Umblaettern")).ToArray();
        var preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Magic_Book_Interactive");
        SceneManager.MoveGameObjectToScene(root, preview);
        Bounds openBounds;
        try
        {
            var visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), preview);
            model.transform.SetParent(visuals.transform, false);
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            opening.SampleAnimation(model,0);
            Bounds rawClosed = Measure(root);
            const float scale = 0.10f;
            visuals.transform.localScale = Vector3.one * scale;
            visuals.transform.localPosition = new Vector3(-rawClosed.center.x, -rawClosed.min.y, -rawClosed.center.z) * scale;
            Bounds closed = Measure(root);
            opening.SampleAnimation(model, opening.length);
            openBounds = Measure(root);
            pages[9].SampleAnimation(model,pages[9].length);
            openBounds.Encapsulate(Measure(root));
            openBounds.Encapsulate(closed);
            closed.Expand(0.012f);
            openBounds.Expand(0.012f);
            opening.SampleAnimation(model,0);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = closed.center;
            collider.size = closed.size;
            var book = root.AddComponent<ReadableBookInteractable>();
            var serialized = new SerializedObject(book);
            serialized.FindProperty("animationRoot").objectReferenceValue = model;
            serialized.FindProperty("openingClip").objectReferenceValue = opening;
            var pageList = serialized.FindProperty("pageClips");
            pageList.arraySize = pages.Length;
            for (int i=0; i<pages.Length; i++) pageList.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
            serialized.FindProperty("interactionCollider").objectReferenceValue = collider;
            serialized.FindProperty("closedInteractionBounds").boundsValue = closed;
            serialized.FindProperty("openInteractionBounds").boundsValue = openBounds;
            serialized.FindProperty("interactionText").stringValue = "E – Buch öffnen";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
        string checks = Verify();
        if (!checks.StartsWith("SUCCESS")) throw new InvalidOperationException(checks);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),scene);
        Undo.RegisterCreatedObjectUndo(instance,"Zauberbuch einrichten");
        var tabletop = table.GetComponentsInChildren<Collider>()
            .Where(c => c.GetComponentInParent<ReadableBookInteractable>() == null)
            .OrderByDescending(c => c.bounds.max.y).First().bounds;
        Quaternion yaw = Quaternion.Euler(0,table.eulerAngles.y,0);
        Vector3 offset = yaw * new Vector3(openBounds.center.x,0,openBounds.center.z);
        instance.transform.position = new Vector3(tabletop.center.x,tabletop.max.y + 0.009f,tabletop.center.z) - offset;
        instance.transform.rotation = yaw;
        instance.transform.SetParent(table,true);
        foreach (var old in oldBooks) Undo.DestroyObjectImmediate(old.gameObject);
        foreach (var old in existing) Undo.DestroyObjectImmediate(old.gameObject);
        Selection.activeGameObject = instance;
        if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the book setup.");
        File.WriteAllText(ReportPath,checks + "\nPREFAB " + PrefabPath + "\nSCENE BOOK " + HierarchyPath(instance.transform)
            + "\nPOSITION " + instance.transform.position + "\nBACKUP " + backup);
    }

    static Bounds Measure(GameObject root)
    {
        var points = root.GetComponentsInChildren<MeshFilter>(true).SelectMany(m => m.sharedMesh.vertices.Select(m.transform.TransformPoint))
            .Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(Points)).ToArray();
        if (points.Length == 0) throw new InvalidOperationException("Book has no mesh vertices.");
        Bounds bounds = new Bounds(root.transform.InverseTransformPoint(points[0]),Vector3.zero);
        foreach (var point in points) bounds.Encapsulate(root.transform.InverseTransformPoint(point));
        return bounds;
    }

    static string Verify()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),testScene);
            root.transform.SetPositionAndRotation(new Vector3(20000,1000,20000),Quaternion.Euler(0,33,0));
            var book = root.GetComponent<ReadableBookInteractable>();
            var advance = typeof(ReadableBookInteractable).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic);
            typeof(ReadableBookInteractable).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(book,null);
            Vector3 position=root.transform.position;
            Quaternion rotation=root.transform.rotation;
            Vector3 scale=root.transform.localScale;
            var page= root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="Page_01");
            var before=Points(page);
            book.InteractReverse();
            Require(!book.IsAnimating && !book.IsOpen,"Closed F must do nothing.");
            book.Interact();
            book.Interact(); book.InteractReverse();
            Complete();
            Require(book.IsOpen && book.TurnedPages==0,"First E must open only.");
            Require(before.Zip(Points(page),(a,b)=>Vector3.Distance(a,b)).Max()<0.0001f,"Pages moved during opening.");
            for(int i=1;i<=10;i++) { book.Interact(); Complete(); Require(book.TurnedPages==i,"Forward page count."); }
            book.Interact();
            Require(!book.IsAnimating && book.TurnedPages==10,"Last page must stop.");
            for(int i=9;i>=0;i--) { book.InteractReverse(); Complete(); Require(book.IsOpen && book.TurnedPages==i,"Reverse page count."); }
            book.InteractReverse(); Complete();
            Require(!book.IsOpen && book.TurnedPages==0,"F from the first spread must close.");

            var cameraObject=new GameObject("Book interaction camera");
            SceneManager.MoveGameObjectToScene(cameraObject,testScene);
            var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;
            var interaction=cameraObject.AddComponent<PlayerInteraction>();
            var serialized=new SerializedObject(interaction);
            serialized.FindProperty("playerCamera").objectReferenceValue=camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var collider=root.GetComponent<BoxCollider>();
            camera.transform.position=collider.bounds.center+Vector3.up;
            camera.transform.LookAt(collider.bounds.center);
            Physics.SyncTransforms();
            Require(interaction.IsTargetingReadableBook,"Aimed-at book not found.");
            var request=typeof(PlayerInteraction).GetMethod("TryInteract",BindingFlags.Instance|BindingFlags.NonPublic);
            request.Invoke(interaction,new object[]{false}); Complete();
            Require(book.IsOpen,"E raycast did not open book.");
            request.Invoke(interaction,new object[]{false}); Complete();
            Require(book.TurnedPages==1,"E raycast did not turn page.");
            request.Invoke(interaction,new object[]{true}); Complete();
            Require(book.TurnedPages==0,"F raycast did not reverse page.");
            var wall=new GameObject("Occlusion check");
            SceneManager.MoveGameObjectToScene(wall,testScene);
            wall.transform.position=camera.transform.position+camera.transform.forward*0.5f;
            wall.AddComponent<BoxCollider>().size=new Vector3(0.5f,0.1f,0.5f);
            Physics.SyncTransforms();
            Require(!interaction.IsTargetingReadableBook,"Book targeted through a wall.");
            request.Invoke(interaction,new object[]{true});
            Require(!book.IsAnimating && book.IsOpen,"F interacted through a wall.");
            UnityEngine.Object.DestroyImmediate(wall);
            camera.transform.position=collider.bounds.center+Vector3.up*4;
            camera.transform.LookAt(collider.bounds.center);
            Physics.SyncTransforms();
            Require(!interaction.IsTargetingReadableBook,"Book targeted outside interaction range.");
            return "SUCCESS\nChecked: closed start; E opens; ten pages forward; ten pages backward; F closes; endpoint limits; busy inputs ignored; placed root stays fixed; pages stay put during opening; E/F raycast routing; walls and distance block interaction.";

            void Complete()
            {
                for(int step=0;step<40 && book.IsAnimating;step++)
                {
                    advance.Invoke(book,new object[]{0.05f});
                    Require(Vector3.Distance(root.transform.position,position)<0.00001f && Quaternion.Angle(root.transform.rotation,rotation)<0.001f
                        && Vector3.Distance(root.transform.localScale,scale)<0.00001f,"Placed book root moved.");
                }
                Require(!book.IsAnimating,"Animation did not finish.");
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(testScene,true);
            if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }

    static string Inspect()
    {
        var report = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        report.AppendLine("SCENE " + scene.path + " dirty=" + scene.isDirty);
        report.AppendLine("SELECTED " + (Selection.activeGameObject ? HierarchyPath(Selection.activeGameObject.transform) : "none"));
        foreach (var t in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)))
        {
            if (t.name == "Table" || t.name == "Study_Room" || t.name.Contains("Magic_Book") || t.GetComponent<PlayerInteraction>())
            {
                report.AppendLine("OBJECT " + HierarchyPath(t) + " position=" + t.position + " scale=" + t.lossyScale);
                foreach (var r in t.GetComponentsInChildren<Renderer>(true).Take(12)) report.AppendLine(" RENDERER " + r.name + " " + r.bounds);
                foreach (var c in t.GetComponentsInChildren<Collider>(true).Take(12)) report.AppendLine(" COLLIDER " + c.name + " " + c.bounds);
            }
        }
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        foreach (var c in clips)
        {
            var bindings = AnimationUtility.GetCurveBindings(c);
            report.AppendLine("CLIP " + c.name + " length=" + c.length + " bindings=" + bindings.Length);
            foreach (var b in bindings.Where(b => b.path == "" || b.type == typeof(Animator)).Take(18))
                report.AppendLine(" CURVE " + b.path + ":" + b.type.Name + ":" + b.propertyName);
        }
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject model = null;
        try
        {
            model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), preview);
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var opening = clips.Single(c => c.name == "Buch_Oeffnen");
            opening.SampleAnimation(model, 0);
            var page = model.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name == "Page_01");
            var before = Points(page);
            report.AppendLine("SAMPLED CLOSED ROOT " + model.transform.localPosition + " " + model.transform.localRotation.eulerAngles);
            opening.SampleAnimation(model, opening.length);
            var after = Points(page);
            report.AppendLine("SAMPLED OPEN ROOT " + model.transform.localPosition + " " + model.transform.localRotation.eulerAngles);
            report.AppendLine("OPENING STATIONARY PAGE ERROR " + before.Zip(after, (a,b) => Vector3.Distance(a,b)).Max());
            foreach (var t in model.GetComponentsInChildren<Transform>()) report.AppendLine("MODEL " + HierarchyPath(t) + " " + t.localPosition + " scale=" + t.localScale);
        }
        finally
        {
            if (model) UnityEngine.Object.DestroyImmediate(model);
            EditorSceneManager.ClosePreviewScene(preview);
        }
        return report.ToString();
    }

    internal static Vector3[] Points(SkinnedMeshRenderer renderer)
    {
        var mesh = new Mesh();
        try { renderer.BakeMesh(mesh); return mesh.vertices.Select(renderer.transform.TransformPoint).ToArray(); }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }

    static string HierarchyPath(Transform t) { return t.parent ? HierarchyPath(t.parent) + "/" + t.name : t.name; }
}
