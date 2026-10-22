using UnityEngine;

public class RainCollector : MonoBehaviour
{
    [SerializeField] private WeatherManager weatherManager;

    [Header("Water")]
    [SerializeField] private Transform water;
    [SerializeField] private Transform waterTop;

    [Header("Filling")]
    [SerializeField] private float fillSpeed = 0.05f;
    [SerializeField] private float waterAppearDelay = 5f;

    [Header("Pond filling and water weight")]
    [Min(0.1f)] [SerializeField] private float capacityLitres = 10f;
    [Min(0f)] [SerializeField] private float pondFillSpeed = 0.35f;

    [Header("Pouring")]
    [SerializeField] private float pourAngle = 55f;
    [SerializeField] private float pourSpeed = 0.25f;

    [Header("Watering nearby garden beds")]
    [Tooltip("Optional bucket rim position. Uses Water Top when unassigned.")]
    [SerializeField] private Transform pourPoint;
    [Min(0f)] [SerializeField] private float wateringDistance = 1.5f;
    [Min(0f)] [SerializeField] private float moisturePerFullBucket = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float fillLevel = 0f;

    private Vector3 startLocalPosition;
    private Vector3 startLocalScale;
    private Vector3 localContainerUp = Vector3.up;

    private float rainTimer = 0f;
    private Rigidbody containerBody;
    private float emptyMass;
    private bool rimUnderwater;
    public bool HasWater => fillLevel > 0.001f;
    public Renderer WaterSurfaceRenderer => water != null ? water.GetComponent<Renderer>() : null;

    private void Awake()
    {
        containerBody = GetComponent<Rigidbody>();
        if (containerBody != null) emptyMass = containerBody.mass;
        // Imported vessels may have a rotated root. Cache their actual filling
        // axis before UpdateWater moves the lower surface toward the rim.
        if (water != null && waterTop != null)
        {
            Vector3 fillingAxis = SurfaceCenter(waterTop) - SurfaceCenter(water);
            if (fillingAxis.sqrMagnitude > 0.000001f)
                localContainerUp = transform.InverseTransformVector(fillingAxis).normalized;
        }
        BucketContents contents = GetComponent<BucketContents>();
        if (contents == null) contents = gameObject.AddComponent<BucketContents>();
        contents.Configure(water, waterTop, pourAngle);
    }

    private static Vector3 SurfaceCenter(Transform surface)
    {
        MeshFilter mesh = surface.GetComponent<MeshFilter>();
        return mesh != null && mesh.sharedMesh != null
            ? surface.TransformPoint(mesh.sharedMesh.bounds.center)
            : surface.position;
    }

    private void Start()
    {
        if (water == null || waterTop == null)
        {
            Debug.LogWarning("RainCollector needs Water and Water Top assigned.", this);
            enabled = false;
            return;
        }
        startLocalPosition = water.localPosition;
        startLocalScale = NonZeroScale(water.localScale);

        water.gameObject.SetActive(fillLevel > 0f);
    }

    private void Update()
    {
        FillFromPond();
        FillFromRain();
        PourWater();
        UpdateWater();
    }

    private void FixedUpdate()
    {
        if (containerBody != null)
            containerBody.mass = emptyMass + fillLevel * capacityLitres;
    }

    private void OnDisable()
    {
        if (containerBody != null) containerBody.mass = emptyMass;
    }

    private bool OpeningFacesUp =>
        Vector3.Dot(transform.TransformVector(localContainerUp), Vector3.up) > 0f;

    private void FillFromPond()
    {
        rimUnderwater = false;
        // An inverted bucket traps air instead of collecting water.
        if (!OpeningFacesUp) return;
        Vector3 center = SurfaceCenter(waterTop);
        // Sample the rim, including its low edge when the bucket is tilted.
        MeshFilter mesh = waterTop.GetComponent<MeshFilter>();
        Vector3 extents = mesh != null && mesh.sharedMesh != null
            ? mesh.sharedMesh.bounds.extents : Vector3.zero;
        Vector3 localCenter = mesh != null && mesh.sharedMesh != null
            ? mesh.sharedMesh.bounds.center : Vector3.zero;
        for (int i = 0; i < 7; i++)
        {
            Vector3 point = center;
            if (i > 0)
            {
                Vector3 offset = Vector3.zero;
                int axis = (i - 1) / 2;
                offset[axis] = extents[axis] * (i % 2 == 1 ? 1f : -1f);
                point = waterTop.TransformPoint(localCenter + offset);
            }
            if (HP3_WaterBody.IsPointUnderwater(point))
            {
                rimUnderwater = true;
                break;
            }
        }
        if (rimUnderwater)
            fillLevel = Mathf.MoveTowards(fillLevel, 1f, pondFillSpeed * Time.deltaTime);
    }

    private void FillFromRain()
    {
        if (OpeningFacesUp && weatherManager != null
            && (weatherManager.currentWeather == WeatherManager.WeatherType.Rainy
                || weatherManager.currentWeather == WeatherManager.WeatherType.Stormy))
        {
            rainTimer += Time.deltaTime;

            if (rainTimer >= waterAppearDelay)
            {
                fillLevel += fillSpeed * Time.deltaTime;
                fillLevel = Mathf.Clamp01(fillLevel);
            }
        }
        else
        {
            rainTimer = 0f;
        }
    }

    private void PourWater()
    {
        if (fillLevel <= 0f || rimUnderwater)
            return;

        float angle = Vector3.Angle(transform.TransformVector(localContainerUp), Vector3.up);

        if (angle >= pourAngle)
        {
            // Transfer only the water actually remaining, including the last partial frame.
            float pouredAmount = Mathf.Min(fillLevel, Mathf.Max(0f, pourSpeed) * Time.deltaTime);
            fillLevel = Mathf.Clamp01(fillLevel - pouredAmount);

            if (pouredAmount > 0f)
                WaterNearbyBed(pouredAmount);
        }
    }

    private void WaterNearbyBed(float pouredAmount)
    {
        Vector3 origin = pourPoint != null ? pourPoint.position
            : waterTop != null ? waterTop.position : transform.position;

        GardenBed nearestBed = null;
        float nearestDistanceSquared = wateringDistance * wateringDistance;

        // Use the collider surface rather than the pivot, so long beds work too.
        foreach (Collider candidate in Physics.OverlapSphere(
            origin, wateringDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            GardenBed bed = candidate.GetComponentInParent<GardenBed>();
            if (bed == null || !bed.isActiveAndEnabled)
                continue;

            float distanceSquared = (candidate.ClosestPoint(origin) - origin).sqrMagnitude;
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestBed = bed;
            }
        }

        if (nearestBed != null)
            nearestBed.AddWater(pouredAmount * moisturePerFullBucket);
    }

    private void UpdateWater()
    {
        water.gameObject.SetActive(fillLevel > 0.001f);

        if (fillLevel <= 0f)
            return;

        water.localPosition = Vector3.Lerp(
            startLocalPosition,
            waterTop.localPosition,
            fillLevel
        );

        water.localScale = Vector3.Lerp(
            startLocalScale,
            NonZeroScale(waterTop.localScale),
            fillLevel
        );
    }

    private static Vector3 NonZeroScale(Vector3 scale)
    {
        // A flattened mesh still needs an invertible transform for correct lighting.
        for (int axis = 0; axis < 3; axis++)
            if (Mathf.Abs(scale[axis]) < 0.000001f) scale[axis] = 0.000001f;
        return scale;
    }
}
