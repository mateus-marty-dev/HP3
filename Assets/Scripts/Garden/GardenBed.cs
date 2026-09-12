using UnityEngine;

/// <summary>Shared soil moisture for all plants assigned to this bed.</summary>
public class GardenBed : Interactable
{
    [Header("Stored soil water (0 = dry)")]
    [Min(0.1f)] [SerializeField] private float maxMoisture = 3f;
    [Min(0f)] [SerializeField] private float moisture = 0f;
    [Min(0f)] [SerializeField] private float dryingPerSecond = 0.005f;
    [Range(0.01f, 1f)] [SerializeField] private float minimumGrowthMoisture = 0.1f;

    [Header("Prototype watering with E (no bucket required)")]
    [Range(0.01f, 1f)] [SerializeField] private float waterPerInteraction = 0.5f;

    [Header("Rain watering")]
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private bool receivesRain = true;
    [Min(0f)] [SerializeField] private float rainWaterPerSecond = 0.05f;

    public float Moisture => moisture;
    public bool CanGrow => isActiveAndEnabled && moisture > 0f && moisture >= minimumGrowthMoisture;

    private void Reset()
    {
        interactionText = "E - Beet bewaessern";
    }

    private void Start()
    {
        if (weatherManager == null)
            weatherManager = FindFirstObjectByType<WeatherManager>();
    }

    private void Update()
    {
        moisture = Mathf.Clamp(moisture - dryingPerSecond * Time.deltaTime, 0f, maxMoisture);

        if (receivesRain && weatherManager != null
            && weatherManager.currentWeather == WeatherManager.WeatherType.Rainy)
        {
            AddWater(rainWaterPerSecond * Time.deltaTime);
        }
    }

    private void OnValidate()
    {
        maxMoisture = Mathf.Max(0.1f, maxMoisture);
        moisture = Mathf.Clamp(moisture, 0f, maxMoisture);
    }

    public override void Interact()
    {
        AddWater(waterPerInteraction);
    }

    // Shared entry point for interaction, rain and water actually poured from a bucket.
    public void AddWater(float amount)
    {
        if (amount > 0f)
            moisture = Mathf.Clamp(moisture + amount, 0f, maxMoisture);
    }
}
