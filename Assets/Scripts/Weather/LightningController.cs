using System.Collections;
using UnityEngine;

public class LightningController : MonoBehaviour
{
    [Header("Lightning Light")]
    [SerializeField] private Light lightningLight;

    [Header("Lightning Bolt")]
    [SerializeField] private LineRenderer lightningBolt;

    [SerializeField] private int boltPoints = 10;
    [SerializeField] private float boltLength = 15f;
    [SerializeField] private float boltJitter = 1.2f;

    [Header("Bolt Height")]
    [SerializeField] private float boltHeight = 40f;

    [Header("Lightning Distance")]
    [SerializeField] private float closeMinDistance = 10f;
    [SerializeField] private float closeMaxDistance = 25f;

    [SerializeField] private float mediumMinDistance = 30f;
    [SerializeField] private float mediumMaxDistance = 60f;

    [SerializeField] private float farMinDistance = 70f;
    [SerializeField] private float farMaxDistance = 120f;

    [Header("Timing")]
    [SerializeField] private float minTimeBetweenLightning = 5f;
    [SerializeField] private float maxTimeBetweenLightning = 20f;

    [Header("Thunder")]
    [SerializeField] private AudioSource thunderAudio;

    // 0-1 = Close
    // 2-4 = Medium
    // 5-7 = Far
    [SerializeField] private AudioClip[] thunderClips;

    private Coroutine lightningRoutine;

    private enum StormDistance
    {
        Close,
        Medium,
        Far
    }

