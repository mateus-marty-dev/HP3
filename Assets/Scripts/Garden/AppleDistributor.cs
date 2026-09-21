using UnityEngine;

public class AppleDistributor : MonoBehaviour
{
    [Header("Only assign the crown meshes of ONE tree")]
    public MeshFilter[] crownMeshes;
    [Tooltip("Only triangles using this green foliage material receive apples. Trunk faces are excluded.")]
    public Material foliageMaterial;
    public GameObject greenAppleTemplate;
    [Tooltip("Optional: creates AppleGrowth holders with matching red apples.")]
    public GameObject redAppleTemplate;

    [Header("Distribution (world metres)")]
    [Min(1)] public int appleCount = 30;
    [Min(0.01f)] public float minimumSpacing = 0.3f;
    public int randomSeed = 42;
    [Min(0.001f)] public float appleScale = 1f;
    [Tooltip("0 attaches the stem tip to the crown surface. Positive values move the attachment outward.")]
    public float surfaceOffset = 0f;
    [Range(0f, 45f)] public float tiltDegrees = 15f;
    [Tooltip("Stem direction in the upright template pose (including its rotation). Usually world Y / (0,1,0). Position the template upright before distributing.")]
    public Vector3 localStemDirection = Vector3.up;
    [HideInInspector] public Transform generatedApples;
}
