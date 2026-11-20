using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public partial class ZombieBehaviour : Interactable
{
    public enum MovementLevel { Schlendern = 1, Spazieren = 2, Rennen = 3 }

    [SerializeField] private MovementLevel movementLevel = MovementLevel.Schlendern;
    [Header("Random gait changes - only when departing")]
    [SerializeField] private Vector2 gaitChangeInterval = new Vector2(30f, 60f);
    [Header("Pause at each destination (seconds)")]
    [SerializeField] private Vector2 idleDuration = new Vector2(3f, 8f);
    [Header("Speed for Schlendern (metres per second)")]
    [Min(0f)] [SerializeField] private float walkSpeed = 0.17f;
    [Header("Destinations")]
    [SerializeField] private bool automaticallyAddGardenTargets = true;
    [SerializeField] private Transform[] destinations = new Transform[0];
    [Min(0.1f)] [SerializeField] private float arrivalDistance = 0.3f;
    [Min(0.1f)] [SerializeField] private float objectClearance = 0.7f;
    [Header("Tomato delivery test")]
    [SerializeField] private bool tomatoDelivery = true;
    [SerializeField] private bool ripeTomatoesAtStart = true;
    [SerializeField] private Transform deliveryDoor;
    private enum DeliveryStep { FetchBasket, CarryToTomatoes, Harvest, ReturnToBasket, CollectBasket, Deliver, Finished }
    private DeliveryStep deliveryStep;
    private Transform taskBasket;
    [Min(1f)] [SerializeField] private float doorClearance = 1.5f;
    private Vector3 carryPosition;
    private Quaternion carryRotation;
    private readonly List<PickupInteractable> harvestedTomatoes = new List<PickupInteractable>();
    private bool harvesting;
    private bool reaching;
    private bool interactionReady;
    [Header("Interaction diagnostics (Play Mode)")]
    [SerializeField] private string interactionStatus = "Noch keine Interaktion";
    private bool deliveryFinished;
    private bool taskStarted;
    private Vector3 harvestHandTarget;
    private PickupInteractable heldTomato;
    [Header("Right hand grip")]
    [Tooltip("Finger offset as a fraction of the visible forearm length, independent of FBX bone scale.")]
    [Range(0f, 0.6f)] [SerializeField] private float fingerGripFraction = 0.3f;
    private NavMeshObstacle basketObstacle;
    private float FingerGripDistance => Vector3.Distance(rightForearm.position, rightHand.position)
        * Mathf.Clamp(fingerGripFraction, 0f, 0.6f);
    private Vector3 FingerGripPosition => rightHand.position + rightHand.up * FingerGripDistance;

    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int Gait = Animator.StringToHash("Gait");
    private static readonly int GaitSpeed = Animator.StringToHash("GaitSpeed");
    private static readonly HashSet<string> GardenDestinationNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "Pond", "Basket", "beehive", "Wooden_Shed", "Workbench",
        "Shovel", "Tomato_House", "CherryBlossom_4"
    };
    private readonly List<Transform> targets = new List<Transform>();
    private Animator animator;
    private NavMeshAgent agent;
    private GameObject navigationObject;
    private Transform currentTarget;
    private Transform previousTarget;
    private float remainingTime;
    private float stuckTime;
    private float previousDistance;
    private float nextGaitChangeTime;
    private bool travelling;
    private bool initialized;
    private Transform carriedBasket;
    private PickupInteractable basketPickup;
    private bool basketPickupEnabled;
    private Rigidbody basketBody;
    private Transform basketParent;
    private bool basketWasKinematic;
    private bool basketUsedGravity;
    private Collider[] basketColliders;
    private bool[] basketColliderStates;
    private Transform leftUpperArm, leftForearm, leftHand;
    private Transform rightUpperArm, rightForearm, rightHand;
    private Transform interactionSpine;
    private float interactionLean;
    private Vector3 basketGripLocal;
    private Quaternion basketCarryRotation;
    private Vector3 basketLeftGripLocal, basketRightGripLocal;
    private float basketCarryDepth;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.SetBool(IsWalking, false);
        interactionText = "E - Korbauftrag starten";
        interactionStatus = "Warte auf Petrusilius (E)";
        CreateInteractionTarget();
        foreach (Transform bone in GetComponentsInChildren<Transform>())
        {
            switch (bone.name)
            {
                case "Spine": interactionSpine = bone; break;
                case "shoulder.L.001": leftUpperArm = bone; break;
                case "forearm.L": leftForearm = bone; break;
                case "hand.L": leftHand = bone; break;
                case "shoulder.R.001": rightUpperArm = bone; break;
                case "forearm.R": rightForearm = bone; break;
                case "hand.R": rightHand = bone; break;
            }
        }
        ScheduleGaitChange();
    }

    private void Start()
    {
        if (tomatoDelivery)
        {
            foreach (PlantGrowth plant in FindObjectsByType<PlantGrowth>(FindObjectsSortMode.None))
                if (ripeTomatoesAtStart && plant.name.ToLowerInvariant().Contains("tomato"))
                    plant.RipenForNpcTest();
            if (deliveryDoor == null)
                foreach (DoorInteractable door in FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None))
                    if (door.name == "Dor") { deliveryDoor = door.transform; break; }
            if (deliveryDoor == null)
                Debug.LogWarning("Assign Delivery Door on the zombie for the tomato delivery.", this);
        }
        CollectTargets();
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        agent.radius = 0.25f;
        agent.height = 1.8f;
        agent.angularSpeed = 180f;
        agent.acceleration = 2f;
        agent.stoppingDistance = arrivalDistance;
        agent.autoBraking = true;

        // Das NavMesh wird dauerhaft in der Unity-Szene gebacken.
        // Der Zombie verwendet nur vorhandene Navigationsdaten.
        if (!NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit start,
                3f,
                NavMesh.AllAreas))
        {
            Debug.LogWarning(
                "Zombie findet kein NavMesh. Navigation in der Szene prüfen.",
                this
            );
            enabled = false;
            return;
        }

        transform.position = start.position;
        agent.enabled = true;
        initialized = agent.isOnNavMesh;
        if (!initialized)
        {
            Debug.LogWarning("Zombie is not on a compatible navigation surface.", this);
            enabled = false;
            return;
        }
        BeginIdle();
    }

    private void OnEnable()
    {
        if (initialized && agent.isOnNavMesh) BeginIdle();
    }

    [ContextMenu("Gartenziele suchen und ergaenzen")]
    private void CollectTargets()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObject(this, "Collect zombie destinations");
