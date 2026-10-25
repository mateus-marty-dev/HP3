using UnityEngine;

/// <summary>A pre-sown plant: invisible germination, then ordered visible stages.</summary>
public class PlantGrowth : MonoBehaviour
{
    [SerializeField] private GardenBed gardenBed;

    [Header("Visual children, ordered from Stage 0 to fully grown")]
    [SerializeField] private GameObject[] growthStages;

    [Header("Optional harvest: fruit meshes or the entire final stage")]
    [SerializeField] private GameObject[] harvestFruits;
    [SerializeField] private Transform harvestHoldPoint;
    [SerializeField] private Collider harvestPlayerCollider;
    [SerializeField] private Vector3 harvestHoldPositionOffset = new Vector3(0f, 0.65f, 0.35f);
    private bool harvestPrepared;

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
        if (IsFullyGrown && !harvestPrepared)
            PrepareHarvest();

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

    private void PrepareHarvest()
    {
        harvestPrepared = true;
        if (harvestFruits == null || harvestFruits.Length == 0)
            return;
        if (harvestHoldPoint == null)
        {
            Debug.LogError("Assign a Harvest Hold Point to pick the ripe fruit.", this);
            return;
        }

        foreach (GameObject fruit in harvestFruits)
        {
            if (fruit == null || !fruit.transform.IsChildOf(growthStages[growthStages.Length - 1].transform))
                continue;

            MeshFilter[] meshes = fruit.GetComponentsInChildren<MeshFilter>();
            if (meshes.Length == 0 || fruit.GetComponent<PickupInteractable>() != null)
                continue;

            bool hasBounds = false;
            Bounds bounds = new Bounds();
            foreach (MeshFilter mesh in meshes)
            {
                if (mesh.sharedMesh == null) continue;
                Bounds local = mesh.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1,
                            (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = fruit.transform.InverseTransformPoint(mesh.transform.TransformPoint(point));
                    if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!hasBounds) continue;

            // A simple shape also works once the picked fruit becomes a dynamic body.
            foreach (Collider existing in fruit.GetComponentsInChildren<Collider>(true))
                existing.enabled = false;
            if (meshes.Length == 1 && meshes[0].gameObject == fruit)
            {
                SphereCollider shape = fruit.AddComponent<SphereCollider>();
                shape.center = bounds.center;
                shape.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            }
            else
            {
                // A root vegetable includes its leaves. Each mesh gets a small shape,
                // all belonging to the single pickup Rigidbody on the stage root.
                foreach (MeshFilter mesh in meshes)
                {
                    if (mesh.sharedMesh == null) continue;
                    BoxCollider shape = mesh.gameObject.AddComponent<BoxCollider>();
                    shape.center = mesh.sharedMesh.bounds.center;
                    shape.size = mesh.sharedMesh.bounds.size;
                }
            }

            Rigidbody body = fruit.GetComponent<Rigidbody>();
            if (body == null) body = fruit.AddComponent<Rigidbody>();
            body.mass = 0.15f;
            body.useGravity = false;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            PickupInteractable pickup = fruit.AddComponent<PickupInteractable>();
            pickup.ConfigureHarvest(harvestHoldPoint, harvestPlayerCollider, harvestHoldPositionOffset, bounds.center);
        }
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
