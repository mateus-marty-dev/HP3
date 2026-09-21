using System.Collections;
using UnityEngine;

public class IndoorRainZone : MonoBehaviour
{
    [Header("Rain Audio")]
    [SerializeField] private AudioSource outdoorRain;
    [SerializeField] private AudioSource indoorRain;

    [Header("Volume")]
    [SerializeField] private float outdoorVolume = 0.5f;
    [SerializeField] private float indoorVolume = 0.5f;

    [Header("Transition")]
    [SerializeField] private float fadeDuration = 1.5f;

    private Coroutine fadeCoroutine;

    private void Start()
    {
        outdoorRain.volume = outdoorVolume;
        indoorRain.volume = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        StartFade(0f, indoorVolume);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        StartFade(outdoorVolume, 0f);
    }

    private void StartFade(float outdoorTarget, float indoorTarget)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(
            FadeRain(outdoorTarget, indoorTarget)
        );
    }

    private IEnumerator FadeRain(
        float outdoorTarget,
        float indoorTarget)
    {
        float startOutdoor = outdoorRain.volume;
        float startIndoor = indoorRain.volume;

        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;

            float t = time / fadeDuration;

            outdoorRain.volume =
                Mathf.Lerp(startOutdoor, outdoorTarget, t);

            indoorRain.volume =
                Mathf.Lerp(startIndoor, indoorTarget, t);

            yield return null;
        }

        outdoorRain.volume = outdoorTarget;
        indoorRain.volume = indoorTarget;
    }
}