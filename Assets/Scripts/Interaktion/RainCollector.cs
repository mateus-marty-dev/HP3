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

    [Range(0f, 1f)]
    [SerializeField] private float fillLevel = 0f;

    private Vector3 startPosition;
    private Vector3 startScale;

    private float rainTimer = 0f;
    private bool waterVisible = false;

    private void Start()
    {
        startPosition = water.position;
        startScale = water.localScale;

        // Kübel startet ohne sichtbares Wasser
        water.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (weatherManager.currentWeather == WeatherManager.WeatherType.Rainy)
        {
            rainTimer += Time.deltaTime;

            // Nach 5 Sekunden Regen erscheint das Wasser
            if (rainTimer >= waterAppearDelay)
            {
                if (!waterVisible)
                {
                    water.gameObject.SetActive(true);
                    waterVisible = true;
                }

                fillLevel += fillSpeed * Time.deltaTime;
                fillLevel = Mathf.Clamp01(fillLevel);

                UpdateWater();
            }
        }
    }

    private void UpdateWater()
    {
        water.position = Vector3.Lerp(
            startPosition,
            waterTop.position,
            fillLevel
        );

        water.localScale = Vector3.Lerp(
            startScale,
            waterTop.localScale,
            fillLevel
        );
    }
}