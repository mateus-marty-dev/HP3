using UnityEngine;

/// <summary>
/// Lässt eine Seerose auf HP3s LowPolyPond.shader schwimmen.
/// Übernimmt exakt die beiden Sinus-Wellen aus dem Vertex-Shader.
/// Die geringe verbleibende Abweichung entsteht durch die Interpolation
/// der tatsächlichen Wasser-Mesh-Dreiecke.
/// </summary>
[DisallowMultipleComponent]
public class HP3_FloatOnPond : MonoBehaviour
{
    [Header("LowPolyPond")]
    [SerializeField] private Renderer pondRenderer;

    [Header("Position")]
    [Tooltip("Zusätzlicher Abstand oberhalb der berechneten Wasseroberfläche (Meter).")]
    [SerializeField] private float surfaceOffset = 0.03f;

    [Header("Neigung")]
    [SerializeField] private bool followWaveTilt = true;
    [Range(0f, 1f)]
    [SerializeField] private float tiltStrength = 0.35f;
    [Min(0f)]
    [SerializeField] private float rotationSmooth = 5f;

    private Quaternion startRotation;
    private Material pondMaterial;

    private void Start()
    {
        startRotation = transform.rotation;

        if (pondRenderer == null)
        {
            Debug.LogWarning($"{name}: Bitte den LowPolyPond MeshRenderer zuweisen.", this);
            enabled = false;
            return;
        }

        pondMaterial = pondRenderer.sharedMaterial;
        if (pondMaterial == null ||
            !pondMaterial.HasProperty("_WaveHeight") ||
            !pondMaterial.HasProperty("_WaveLength") ||
            !pondMaterial.HasProperty("_WaveSpeed"))
        {
            Debug.LogWarning($"{name}: Das Wassermaterial muss den HP3 LowPolyPond-Shader verwenden.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        Vector3 p = transform.position;
        float t = Time.time;

        // Das zugrunde liegende PondSurface-Mesh liegt auf lokal Y=0.
        float baseWaterY = pondRenderer.transform.TransformPoint(Vector3.zero).y;
        p.y = baseWaterY + SampleWave(p.x, p.z, t) + surfaceOffset;
        transform.position = p;

        if (!followWaveTilt)
            return;

        const float step = 0.12f;
        float left = SampleWave(p.x - step, p.z, t);
        float right = SampleWave(p.x + step, p.z, t);
        float back = SampleWave(p.x, p.z - step, t);
        float front = SampleWave(p.x, p.z + step, t);

        Vector3 normal = new Vector3(left - right, 2f * step, back - front).normalized;
        Vector3 gentleNormal = Vector3.Lerp(Vector3.up, normal, tiltStrength).normalized;
        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, gentleNormal) * startRotation;

        float smooth = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smooth);
    }

    private float SampleWave(float x, float z, float time)
    {
        // Spiegelung der Wellenform aus Assets/Art/LowPolyWater/LowPolyPond.shader.
        float height = pondMaterial.GetFloat("_WaveHeight");
        float length = Mathf.Max(pondMaterial.GetFloat("_WaveLength"), 0.1f);
        float speed = pondMaterial.GetFloat("_WaveSpeed");

        float k = 2f * Mathf.PI / length;
        float t = time * speed;

        float first = Mathf.Sin((x + z * 0.37f) * k + t);
        float second = 0.45f * Mathf.Sin((z - x * 0.28f) * k * 1.31f - t * 0.73f);
        return height * (first + second) / 1.45f;
    }
}
