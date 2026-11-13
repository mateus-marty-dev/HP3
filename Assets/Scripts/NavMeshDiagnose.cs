
using UnityEngine;
using UnityEngine.AI;

public static class NavMeshDiagnose
{
    [UnityEditor.MenuItem("Tools/HP3/NavMesh pruefen")]
    public static void CheckNavMesh()
    {
        NavMeshTriangulation mesh =
            NavMesh.CalculateTriangulation();

        Debug.Log(
            $"NavMesh: {mesh.indices.Length / 3} Dreiecke"
        );
    }
}
