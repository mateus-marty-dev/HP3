using UnityEngine;

/// <summary>A pre-sown plant: invisible germination, then ordered visible stages.</summary>
public class PlantGrowth : MonoBehaviour
{
    [SerializeField] private GardenBed gardenBed;

    [Header("Visual children, ordered from Stage 0 to fully grown")]
    [SerializeField] private GameObject[] growthStages;

    [Header("Seconds of moist soil required")]
    [Min(0.1f)] [SerializeField] private float germinationSeconds = 10f;
    [Min(0.1f)] [SerializeField] private float secondsPerStage = 20f;

    [Header("Stage 0 grows from this fraction to its original size")]
    [Range(0.01f, 1f)] [SerializeField] private float initialScaleFraction = 0.05f;

    private Vector3 stageZeroFullScale;

    [Header("Runtime progress (-1 = germinating)")]
    [SerializeField] private int currentStage = -1;
    [SerializeField] private float moistSecondsInStage;

    public int CurrentStage => currentStage;
    public bool IsFullyGrown => growthStages != null && growthStages.Length > 0
        && currentStage == growthStages.Length - 1
        && (currentStage > 0 || moistSecondsInStage >= Mathf.Max(0.1f, secondsPerStage));

    private void Awake()
    {
        currentStage = -1;
        moistSecondsInStage = 0f;

        if (gardenBed == null || growthStages == null || growthStages.Length == 0)
        {
            Debug.LogError("PlantGrowth needs a GardenBed and at least one visual stage.", this);
            enabled = false;
            return;
        }

        // Only children may be hidden: the plant controller must stay active.
        for (int i = 0; i < growthStages.Length; i++)
        {
            GameObject stage = growthStages[i];
            if (stage == null || stage == gameObject || !stage.transform.IsChildOf(transform))
            {
                Debug.LogError("Each growth stage must be a visual child of the PlantGrowth object.", this);
                enabled = false;
                return;
            }

            for (int j = 0; j < i; j++)
            {
                Transform other = growthStages[j].transform;
                if (stage.transform == other || stage.transform.IsChildOf(other) || other.IsChildOf(stage.transform))
                {
                    Debug.LogError("Growth stages must be separate objects, not nested inside each other.", this);
                    enabled = false;
                    return;
                }
            }
        }

        stageZeroFullScale = growthStages[0].transform.localScale;
        UpdateStageZeroSize();
        ShowCurrentStage();
    }

    private void Update()
    {
        if (gardenBed == null || !gardenBed.CanGrow || IsFullyGrown)
            return;

        moistSecondsInStage += Time.deltaTime;
        while (!IsFullyGrown)
        {
            float duration = Mathf.Max(0.1f, currentStage < 0 ? germinationSeconds : secondsPerStage);
            if (moistSecondsInStage < duration)
                break;

            if (currentStage == growthStages.Length - 1)
                break;

            moistSecondsInStage -= duration;
            currentStage++;
            ShowCurrentStage();
        }

        if (currentStage == 0)
            moistSecondsInStage = Mathf.Min(moistSecondsInStage, Mathf.Max(0.1f, secondsPerStage));

        UpdateStageZeroSize();
    }

    private void UpdateStageZeroSize()
    {
        float progress = currentStage < 0 ? 0f
            : currentStage > 0 ? 1f
            : Mathf.Clamp01(moistSecondsInStage / Mathf.Max(0.1f, secondsPerStage));

        growthStages[0].transform.localScale = stageZeroFullScale
            * Mathf.Lerp(initialScaleFraction, 1f, progress);
    }

    private void ShowCurrentStage()
    {
        for (int i = 0; i < growthStages.Length; i++)
            growthStages[i].SetActive(i == currentStage);
    }
}
