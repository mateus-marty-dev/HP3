using UnityEngine;

/// <summary>
/// Ein Wasserkoerper fuer HP.3 (Unity 6 / URP).
/// Auf dasselbe GameObject wie einen BoxCollider mit Is Trigger legen.
/// Die sichtbare Wasseroberflaeche (LowPolyPond) kann ein anderes Objekt sein.
///
/// Das Script fragt die exakte mathematische Wellenfunktion aus
/// Assets/Art/LowPolyWater/LowPolyPond.shader ab; das sichtbare Dreiecksnetz
/// interpoliert zwischen seinen Vertices und kann minimal abweichen.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class HP3_WaterBody : MonoBehaviour
{
    [Header("Sichtbare Wasseroberflaeche")]
    [Tooltip("MeshRenderer von LowPolyPond (NICHT Water_Rose).")]
    [SerializeField] private Renderer pondRenderer;

    [Header("Eigenschaften des Wassers")]
    [Min(0f)]
    [Tooltip("Wasserdichte in kg/m³. Erst relevant, wenn Auftrieb eingeschaltet wird.")]
    [SerializeField] private float density = 1000f;

    [Tooltip("Falls kein passender Wellenshader vorhanden ist: Hoehe ohne Wellen verwenden.")]
    [SerializeField] private bool useShaderWaves = true;

    private static readonly System.Collections.Generic.HashSet<HP3_WaterBody> activeBodies =
        new System.Collections.Generic.HashSet<HP3_WaterBody>();

    private void OnEnable() => activeBodies.Add(this);
    private void OnDisable() => activeBodies.Remove(this);

    public static bool IsPointUnderwater(Vector3 point)
    {
        foreach (HP3_WaterBody body in activeBodies)
        {
            if (body == null || !body.isActiveAndEnabled) continue;
            BoxCollider zone = body.GetComponent<BoxCollider>();
            if (zone == null || !zone.enabled) continue;
            Vector3 local = zone.transform.InverseTransformPoint(point) - zone.center;
            Vector3 half = zone.size * 0.5f;
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y ||
                Mathf.Abs(local.z) > half.z) continue;
            if (point.y < body.GetSurfaceY(point, Time.time)) return true;
        }
        return false;
    }
    public float Density => density;
    public Renderer PondRenderer => pondRenderer;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        if (pondRenderer == null)
            Debug.LogWarning($"{name}: LowPolyPond MeshRenderer in 'Pond Renderer' zuweisen.", this);
    }

    /// <summary>
    /// Wasserhoehe an einer Weltposition. Geht von waagrechtem Teichwasser aus.
    /// </summary>
    public float GetSurfaceY(float worldX, float worldZ, float time)
    {
        // Das PondSurface-Mesh liegt im Asset auf lokal y=0.
        float surfaceY = pondRenderer != null
            ? pondRenderer.transform.TransformPoint(Vector3.zero).y
            : transform.position.y;

        if (!useShaderWaves || pondRenderer == null)
            return surfaceY;

        Material mat = pondRenderer.sharedMaterial;
        if (mat == null ||
            !mat.HasProperty("_WaveHeight") ||
            !mat.HasProperty("_WaveLength") ||
            !mat.HasProperty("_WaveSpeed"))
            return surfaceY;

        float amplitude = mat.GetFloat("_WaveHeight");
        float wavelength = Mathf.Max(mat.GetFloat("_WaveLength"), 0.1f);
        float speed = mat.GetFloat("_WaveSpeed");
        float k = 2f * Mathf.PI / wavelength;
        float t = time * speed;

        // Gleiches Wellenmodell wie LowPolyPond.shader (Weltkoordinaten X/Z).
        float first = Mathf.Sin((worldX + worldZ * 0.37f) * k + t);
        float second = 0.45f * Mathf.Sin(
            (worldZ - worldX * 0.28f) * k * 1.31f - t * 0.73f);
        return surfaceY + amplitude * (first + second) / 1.45f;
    }

    public float GetSurfaceY(Vector3 worldPosition, float time)
    {
        return GetSurfaceY(worldPosition.x, worldPosition.z, time);
    }

    // Anstoss fuer die Bewegungsphysik: die Triggerzone selbst hat keinen Rigidbody.
    // Es werden nur reale Objekt-Collider registriert, nicht andere Trigger.
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Wasserkontakt: " + other.name, this);
        Register(other);
    }
    private void OnTriggerStay(Collider other) => Register(other);

    private void Register(Collider other)
    {
        if (other.isTrigger || other.attachedRigidbody == null) return;

        HP3_WaterPhysics physics =
            other.attachedRigidbody.GetComponent<HP3_WaterPhysics>();
        if (physics != null && physics.isActiveAndEnabled)
            physics.RegisterWaterContact(this, other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody == null) return;
        HP3_WaterPhysics physics =
            other.attachedRigidbody.GetComponent<HP3_WaterPhysics>();
        if (physics != null)
            physics.UnregisterWaterContact(this, other);
    }
}
