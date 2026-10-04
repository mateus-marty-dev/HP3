using UnityEngine;

public class WorldStateManager : MonoBehaviour
{
    [Header("World State")]
    [Range(-100f, 100f)]
    [SerializeField] private float worldState = 0f;

    [Header("References")]
    [SerializeField] private WeatherManager weatherManager;

    public float WorldState => worldState;

    private void Start()
    {
        UpdateWorld();
    }

    private void Update()
    {
        // Nur zum Testen
        if (Input.GetKeyDown(KeyCode.J))
        {
            ChangeWorldState(-10f);
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            ChangeWorldState(10f);
        }
    }

    public void ChangeWorldState(float amount)
    {
        worldState = Mathf.Clamp(worldState + amount, -100f, 100f);

        Debug.Log("World State: " + worldState);

        UpdateWorld();
    }

    private void UpdateWorld()
    {
        if (weatherManager == null)
            return;

        if (worldState <= -70f)
        {
            weatherManager.SetWeather(
                WeatherManager.WeatherType.Stormy
            );
        }
        else if (worldState <= -40f)
        {
            weatherManager.SetWeather(
                WeatherManager.WeatherType.Rainy
            );
        }
        else if (worldState <= -10f)
        {
            weatherManager.SetWeather(
                WeatherManager.WeatherType.Cloudy
            );
        }
        else
        {
            weatherManager.SetWeather(
                WeatherManager.WeatherType.Sunny
            );
        }
    }
}