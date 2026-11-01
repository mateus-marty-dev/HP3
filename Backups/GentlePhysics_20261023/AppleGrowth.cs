using UnityEngine;

/// <summary>Attach to an apple holder, keeping the tree itself outside its visual children.</summary>
public class AppleGrowth : MonoBehaviour
{
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private GameObject greenApple;
    [SerializeField] private GameObject redApple;

    [Header("Rain fills the water reserve; sunshine enables growth")]
    [Min(0.1f)] [SerializeField] private float waterCapacity = 1f;
    [Min(0f)] [SerializeField] private float rainWaterPerSecond = 0.1f;
    [Min(0.001f)] [SerializeField] private float waterUsedPerGrowthSecond = 0.01f;

    [Header("Seconds of sunshine with available water")]
    [Min(0.1f)] [SerializeField] private float buddingSeconds = 10f;
    [Min(0.1f)] [SerializeField] private float growingSeconds = 20f;
    [Min(0.1f)] [SerializeField] private float ripeningSeconds = 20f;
    [Range(0.01f, 1f)] [SerializeField] private float initialScaleFraction = 0.05f;

    [Header("Random falling after ripening (seconds)")]
    [SerializeField] private bool fallWhenRipe = true;
    [Min(0f)] [SerializeField] private float minimumFallDelay = 15f;
    [Min(0f)] [SerializeField] private float maximumFallDelay = 90f;
    private float ripeSeconds;
    private float fallDelay;
    private bool hasFallen;

    [Header("Runtime progress")]
    [SerializeField] private float storedWater;
    [SerializeField] private float growthSeconds;
    private Vector3 greenFullScale;

    public bool IsRipe => growthSeconds >= buddingSeconds + growingSeconds + ripeningSeconds;

    private void Awake()
    {
        if (!ValidVisual(greenApple) || !ValidVisual(redApple)
            || greenApple == redApple || greenApple.transform.IsChildOf(redApple.transform)
            || redApple.transform.IsChildOf(greenApple.transform))
        {
            Debug.LogError("AppleGrowth requires separate green and red apple children from the scene, not the entire tree or Project assets.", this);
            enabled = false;
            return;
        }

        greenFullScale = greenApple.transform.localScale;
        storedWater = 0f;
        growthSeconds = 0f;
        ripeSeconds = 0f;
        hasFallen = false;
        fallDelay = Random.Range(Mathf.Max(0f, minimumFallDelay),
            Mathf.Max(minimumFallDelay, maximumFallDelay, 0f));
        greenApple.SetActive(false);
        redApple.SetActive(false);
    }

    private bool ValidVisual(GameObject visual)
    {
        return visual != null && visual != gameObject && visual.transform.IsChildOf(transform);
    }

    private void Start()
    {
        if (weatherManager == null)
            weatherManager = FindFirstObjectByType<WeatherManager>();
    }

    private void Update()
    {
        if (hasFallen) return;
        if (IsRipe)
        {
            ripeSeconds += Time.deltaTime;
            if (fallWhenRipe && ripeSeconds >= fallDelay)
                DropApple();
            return;
        }
        if (weatherManager == null)
            return;

        if (weatherManager.currentWeather == WeatherManager.WeatherType.Rainy)
        {
            storedWater = Mathf.Clamp(storedWater + rainWaterPerSecond * Time.deltaTime, 0f, waterCapacity);
            return;
        }

        if (weatherManager.currentWeather != WeatherManager.WeatherType.Sunny || storedWater <= 0f)
            return;

        // Count only the fraction of this frame for which water is actually available.
        float elapsed = Mathf.Min(Time.deltaTime, storedWater / Mathf.Max(0.001f, waterUsedPerGrowthSecond));
        storedWater = Mathf.Max(0f, storedWater - elapsed * waterUsedPerGrowthSecond);
        growthSeconds = Mathf.Min(growthSeconds + elapsed, buddingSeconds + growingSeconds + ripeningSeconds);

        greenApple.transform.localScale = greenFullScale * Mathf.Lerp(initialScaleFraction, 1f,
            Mathf.Clamp01((growthSeconds - buddingSeconds) / Mathf.Max(0.1f, growingSeconds)));
        greenApple.SetActive(growthSeconds >= buddingSeconds && !IsRipe);
        redApple.SetActive(IsRipe);
    }

    private void DropApple()
    {
        if (redApple == null) return;
        hasFallen = true;
        AppleDistributor distributor = GetComponentInParent<AppleDistributor>();

        // Only the ripe fruit detaches; the holder and tree remain in place.
        redApple.transform.SetParent(null, true);
        Renderer[] renderers = redApple.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(redApple.transform.position, Vector3.one * 0.1f);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        }

        // Use one simple physics shape, avoiding non-convex imported mesh colliders.
        foreach (Collider existing in redApple.GetComponentsInChildren<Collider>())
            existing.enabled = false;
        SphereCollider fruitCollider = redApple.AddComponent<SphereCollider>();
        fruitCollider.center = redApple.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = redApple.transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z), 0.0001f);
        fruitCollider.radius = Mathf.Max(0.005f, Mathf.Min(bounds.extents.x, bounds.extents.y, bounds.extents.z)) / largestScale;

        Rigidbody body = redApple.GetComponent<Rigidbody>();
        if (body == null) body = redApple.AddComponent<Rigidbody>();
        body.mass = 0.15f;
        body.isKinematic = false;
        body.useGravity = true;
        body.constraints = RigidbodyConstraints.None;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Fruit is embedded in foliage. Ignore crown collisions so it can fall free.
        if (distributor != null && distributor.crownMeshes != null)
            foreach (MeshFilter crown in distributor.crownMeshes)
            {
                if (crown == null) continue;
                foreach (Collider crownCollider in crown.GetComponents<Collider>())
                    Physics.IgnoreCollision(fruitCollider, crownCollider);
            }
        body.angularVelocity = Random.insideUnitSphere * 1.5f;
    }
}