    private void OnEnable()
    {
        if (lightningLight != null)
            lightningLight.intensity = 0f;

        if (lightningBolt != null)
            lightningBolt.enabled = false;

        lightningRoutine = StartCoroutine(LightningRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        lightningRoutine = null;

        if (lightningLight != null)
            lightningLight.intensity = 0f;

        if (lightningBolt != null)
            lightningBolt.enabled = false;

        if (thunderAudio != null)
            thunderAudio.Stop();
    }

    private IEnumerator LightningRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(
                minTimeBetweenLightning,
                maxTimeBetweenLightning
            );

            yield return new WaitForSeconds(waitTime);

            StormDistance distance = ChooseStormDistance();

            yield return StartCoroutine(
                StormEvent(distance)
            );
        }
    }

    private StormDistance ChooseStormDistance()
    {
        float randomValue = Random.value;

        // 15 % Close
        if (randomValue < 0.15f)
            return StormDistance.Close;

        // 35 % Medium
        if (randomValue < 0.50f)
            return StormDistance.Medium;

        // 50 % Far
        return StormDistance.Far;
    }

    private IEnumerator StormEvent(StormDistance distance)
    {
        float lightIntensity;
        float thunderDelay;
        float minDistance;
        float maxDistance;

        AudioClip thunderClip;

        switch (distance)
        {
            case StormDistance.Close:

                lightIntensity = Random.Range(3.5f, 5f);

                thunderDelay = Random.Range(
                    0.3f,
                    1.5f
                );

                minDistance = closeMinDistance;
                maxDistance = closeMaxDistance;

                thunderClip = GetRandomClip(0, 2);

                break;


            case StormDistance.Medium:

                lightIntensity = Random.Range(2f, 3.5f);

                thunderDelay = Random.Range(
                    2f,
                    4f
                );

                minDistance = mediumMinDistance;
                maxDistance = mediumMaxDistance;

                thunderClip = GetRandomClip(2, 5);

                break;


            case StormDistance.Far:

                lightIntensity = Random.Range(0.8f, 2f);

                thunderDelay = Random.Range(
                    4f,
                    7f
                );

                minDistance = farMinDistance;
                maxDistance = farMaxDistance;

                thunderClip = GetRandomClip(5, 8);

                break;


            default:
                yield break;
        }


        // ---------------------------------
        // ZUFÄLLIGE BLITZPOSITION BESTIMMEN
        // ---------------------------------

        Vector3 boltPosition =
            GetRandomBoltPosition(
                minDistance,
                maxDistance
            );

        lightningBolt.transform.position =
            boltPosition;


        // ---------------------------------
        // BLITZLICHT AUF BLITZ AUSRICHTEN
        // ---------------------------------

        if (lightningLight != null)
        {
            Vector3 direction =
                boltPosition - transform.position;

            lightningLight.transform.rotation =
                Quaternion.LookRotation(direction);
        }


        // Neue Blitzform erzeugen
        GenerateBolt();


        // ---------------------------------
        // ERSTER BLITZ
        // ---------------------------------

        if (lightningLight != null)
            lightningLight.intensity = lightIntensity;

        if (lightningBolt != null)
            lightningBolt.enabled = true;

        yield return new WaitForSeconds(0.06f);


        // ---------------------------------
        // KURZ DUNKEL
        // ---------------------------------

        if (lightningLight != null)
            lightningLight.intensity = 0f;

        if (lightningBolt != null)
            lightningBolt.enabled = false;

        yield return new WaitForSeconds(0.08f);


        // ---------------------------------
        // ZWEITER BLITZ
        // ---------------------------------

        GenerateBolt();

        if (lightningLight != null)
        {
            lightningLight.intensity =
                lightIntensity *
                Random.Range(0.8f, 1.2f);
        }

        if (lightningBolt != null)
            lightningBolt.enabled = true;

        yield return new WaitForSeconds(0.10f);


        // ---------------------------------
        // BLITZ AUS
        // ---------------------------------

        if (lightningLight != null)
            lightningLight.intensity = 0f;

        if (lightningBolt != null)
            lightningBolt.enabled = false;


        // ---------------------------------
        // DONNER VERZÖGERUNG
        // ---------------------------------

        yield return new WaitForSeconds(
            thunderDelay
        );


        // ---------------------------------
        // DONNER
        // ---------------------------------

        if (thunderAudio != null &&
            thunderClip != null)
        {
            thunderAudio.PlayOneShot(
                thunderClip
            );
        }
    }


    // =================================================
    // ZUFÄLLIGE POSITION RUND UM DAS SCHLOSS
    // =================================================

    private Vector3 GetRandomBoltPosition(
        float minDistance,
        float maxDistance)
    {
        // Zufälliger Winkel um das Schloss
        float angle =
            Random.Range(0f, 360f) *
            Mathf.Deg2Rad;

        // Zufällige Entfernung
        float distance =
            Random.Range(
                minDistance,
                maxDistance
            );

        float x =
            Mathf.Cos(angle) * distance;

        float z =
            Mathf.Sin(angle) * distance;

        return new Vector3(
            transform.position.x + x,
            boltHeight,
            transform.position.z + z
        );
    }


    // =================================================
    // BLITZFORM
    // =================================================

    private void GenerateBolt()
    {
        if (lightningBolt == null)
            return;

        lightningBolt.positionCount =
            boltPoints;

        Vector3 startPosition =
            lightningBolt.transform.position;

        float step =
            boltLength /
            (boltPoints - 1);

        for (int i = 0; i < boltPoints; i++)
        {
            float y =
                startPosition.y -
                (step * i);

            float x =
                startPosition.x +
                Random.Range(
                    -boltJitter,
                    boltJitter
                );

            float z =
                startPosition.z +
                Random.Range(
                    -boltJitter,
                    boltJitter
                );

            // Oberster Punkt bleibt stabil
            if (i == 0)
            {
                x = startPosition.x;
                z = startPosition.z;
            }

            lightningBolt.SetPosition(
                i,
                new Vector3(
                    x,
                    y,
                    z
                )
            );
        }
    }


    // =================================================
    // DONNERCLIP AUSWÄHLEN
    // =================================================

    private AudioClip GetRandomClip(
        int minIndex,
        int maxIndex)
    {
        if (thunderClips == null ||
            thunderClips.Length == 0 ||
            minIndex >= thunderClips.Length)
        {
            return null;
        }

        maxIndex =
            Mathf.Min(
                maxIndex,
                thunderClips.Length
            );

        int randomIndex =
            Random.Range(
                minIndex,
                maxIndex
            );

        return thunderClips[randomIndex];
    }
}