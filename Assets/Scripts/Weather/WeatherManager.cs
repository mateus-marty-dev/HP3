using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    public enum WeatherType
    {
        Sunny,
        Cloudy,
        Rainy,
        Stormy
    }

    [Header("Current Weather")]
    public WeatherType currentWeather = WeatherType.Sunny;

    [Header("Scene References")]
    public Light sun;
    public ParticleSystem rain;
    public GameObject lightning;

    [Header("Rain Audio")]
    public AudioSource rainOutdoor;
    public AudioSource rainIndoor;

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

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            currentWeather = WeatherType.Stormy;
            ApplyWeather();
        }
    }

    private void ApplyWeather()
    {
        // -------------------------
        // Erst alles zurücksetzen
        // -------------------------

        rain.Stop();

        rainOutdoor.Stop();
        rainIndoor.Stop();

        lightning.SetActive(false);


        // -------------------------
        // Gewünschtes Wetter setzen
        // -------------------------

        switch (currentWeather)
        {
            // 1 - SONNE
            case WeatherType.Sunny:

                RenderSettings.skybox = sunnySkybox;
                sun.intensity = 2f;

                break;


            // 2 - BEWÖLKT
            case WeatherType.Cloudy:

                RenderSettings.skybox = cloudySkybox;
                sun.intensity = 0.7f;

                break;


            // 3 - REGEN
            case WeatherType.Rainy:

                RenderSettings.skybox = cloudySkybox;
                sun.intensity = 0.4f;

                rain.Play();

                StartRainAudio();

                break;


            // 4 - GEWITTER
            case WeatherType.Stormy:

                RenderSettings.skybox = cloudySkybox;
                sun.intensity = 0.4f;

                rain.Play();

                StartRainAudio();

                lightning.SetActive(true);

                break;
        }

        DynamicGI.UpdateEnvironment();
    }


    private void StartRainAudio()
    {
        // Beide starten gleichzeitig.
        // Die IndoorRainZone regelt anschließend,
        // welches davon hörbar ist.

        if (!rainOutdoor.isPlaying)
        {
            rainOutdoor.Play();
        }

        if (!rainIndoor.isPlaying)
        {
            rainIndoor.Play();
        }
    }
}