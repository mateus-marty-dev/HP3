using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AppleDistributor))]
public class AppleDistributorEditor : Editor
{
    private struct Triangle
    {
        public Vector3 a, b, c, normal;
        public float cumulativeArea;
        public bool foliage;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Assign crown meshes, their green foliage material and an apple template. Distribution runs only when you press the button. Ctrl+Z undoes it.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Aepfel verteilen / neu verteilen"))
                Generate((AppleDistributor)target);
        }
    }

    private static void Generate(AppleDistributor settings)
    {
        if (settings.greenAppleTemplate == null || settings.foliageMaterial == null
            || settings.crownMeshes == null || settings.crownMeshes.Length == 0
            || settings.localStemDirection.sqrMagnitude < 0.0001f)
        {
            Debug.LogError("Assign crown meshes, foliage material, green apple and a non-zero stem direction.", settings);
            return;
        }

        var triangles = new List<Triangle>();
        var candidates = new List<Triangle>();
        var seen = new HashSet<MeshFilter>();
        float area = 0f;
        try
        {
            foreach (MeshFilter filter in settings.crownMeshes)
            {
                if (filter == null || filter.sharedMesh == null || !seen.Add(filter)) continue;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                Material[] materials = renderer.sharedMaterials;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    bool foliage = sub < materials.Length && materials[sub] == settings.foliageMaterial;
                    int[] indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = matrix.MultiplyPoint3x4(vertices[indices[i]]);
                        Vector3 b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]);
                        Vector3 c = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                        Vector3 cross = Vector3.Cross(b - a, c - a);
                        if (cross.sqrMagnitude < 1e-12f) continue;
                        Vector3 localNormal = Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]);
                        var t = new Triangle { a = a, b = b, c = c,
                            normal = matrix.inverse.transpose.MultiplyVector(localNormal).normalized, foliage = foliage };
                        triangles.Add(t);
                        if (!foliage) continue;
                        area += cross.magnitude * 0.5f;
                        t.cumulativeArea = area;
                        candidates.Add(t);
                    }
                }
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogError("Cannot read crown mesh. Enable Read/Write in its model import settings. " + exception.Message, settings);
            return;
        }
        if (candidates.Count == 0)
        {
            Debug.LogError("No crown triangles use the selected foliage material. Nothing changed.", settings);
            return;
        }

        var random = new System.Random(settings.randomSeed);
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        int count = Mathf.Clamp(settings.appleCount, 1, 500);
        for (int attempt = 0; attempt < count * 100 && positions.Count < count; attempt++)
        {
            float pick = (float)random.NextDouble() * area;
            int low = 0, high = candidates.Count - 1;
            while (low < high) { int mid = (low + high) / 2; if (candidates[mid].cumulativeArea < pick) low = mid + 1; else high = mid; }
            Triangle t = candidates[low];
            // Hanging fruit on the top of a crown would disappear into the leaves.
            if (Vector3.Dot(t.normal, Vector3.up) > 0.2f) continue;
            float u = Mathf.Sqrt((float)random.NextDouble()), v = (float)random.NextDouble();
            Vector3 point = (1f - u) * t.a + u * (1f - v) * t.b + u * v * t.c;
            // Reject inward seams and surfaces hidden by another crown sphere.
            bool blocked = false;
            foreach (Triangle other in triangles)
                if (RayHits(point + t.normal * 0.002f, t.normal, other)) { blocked = true; break; }
            if (blocked) continue;
            foreach (Vector3 previous in positions)
                if ((previous - point).sqrMagnitude < settings.minimumSpacing * settings.minimumSpacing) { blocked = true; break; }
            if (blocked) continue;
            positions.Add(point);
            normals.Add(t.normal);
        }
        if (positions.Count == 0)
        {
            Debug.LogWarning("No accessible crown positions found. Check mesh normals and spacing. Nothing changed.", settings);
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Distribute apples");
        Undo.RecordObject(settings, "Update apple group");
        // Delete only the group explicitly owned by this distributor.
        if (settings.generatedApples != null && settings.generatedApples.IsChildOf(settings.transform))
            Undo.DestroyObjectImmediate(settings.generatedApples.gameObject);
        var container = new GameObject("Generated Apples");
        Undo.RegisterCreatedObjectUndo(container, "Create apple group");
        container.transform.SetParent(settings.transform, true);
        settings.generatedApples = container.transform;

        for (int i = 0; i < positions.Count; i++)
        {
            var holder = new GameObject("AppleHolder_" + (i + 1).ToString("D2"));
            Undo.RegisterCreatedObjectUndo(holder, "Create apple holder");
            holder.transform.SetParent(container.transform, true);
            holder.transform.position = positions[i] + normals[i] * settings.surfaceOffset;
            // Stem points mostly upward, slightly toward the crown; fruit hangs below it.
            float angle = settings.tiltDegrees * Mathf.Lerp(0.4f, 1f, (float)random.NextDouble());
            Vector3 inward = Vector3.ProjectOnPlane(-normals[i], Vector3.up).normalized;
            Vector3 direction = Vector3.up * Mathf.Cos(angle * Mathf.Deg2Rad)
                + inward * Mathf.Sin(angle * Mathf.Deg2Rad);
            holder.transform.rotation = Quaternion.FromToRotation(settings.localStemDirection.normalized, direction);
            GameObject green = CopyApple(settings.greenAppleTemplate, holder.transform, settings.appleScale, settings.localStemDirection, "Green Apple");
            ExposeFruit(holder.transform, green, normals[i], triangles);
            if (settings.redAppleTemplate != null)
            {
                GameObject red = CopyApple(settings.redAppleTemplate, holder.transform, settings.appleScale, settings.localStemDirection, "Red Apple");
                red.SetActive(false);
                AppleGrowth growth = Undo.AddComponent<AppleGrowth>(holder);
                var serialized = new SerializedObject(growth);
                serialized.FindProperty("greenApple").objectReferenceValue = green;
                serialized.FindProperty("redApple").objectReferenceValue = red;
                serialized.ApplyModifiedProperties();
            }
        }
        EditorUtility.SetDirty(settings);
        Undo.CollapseUndoOperations(group);
        Debug.Log("Placed " + positions.Count + " of " + count + " apples. If fewer fit, reduce Minimum Spacing. Original templates remain unchanged.", settings);
    }

    private static GameObject CopyApple(GameObject template, Transform parent, float scale, Vector3 stemDirection, string name)
    {
        // This visual root is also the growth pivot: scaling keeps the stem attached.
        var visualRoot = new GameObject(name);
        visualRoot.transform.SetParent(parent, false);
        GameObject copy = Object.Instantiate(template, visualRoot.transform);
        copy.name = "Model";
        copy.SetActive(true);
        copy.transform.localPosition = Vector3.zero;
        // Preserve the upright template pose, including Blender import and parent rotation.
        // Resetting this to identity can turn the stem sideways before hanging is applied.
        copy.transform.localRotation = template.transform.rotation;
        copy.transform.localScale = template.transform.lossyScale * Mathf.Max(.001f, scale);
        Vector3 axis = stemDirection.normalized;
        var points = new List<Vector3>();
        float highest = float.NegativeInfinity;
        foreach (MeshFilter meshFilter in copy.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null) continue;
            foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
            {
                Vector3 point = visualRoot.transform.InverseTransformPoint(meshFilter.transform.TransformPoint(vertex));
                points.Add(point);
                highest = Mathf.Max(highest, Vector3.Dot(point, axis));
            }
        }
        Vector3 tip = Vector3.zero;
        int tipCount = 0;
        foreach (Vector3 point in points)
            if (highest - Vector3.Dot(point, axis) <= 0.0005f) { tip += point; tipCount++; }
        if (tipCount > 0) copy.transform.localPosition -= tip / tipCount;
        Undo.RegisterCreatedObjectUndo(visualRoot, "Create apple");
        return visualRoot;
    }

    private static void ExposeFruit(Transform holder, GameObject fruit, Vector3 outward, List<Triangle> crown)
    {
        Renderer[] renderers = fruit.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

        // The stem can be exposed while the hanging fruit sits inside a lower sphere.
        // Move its centre to the outermost crown surface along the placement normal.
        float exitDistance = 0f;
        foreach (Triangle triangle in crown)
        {
            if (!triangle.foliage) continue;
            float distance = RayDistance(bounds.center, outward, triangle);
            if (distance > exitDistance) exitDistance = distance;
        }
        if (exitDistance > 0f)
            holder.position += outward * (exitDistance + 0.005f);
    }

    private static bool RayHits(Vector3 origin, Vector3 direction, Triangle t)
    {
        return RayDistance(origin, direction, t) > .001f;
    }

    private static float RayDistance(Vector3 origin, Vector3 direction, Triangle t)
    {
        Vector3 edge1 = t.b - t.a, edge2 = t.c - t.a;
        Vector3 p = Vector3.Cross(direction, edge2);
        float determinant = Vector3.Dot(edge1, p);
        if (Mathf.Abs(determinant) < 1e-8f) return -1f;
        float inverse = 1f / determinant;
        Vector3 s = origin - t.a;
        float u = Vector3.Dot(s, p) * inverse;
        if (u < 0f || u > 1f) return -1f;
        Vector3 q = Vector3.Cross(s, edge1);
        float v = Vector3.Dot(direction, q) * inverse;
        return v >= 0f && u + v <= 1f ? Vector3.Dot(edge2, q) * inverse : -1f;
    }
}
