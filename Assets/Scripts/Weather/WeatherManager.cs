using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    public enum WeatherType
    {
        Sunny,
        Cloudy,
        Rainy
    }

    [Header("Current Weather")]
    public WeatherType currentWeather = WeatherType.Sunny;

    [Header("Scene References")]
    public Light sun;
    public ParticleSystem rain;

    [Header("Skyboxes")]
    public Material sunnySkybox;
    public Material cloudySkybox;

    private void Start()
    {
        ApplyWeather();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            currentWeather = WeatherType.Sunny;
            ApplyWeather();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            currentWeather = WeatherType.Cloudy;
            ApplyWeather();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            currentWeather = WeatherType.Rainy;
            ApplyWeather();
        }
    }

    private void ApplyWeather()
    {
        switch (currentWeather)
        {
            case WeatherType.Sunny:
                RenderSettings.skybox = sunnySkybox;
                sun.intensity = 2f;
                rain.Stop();
                break;

            case WeatherType.Cloudy:
                RenderSettings.skybox = cloudySkybox;
                sun.intensity = 0.7f;
                rain.Stop();
                break;

            case WeatherType.Rainy:
                RenderSettings.skybox = cloudySkybox;
                sun.intensity = 0.4f;
                rain.Play();
                break;
        }

        DynamicGI.UpdateEnvironment();
    }
}