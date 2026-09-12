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

    private float rainTimer = 0f;

    private void Start()
    {
        startLocalPosition = water.localPosition;
        startLocalScale = water.localScale;

        water.gameObject.SetActive(fillLevel > 0f);
    }

    private void Update()
    {
        FillFromRain();
        PourWater();
        UpdateWater();
    }

    private void FillFromRain()
    {
        if (weatherManager.currentWeather == WeatherManager.WeatherType.Rainy)
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
        if (fillLevel <= 0f)
            return;

        float angle = Vector3.Angle(transform.up, Vector3.up);

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
            waterTop.localScale,
            fillLevel
        );
    }
}