#endif
        targets.Clear();
        foreach (Transform target in destinations)
            AddTarget(target);
        if (!automaticallyAddGardenTargets) return;

        foreach (PlantGrowth plant in FindObjectsByType<PlantGrowth>(FindObjectsSortMode.None))
        {
            string name = plant.name.ToLowerInvariant();
            if (name.Contains("tomato") || name.Contains("potato"))
                AddTarget(plant.transform);
        }
        foreach (BucketContents bucket in FindObjectsByType<BucketContents>(FindObjectsSortMode.None))
            AddTarget(bucket.transform);
        foreach (DoorInteractable door in FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None))
            AddTarget(door.transform);
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            // Unity appends suffixes such as " (1)" when objects are duplicated.
            string objectName = candidate.name;
            int suffix = objectName.IndexOf(" (", System.StringComparison.Ordinal);
            if (suffix >= 0) objectName = objectName.Substring(0, suffix);
            if (GardenDestinationNames.Contains(objectName))
                AddTarget(candidate);
        }
        destinations = targets.ToArray();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        if (targets.Count == 0)
            Debug.LogWarning("Zombie has no destinations. Assign scene objects in Destinations.", this);
    }

    private void AddTarget(Transform target)
    {
        if (target != null && target != transform && !target.IsChildOf(transform) && !targets.Contains(target))
            targets.Add(target);
    }

    private void Update()
    {
        if (!initialized || !agent.isOnNavMesh) return;
        if (!taskStarted) return;
        if (harvesting || deliveryFinished) return;
        int level = Mathf.Clamp((int)movementLevel, 1, 3);
        float multiplier = level == 3 ? 4.6170213f : level == 2 ? 1.6489362f : 1f;
        agent.speed = Mathf.Max(0f, walkSpeed) * multiplier;
        animator.SetFloat(Gait, level);
        float referenceSpeed = 0.4548387f * transform.TransformVector(Vector3.forward).magnitude * multiplier;
        float actualSpeed = agent.velocity.magnitude;
        animator.SetBool(IsWalking, travelling && actualSpeed > 0.01f);
        animator.SetFloat(GaitSpeed, actualSpeed / Mathf.Max(0.0001f, referenceSpeed));

        if (!travelling)
        {
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f) ChooseDestination();
            return;
        }
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            BeginIdle();
            return;
        }
        if (agent.pathPending) return;
        if (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete &&
            agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            StartCoroutine(ArriveAndInteract());
            return;
        }
        if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            BeginIdle();
            return;
        }

        // A blocked route must not leave the zombie walking forever.
        if (agent.remainingDistance < previousDistance - 0.02f)
        {
            previousDistance = agent.remainingDistance;
            stuckTime = 0f;
        }
        else
        {
            stuckTime += Time.deltaTime;
            if (stuckTime >= 10f) BeginIdle();
        }
    }

    public override void Interact()
    {
        if (!isActiveAndEnabled || !initialized || !agent.isOnNavMesh || taskStarted || deliveryFinished)
            return;
        taskStarted = true;
        tomatoDelivery = true;
        interactionText = "Korbauftrag laeuft";
        interactionStatus = "Korbauftrag von Petrusilius erhalten";
        BeginIdle();
        remainingTime = 0f;
    }

    private void CreateInteractionTarget()
    {
        // This ray target does not obstruct navigation or carried objects.
        bool found = false;
        Bounds localBounds = default;
        foreach (Renderer visual in GetComponentsInChildren<Renderer>())
        {
            Bounds bounds = visual.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                        (corner & 4) == 0 ? -1 : 1)));
                if (!found) { localBounds = new Bounds(point, Vector3.zero); found = true; }
                else localBounds.Encapsulate(point);
            }
        }
        if (!found) return;
        BoxCollider target = gameObject.AddComponent<BoxCollider>();
        target.isTrigger = true;
        target.center = localBounds.center;
        target.size = localBounds.size;
    }

    private void ChooseDestination()
    {
        if (tomatoDelivery)
        {
            ChooseDeliveryDestination();
            return;
        }
        int count = targets.Count;
        int first = Random.Range(0, Mathf.Max(1, count));
        for (int i = 0; i < count; i++)
        {
            Transform target = targets[(first + i) % count];
            if (target == null || !target.gameObject.activeInHierarchy ||
                target == carriedBasket ||
                (count > 1 && target == previousTarget))
                continue;
            if (!TryApproach(target, out NavMeshPath path)) continue;
            agent.isStopped = false;
            if (!agent.SetPath(path)) continue;
            if (Time.time >= nextGaitChangeTime)
            {
                int currentLevel = Mathf.Clamp((int)movementLevel, 1, 3);
                // Either of the other two gaits, with equal probability.
                movementLevel = (MovementLevel)(1 + (currentLevel - 1 + Random.Range(1, 3)) % 3);
                ScheduleGaitChange();
            }
            currentTarget = target;
            travelling = true;
            previousDistance = float.PositiveInfinity;
            stuckTime = 0f;
            return;
        }
        BeginIdle();
    }

    private bool IsAvailableTomato(PickupInteractable fruit)
    {
        return fruit != null && fruit.isActiveAndEnabled && !fruit.IsHeld &&
            (fruit.CompareTag("Tomato") || fruit.name.StartsWith("Ripe_Tomato")) &&
            (fruit.GetComponentInParent<PlantGrowth>() != null || harvestedTomatoes.Contains(fruit)) &&
            (taskBasket == null || !taskBasket.GetComponent<BucketContents>().ContainsBody(fruit.GetComponent<Rigidbody>()));
    }

    private int TomatoesInBasket()
    {
        if (taskBasket == null) return 0;
        BucketContents contents = taskBasket.GetComponent<BucketContents>();
        if (contents == null) return 0;
        int count = 0;
        foreach (PickupInteractable fruit in FindObjectsByType<PickupInteractable>(FindObjectsSortMode.None))
            if ((fruit.CompareTag("Tomato") || fruit.name.StartsWith("Ripe_Tomato")) &&
                contents.ContainsBody(fruit.GetComponent<Rigidbody>())) count++;
        return count;
    }

    private void HandleDeliveryArrival()
    {
        switch (deliveryStep)
        {
            case DeliveryStep.FetchBasket:
            case DeliveryStep.CollectBasket:
                TryPickUpBasket();
                if (carriedBasket != null)
                {
                    taskBasket = carriedBasket;
                    deliveryStep = TomatoesInBasket() >= 4 ? DeliveryStep.Deliver : DeliveryStep.CarryToTomatoes;
                }
                break;
            case DeliveryStep.CarryToTomatoes:
                if (PutBasketDown()) deliveryStep = DeliveryStep.Harvest;
                break;
            case DeliveryStep.Harvest:
                if (currentTarget.TryGetComponent(out PickupInteractable fruit) && IsAvailableTomato(fruit))
                    StartCoroutine(HarvestTomato(fruit));
                break;
            case DeliveryStep.ReturnToBasket:
                if (heldTomato != null) StartCoroutine(DropTomatoIntoBasket());
                else deliveryStep = DeliveryStep.Harvest;
                break;
            case DeliveryStep.Deliver:
                if (TomatoesInBasket() < 4)
                    deliveryStep = DeliveryStep.CarryToTomatoes;
                else StartCoroutine(DeliverBasketToKitchen());
                break;
        }
    }

    private void ChooseDeliveryDestination()
    {
        if (deliveryStep == DeliveryStep.Harvest && TomatoesInBasket() >= 4)
            deliveryStep = DeliveryStep.CollectBasket;
        var candidates = new List<Transform>();
        switch (deliveryStep)
        {
            case DeliveryStep.FetchBasket:
                foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (candidate.name == "Basket") candidates.Add(candidate);
                break;
            case DeliveryStep.ReturnToBasket:
            case DeliveryStep.CollectBasket:
                if (taskBasket != null) candidates.Add(taskBasket);
                break;
            case DeliveryStep.Deliver:
                if (deliveryDoor != null) candidates.Add(deliveryDoor);
                break;
            default:
                foreach (PickupInteractable fruit in FindObjectsByType<PickupInteractable>(FindObjectsSortMode.None))
                    if (IsAvailableTomato(fruit)) candidates.Add(fruit.transform);
                break;
        }
        candidates.Sort((a, b) => (a.position - transform.position).sqrMagnitude.CompareTo(
            (b.position - transform.position).sqrMagnitude));
        foreach (Transform candidate in candidates)
        {
            if (!TryApproach(candidate, out NavMeshPath path)) continue;
            agent.isStopped = false;
            if (!agent.SetPath(path)) continue;
            currentTarget = candidate;
            travelling = true;
            previousDistance = float.PositiveInfinity;
            stuckTime = 0f;
            return;
        }
        BeginIdle();
    }

    private System.Collections.IEnumerator HarvestTomato(PickupInteractable fruit)
    {
        Rigidbody body = fruit.GetComponent<Rigidbody>();
        if (body == null || !IsAvailableTomato(fruit)) yield break;
        harvesting = true;
        animator.SetBool(IsWalking, false);
        interactionStatus = "Gehe zur Tomate: " + fruit.name;
        yield return ApproachInteraction(body.worldCenterOfMass);
        if (!interactionReady || !IsAvailableTomato(fruit)) { harvesting = false; yield break; }
        reaching = true;
        Vector3 startHand = FingerGripPosition;
        for (float time = 0f; time < 0.6f; time += Time.deltaTime)
        {
            if (!IsAvailableTomato(fruit)) { reaching = false; harvesting = false; yield break; }
            harvestHandTarget = Vector3.Lerp(startHand, body.worldCenterOfMass, Mathf.SmoothStep(0f, 1f, time / 0.6f));
            yield return null;
        }
        harvestHandTarget = body.worldCenterOfMass;
        // Let LateUpdate finish the grip pose without depending on Game-view rendering.
        yield return null;
        if (!IsAvailableTomato(fruit) || !CanGripTomato(fruit))
        {
            interactionStatus = $"Pfluecken abgebrochen: Fingerabstand zur Mitte {Vector3.Distance(FingerGripPosition, body.worldCenterOfMass):F2} m, Hoehe Tomate {body.worldCenterOfMass.y - transform.position.y:F2} m";
            reaching = false; harvesting = false; yield break;
        }
        interactionStatus = "Tomate aufgenommen";
        heldTomato = fruit;
        fruit.enabled = false;
        body.isKinematic = true;
        body.useGravity = false;
        fruit.transform.SetParent(null, true);
        harvestedTomatoes.Add(fruit);
        deliveryStep = DeliveryStep.ReturnToBasket;
        reaching = false;
        harvesting = false;
    }

    private bool CanGripTomato(PickupInteractable fruit)
    {
        // Fingers touch the fruit's surface; they do not need to reach its centre.
        foreach (Collider shape in fruit.GetComponentsInChildren<Collider>())
            if (shape.enabled && !shape.isTrigger &&
                Vector3.Distance(FingerGripPosition, shape.ClosestPoint(FingerGripPosition)) <= 0.12f)
                return true;
        return false;
    }

    private System.Collections.IEnumerator DropTomatoIntoBasket()
    {
        harvesting = true;
        animator.SetBool(IsWalking, false);
        PickupInteractable fruit = heldTomato;
        Rigidbody body = fruit.GetComponent<Rigidbody>();
        Bounds bounds = BasketBounds(taskBasket);
        Vector3 releasePoint = new Vector3(bounds.center.x, bounds.max.y + 0.15f, bounds.center.z);
        yield return ApproachInteraction(releasePoint);
        if (!interactionReady) { harvesting = false; yield break; }
        Vector3 nearSide = Vector3.ProjectOnPlane(transform.position - bounds.center, Vector3.up).normalized;
        releasePoint = bounds.center + Vector3.Scale(nearSide,
            new Vector3(bounds.extents.x * 0.45f, 0f, bounds.extents.z * 0.45f));
        releasePoint.y = Mathf.Max(bounds.max.y + 0.15f, rightUpperArm.position.y - 0.1f);
        reaching = true;
        interactionStatus = "Fuehre Tomate ueber die Korboeffnung";
        Vector3 startHand = FingerGripPosition;
        for (float time = 0f; time < 0.8f; time += Time.deltaTime)
        {
            harvestHandTarget = Vector3.Lerp(startHand,
                releasePoint,
                Mathf.SmoothStep(0f, 1f, time / 0.8f));
            yield return null;
        }
        harvestHandTarget = releasePoint;
        yield return null;
        Vector3 grip = FingerGripPosition;
        Bounds fruitBounds = new Bounds(body.worldCenterOfMass, Vector3.zero);
        foreach (Collider shape in fruit.GetComponentsInChildren<Collider>())
            if (shape.enabled && !shape.isTrigger) fruitBounds.Encapsulate(shape.bounds);
        float marginX = fruitBounds.extents.x + 0.03f;
        float marginZ = fruitBounds.extents.z + 0.03f;
        bool aboveOpening = grip.x > bounds.min.x + marginX && grip.x < bounds.max.x - marginX &&
            grip.z > bounds.min.z + marginZ && grip.z < bounds.max.z - marginZ &&
            grip.y - fruitBounds.extents.y > bounds.max.y;
        if (!aboveOpening)
        {
            interactionStatus = $"Ablegen wartet: Hand noch ausserhalb der Korboeffnung (Zielabstand {Vector3.Distance(grip, releasePoint):F2} m)";
            reaching = false; harvesting = false; yield break;
        }
        heldTomato = null;
        reaching = false;
        fruit.enabled = true;
        fruit.ReleaseNpcHarvest();
        interactionStatus = "Tomate losgelassen, warte auf Landung im Korb";
        // Wait for real collision/settling. A missed basket never counts as a harvest.
        float stable = 0f;
        for (float time = 0f; time < 6f && stable < 0.7f; time += Time.deltaTime)
        {
            bool inside = taskBasket.GetComponent<BucketContents>().ContainsBody(body);
            stable = inside && body.linearVelocity.sqrMagnitude < 0.04f ? stable + Time.deltaTime : 0f;
            yield return null;
        }
        deliveryStep = TomatoesInBasket() >= 4 ? DeliveryStep.CollectBasket : DeliveryStep.Harvest;
        harvesting = false;
    }

    private System.Collections.IEnumerator ArriveAndInteract()
    {
        Transform target = currentTarget;
        BeginIdle();
        harvesting = true;
        if (target != null)
        {
            yield return FaceInteraction(target.position);
            currentTarget = target;
            harvesting = false;
            if (tomatoDelivery) HandleDeliveryArrival();
            else TryPickUpBasket();
        }
        else harvesting = false;
    }

    private System.Collections.IEnumerator FaceInteraction(Vector3 point)
    {
        bool updateRotation = agent.updateRotation;
        agent.updateRotation = false;
        Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion facing = Quaternion.LookRotation(direction);
            while (Quaternion.Angle(transform.rotation, facing) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 150f * Time.deltaTime);
                yield return null;
            }
        }
        agent.updateRotation = updateRotation;
    }

    private System.Collections.IEnumerator ApproachInteraction(Vector3 point)
    {
        interactionReady = false;
        float armLength = Vector3.Distance(rightUpperArm.position, rightForearm.position)
            + Vector3.Distance(rightForearm.position, rightHand.position);
        Vector3 away = Vector3.ProjectOnPlane(transform.position - point, Vector3.up).normalized;
        Vector3 stance = point + away * (armLength * 0.65f);
        bool approachingBasket = deliveryStep == DeliveryStep.ReturnToBasket && taskBasket != null;
        Bounds basketBounds = approachingBasket ? BasketBounds(taskBasket) : default;
        if (approachingBasket)
        {
            float footprint = Mathf.Abs(away.x) * basketBounds.extents.x
                + Mathf.Abs(away.z) * basketBounds.extents.z;
            stance = basketBounds.center + away * (footprint + agent.radius + 0.15f);
        }
        stance.y = transform.position.y;
        if (!NavMesh.SamplePosition(stance, out NavMeshHit hit, 0.5f, agent.areaMask))
        {
            interactionStatus = "Kein begehbarer Standpunkt zum Greifen";
            yield break;
        }
        if (approachingBasket)
        {
            basketBounds.Expand(new Vector3((agent.radius + 0.1f) * 2f, 0f, (agent.radius + 0.1f) * 2f));
            Vector3 horizontalHit = hit.position;
            horizontalHit.y = basketBounds.center.y;
            if (basketBounds.Contains(horizontalHit))
            {
                interactionStatus = "Standpunkt zu nahe am Korb";
                yield break;
            }
        }
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            interactionStatus = "Standpunkt hat keinen vollstaendigen Laufweg";
            yield break;
        }
        float stoppingDistance = agent.stoppingDistance;
        agent.stoppingDistance = 0.05f;
        agent.isStopped = false;
        agent.SetPath(path);
        float elapsed = 0f;
        while (elapsed < 15f && (agent.pathPending ||
            Vector3.ProjectOnPlane(transform.position - hit.position, Vector3.up).magnitude > 0.12f))
        {
            animator.SetBool(IsWalking, agent.velocity.magnitude > 0.01f);
            animator.SetFloat(GaitSpeed, agent.velocity.magnitude /
                Mathf.Max(0.001f, 0.4548387f * transform.TransformVector(Vector3.forward).magnitude));
            elapsed += Time.deltaTime;
            yield return null;
        }
        interactionReady = !agent.pathPending &&
            Vector3.ProjectOnPlane(transform.position - hit.position, Vector3.up).magnitude <= 0.12f;
        interactionStatus = interactionReady ? "Standpunkt erreicht, drehe zum Gegenstand" :
            $"Standpunkt nicht erreicht: Abstand {Vector3.Distance(transform.position, hit.position):F2} m";
        agent.isStopped = true;
        agent.ResetPath();
        agent.stoppingDistance = stoppingDistance;
        animator.SetBool(IsWalking, false);
        yield return FaceInteraction(point);
    }

    private Bounds BasketBounds(Transform basket)
    {
        Bounds bounds = new Bounds(basket.position, Vector3.zero);
        foreach (Collider collider in basket.GetComponentsInChildren<Collider>())
            if (!collider.isTrigger && collider.enabled) bounds.Encapsulate(collider.bounds);
        return bounds;
    }

    private bool PutBasketDown(bool checkDoorClearance = true)
    {
        if (carriedBasket == null) return false;
        Bounds bounds = BasketBounds(carriedBasket);
        // Keep the vessel beside the agent, on the same accessible side of the door.
        Vector3 ground = transform.position + transform.right * (bounds.extents.magnitude + 0.2f);
        if (!NavMesh.SamplePosition(ground, out NavMeshHit hit, 1f, agent.areaMask)) return false;
        if (checkDoorClearance && deliveryStep == DeliveryStep.Deliver &&
            Vector3.ProjectOnPlane(hit.position - deliveryDoor.position, Vector3.up).magnitude < doorClearance)
            return false;
        Vector3 position = basketBody.position + new Vector3(hit.position.x - bounds.center.x,
            hit.position.y - bounds.min.y + 0.03f, hit.position.z - bounds.center.z);
        BucketContents contents = carriedBasket.GetComponent<BucketContents>();
        if (contents == null || !contents.TryMoveDynamicContents(basketBody, position, basketBody.rotation))
            basketBody.position = position;
        ReleaseBasket();
        ConfigureBasketObstacle(taskBasket);
        return true;
    }

    private void ConfigureBasketObstacle(Transform basket)
    {
        if (basket == null) return;
        Bounds worldBounds = BasketBounds(basket);
        Bounds localBounds = new Bounds(basket.InverseTransformPoint(worldBounds.center), Vector3.zero);
        for (int corner = 0; corner < 8; corner++)
            localBounds.Encapsulate(basket.InverseTransformPoint(worldBounds.center +
                Vector3.Scale(worldBounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                    (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
        basketObstacle = basket.GetComponent<NavMeshObstacle>();
        if (basketObstacle == null) basketObstacle = basket.gameObject.AddComponent<NavMeshObstacle>();
        basketObstacle.shape = NavMeshObstacleShape.Box;
        basketObstacle.center = localBounds.center;
        basketObstacle.size = localBounds.size;
        basketObstacle.carving = true;
        basketObstacle.carveOnlyStationary = true;
        basketObstacle.enabled = true;
    }

    private void ReleaseCargo()
    {
        if (heldTomato == null) return;
        heldTomato.enabled = true;
        heldTomato.ReleaseNpcHarvest();
        heldTomato = null;
    }

    private void ScheduleGaitChange()
    {
        float minimum = Mathf.Max(1f, Mathf.Min(gaitChangeInterval.x, gaitChangeInterval.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(gaitChangeInterval.x, gaitChangeInterval.y));
        nextGaitChangeTime = Time.time + Random.Range(minimum, maximum);
    }

    private bool TryApproach(Transform target, out NavMeshPath bestPath)
    {
        bestPath = null;
        Bounds bounds = new Bounds(target.position, Vector3.zero);
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            bounds.Encapsulate(renderer.bounds);
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            Vector3 point = bounds.center + new Vector3(
                Mathf.Sin(angle) * (bounds.extents.x + (target == deliveryDoor ? doorClearance + 0.8f : objectClearance)), 0f,
                Mathf.Cos(angle) * (bounds.extents.z + (target == deliveryDoor ? doorClearance + 0.8f : objectClearance)));
            point.y = bounds.min.y;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, agent.areaMask)) continue;
            NavMeshPath path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;
            float length = 0f;
            for (int corner = 1; corner < path.corners.Length; corner++)
                length += Vector3.Distance(path.corners[corner - 1], path.corners[corner]);
            if (length <= arrivalDistance || length >= bestDistance) continue;
            bestDistance = length;
            bestPath = path;
        }
        return bestPath != null;
    }

    private void BeginIdle()
    {
        if (currentTarget != null) previousTarget = currentTarget;
        currentTarget = null;
        travelling = false;
        agent.isStopped = true;
        agent.ResetPath();
        animator.SetBool(IsWalking, false);
        float minimum = Mathf.Max(0.1f, Mathf.Min(idleDuration.x, idleDuration.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(idleDuration.x, idleDuration.y));
        remainingTime = Random.Range(minimum, maximum);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ReleaseCookingPot();
        harvesting = false;
        reaching = false;
        if (agent != null) agent.updateRotation = true;
        ReleaseBasket();
        ReleaseCargo();
        if (animator != null) animator.SetBool(IsWalking, false);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void OnDestroy()
    {
        if (navigationObject != null) Destroy(navigationObject);
    }

    private void TryPickUpBasket()
    {
        if (carriedBasket != null || currentTarget == null ||
            currentTarget.name.Split('(')[0].Trim() != "Basket") return;
        PickupInteractable pickup = currentTarget.GetComponent<PickupInteractable>();
        Rigidbody body = currentTarget.GetComponent<Rigidbody>();
        if (body == null || (pickup != null && pickup.IsHeld)) return;
        if (leftUpperArm == null || leftForearm == null || leftHand == null ||
            rightUpperArm == null || rightForearm == null || rightHand == null) return;

        Bounds bounds = new Bounds(currentTarget.position, Vector3.zero);
        foreach (Renderer renderer in currentTarget.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(renderer.bounds);
        if (Vector3.Distance(transform.position, bounds.ClosestPoint(transform.position)) > 2f) return;

        carriedBasket = currentTarget;
        basketObstacle = carriedBasket.GetComponent<NavMeshObstacle>();
        if (basketObstacle != null) basketObstacle.enabled = false;
        basketParent = carriedBasket.parent;
        basketBody = body;
        basketWasKinematic = body.isKinematic;
        basketUsedGravity = body.useGravity;
        basketPickup = pickup;
        basketPickupEnabled = pickup != null && pickup.enabled;
        if (pickup != null) pickup.enabled = false;
        basketColliders = carriedBasket.GetComponentsInChildren<Collider>(true);
        basketColliderStates = new bool[basketColliders.Length];
        for (int i = 0; i < basketColliders.Length; i++)
        {
            basketColliderStates[i] = basketColliders[i].enabled;

        }
        body.isKinematic = true;
        body.useGravity = false;
        // Basket.fbx is Z-up, with the two handles on opposite local Y sides.
        Bounds localBounds = new Bounds();
        bool hasBounds = false;
        foreach (MeshFilter filter in carriedBasket.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Bounds meshBounds = filter.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 vertex = meshBounds.center + Vector3.Scale(meshBounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                Vector3 point = carriedBasket.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                if (!hasBounds) { localBounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else localBounds.Encapsulate(point);
            }
        }
        basketLeftGripLocal = new Vector3(localBounds.center.x,
            localBounds.min.y + localBounds.size.y * 0.035f, localBounds.max.z - localBounds.size.z * 0.02f);
        basketRightGripLocal = new Vector3(localBounds.center.x,
            localBounds.max.y - localBounds.size.y * 0.035f, localBounds.max.z - localBounds.size.z * 0.06f);
        basketGripLocal = (basketLeftGripLocal + basketRightGripLocal) * 0.5f;
        basketCarryRotation = Quaternion.Euler(-90f, 90f, 0f);
        float armLength = Vector3.Distance(leftUpperArm.position, leftForearm.position)
            + Vector3.Distance(leftForearm.position, leftHand.position);
        if (taskBasket == null)
        {
            float width = carriedBasket.TransformVector(basketRightGripLocal - basketLeftGripLocal).magnitude;
            float comfortableWidth = Vector3.Distance(leftUpperArm.position, rightUpperArm.position) + armLength * 0.5f;
            carriedBasket.localScale *= Mathf.Min(1f, comfortableWidth / Mathf.Max(0.001f, width));
        }
        basketCarryDepth = carriedBasket.TransformVector(Vector3.right * localBounds.extents.x).magnitude;
        carriedBasket.SetParent(null, true);
        carriedBasket.GetComponent<BucketContents>()?.BeginCarry(null);
    }

    private void FixedUpdate()
    {
        MoveCookingPot();
        if (heldTomato != null)
        {
            Rigidbody fruitBody = heldTomato.GetComponent<Rigidbody>();
            Vector3 handPosition = FingerGripPosition;
            fruitBody.MovePosition(handPosition - (fruitBody.worldCenterOfMass - fruitBody.position));
        }
        if (carriedBasket == null) return;
        GetCarryPose(out carryPosition, out carryRotation);
        BucketContents contents = carriedBasket.GetComponent<BucketContents>();
        if (contents != null && contents.TryMoveDynamicContents(basketBody, carryPosition, carryRotation)) return;
        basketBody.MovePosition(carryPosition);
        basketBody.MoveRotation(carryRotation);
    }

    private void GetCarryPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 shoulders = (leftUpperArm.position + rightUpperArm.position) * 0.5f;
        float armLength = Vector3.Distance(leftUpperArm.position, leftForearm.position)
            + Vector3.Distance(leftForearm.position, leftHand.position);
        Vector3 grip = shoulders + transform.forward * Mathf.Max(armLength * 0.65f, basketCarryDepth + 0.1f)
            - transform.up * (armLength * 0.35f);
        rotation = transform.rotation * basketCarryRotation;
        position = grip - rotation * Vector3.Scale(basketGripLocal, carriedBasket.lossyScale);
    }

    private void LateUpdate()
    {
        PoseCookingPot();
        float desiredLean = reaching && harvestHandTarget.y < rightUpperArm.position.y ? 30f : 0f;
        interactionLean = Mathf.MoveTowards(interactionLean, desiredLean, 75f * Time.deltaTime);
        if (interactionSpine != null)
            interactionSpine.rotation = Quaternion.AngleAxis(interactionLean, transform.right) * interactionSpine.rotation;
        if (reaching || heldTomato != null)
        {
            Vector3 target = reaching ? harvestHandTarget :
                rightUpperArm.position + transform.forward * 0.25f - transform.up * 0.12f;
            Quaternion handRotation = Quaternion.LookRotation(-transform.up, transform.forward);
            Vector3 fingerOffset = handRotation * Vector3.up * FingerGripDistance;
            PoseCarryingArm(rightUpperArm, rightForearm, rightHand, target - fingerOffset);
            rightHand.rotation = handRotation;
        }
        if (heldTomato != null)
        {
            Rigidbody fruitBody = heldTomato.GetComponent<Rigidbody>();
            heldTomato.transform.position += FingerGripPosition - fruitBody.worldCenterOfMass;
        }
        if (carriedBasket == null) return;
        GetCarryPose(out carryPosition, out carryRotation);
        float armLength = Vector3.Distance(leftUpperArm.position, leftForearm.position)
            + Vector3.Distance(leftForearm.position, leftHand.position);
        // Use the actual shoulder sides, including mirrored Blender rigs.
        Vector3 across = (leftUpperArm.position - rightUpperArm.position).normalized;
        Vector3 firstGrip = carryPosition + carryRotation * Vector3.Scale(basketLeftGripLocal, carriedBasket.lossyScale);
        Vector3 secondGrip = carryPosition + carryRotation * Vector3.Scale(basketRightGripLocal, carriedBasket.lossyScale);
        if (Vector3.Dot(firstGrip - secondGrip, across) < 0f)
            (firstGrip, secondGrip) = (secondGrip, firstGrip);
        Vector3 wristOffset = transform.forward * (armLength * 0.12f);
        PoseCarryingArm(leftUpperArm, leftForearm, leftHand, firstGrip - wristOffset);
        PoseCarryingArm(rightUpperArm, rightForearm, rightHand,
            reaching ? harvestHandTarget : secondGrip - wristOffset);
        leftHand.rotation = Quaternion.LookRotation(-transform.up, transform.forward);
        rightHand.rotation = Quaternion.LookRotation(-transform.up, transform.forward);
        carriedBasket.GetComponent<BucketContents>()?.RenderCarry(carryPosition, carryRotation);
    }

    private void PoseCarryingArm(Transform upper, Transform forearm, Transform hand, Vector3 target)
    {
        Vector3 shoulder = upper.position;
        float upperLength = Vector3.Distance(shoulder, forearm.position);
        float lowerLength = Vector3.Distance(forearm.position, hand.position);
        if (upperLength < 0.0001f || lowerLength < 0.0001f) return;
        Vector3 direction = (target - shoulder).normalized;
        float distance = Mathf.Clamp(Vector3.Distance(shoulder, target),
            Mathf.Abs(upperLength - lowerLength) + 0.0001f, upperLength + lowerLength - 0.0001f);
        float along = (upperLength * upperLength - lowerLength * lowerLength
            + distance * distance) / (2f * distance);
        float bend = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 elbowDirection = Vector3.ProjectOnPlane(-transform.up, direction).normalized;
        Vector3 elbow = shoulder + direction * along + elbowDirection * bend;
        upper.rotation = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder)
            * upper.rotation;
        Vector3 reachableTarget = shoulder + direction * distance;
        forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position,
            reachableTarget - forearm.position) * forearm.rotation;
    }

    private void ReleaseBasket()
    {
        if (carriedBasket == null) return;
        carriedBasket.GetComponent<BucketContents>()?.ReleaseContents();
        carriedBasket.SetParent(basketParent, true);

        if (basketBody != null)
        {
            basketBody.isKinematic = basketWasKinematic;
            basketBody.useGravity = basketUsedGravity;
        }
        for (int i = 0; i < basketColliders.Length; i++)
            if (basketColliders[i] != null) basketColliders[i].enabled = basketColliderStates[i];
        if (basketPickup != null) basketPickup.enabled = basketPickupEnabled;
        carriedBasket = null;
    }
}
