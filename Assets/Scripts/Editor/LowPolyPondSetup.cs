using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Creates persistent water assets without modifying existing scene objects.</summary>
public static class LowPolyPondSetup
{
    const string Folder = "Assets/Art/LowPolyWater";
    const string Request = "Temp/LowPolyPondSetup.request";

    [InitializeOnLoadMethod]
    static void Register() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try
        {
            CreateAssets();
            File.WriteAllText("Temp/LowPolyPondSetup.result.txt", "OK: " + Folder + "/LowPolyPond.prefab");
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/LowPolyPondSetup.result.txt", "FAILED: " + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Hotzenplotz/Low-Poly-Teich/Assets erstellen")]
    public static void CreateAssets()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        Shader shader = Shader.Find("Hotzenplotz/Low Poly Pond");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Low Poly Pond shader is missing or has compilation errors.");

        string meshPath = Folder + "/PondSurface.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = BuildMesh();
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        string materialPath = Folder + "/Teichwasser.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Teichwasser" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        string prefabPath = Folder + "/LowPolyPond.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            GameObject water = new GameObject("LowPolyPond");
            try
            {
                water.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = water.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(water, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(water); }
        }
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
    }

    static Mesh BuildMesh()
    {
        // Shared grid positions, duplicated triangle vertices for individual facet colors.
        const int cells = 32;
        const float size = 20f;
        var random = new System.Random(431);
        var grid = new Vector3[cells + 1, cells + 1];
        for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                float jitterX = x == 0 || x == cells ? 0 : ((float)random.NextDouble() - 0.5f) * 0.35f;
                float jitterZ = z == 0 || z == cells ? 0 : ((float)random.NextDouble() - 0.5f) * 0.35f;
                grid[x,z] = new Vector3((x + jitterX) * size / cells - size / 2, 0,
                    (z + jitterZ) * size / cells - size / 2);
            }
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();
        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            float shade = (float)random.NextDouble();
            foreach (Vector3 point in new[] { a, b, c })
            {
                indices.Add(vertices.Count);
                vertices.Add(point);
                colors.Add(new Color(shade, shade, shade, 1));
            }
        }
        for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                Vector3 a = grid[x,z], b = grid[x + 1,z], c = grid[x,z + 1], d = grid[x + 1,z + 1];
                if ((x + z) % 2 == 0) { Triangle(a,c,b); Triangle(b,c,d); }
                else { Triangle(a,c,d); Triangle(a,d,b); }
            }
        var mesh = new Mesh { name = "Low Poly Pond Surface" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        // Include maximum shader displacement so waves are not culled at the screen edges.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 2, size));
        return mesh;
    }
}
