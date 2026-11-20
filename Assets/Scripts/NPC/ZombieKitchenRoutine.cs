using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public partial class ZombieBehaviour
{
    [Header("Kitchen basket delivery")]
    [Tooltip("Optional kitchen destination. If empty, the existing Pan object is used only as a location.")]
    [SerializeField] private Transform kitchenDestination;
    private bool kitchenRouteReached;
    [Tooltip("Kochtopf zum Aufnehmen. Ohne Zuweisung wird das Objekt Pan verwendet.")]
    [SerializeField] private PickupInteractable cookingPot;
    private Rigidbody potBody;
    private bool potHeld, potReaching;
    private bool potWasKinematic, potUsedGravity, potPickupEnabled;
    private Vector3 potCenterLocal, potLiftStart;
    private Quaternion potRelativeRotation;
    private float potLiftStarted;
    [SerializeField] private Transform pondDestination;
    private float potDipWeight;
    private Vector3 potDipPosition;
    private bool pondFillCompleted;
    private Vector3 potHomePosition, potHomeStance;
    private Quaternion potHomeRotation;
    private bool placingPot, kitchenCookingCompleted;
    private float basketPourAngle;
    private bool basketEmptied;
    private float potBendAngle;
    private Quaternion potSpineBase, potSpineApplied;
    private bool potSpinePoseApplied;
    private Transform basketReachTarget;
    private Vector3 basketLiftStart;
    private Quaternion basketLiftRotation;
    private float basketLiftStarted = float.NegativeInfinity;

    [Header("Tomaten nach dem Ausschuetten")]
    [Min(0f)][SerializeField] private float tomatoFreeRollSeconds = 0.8f;
    [Min(0f)][SerializeField] private float tomatoLinearDampingWhenSettling = 6f;
    [Min(0f)][SerializeField] private float tomatoAngularDampingWhenSettling = 8f;

    private void GetBasketReachGrips(out Vector3 left, out Vector3 right)
    {
        Bounds bounds = BasketBounds(basketReachTarget);
        Vector3 across = (leftUpperArm.position - rightUpperArm.position).normalized;
        float width = Mathf.Abs(across.x) * bounds.extents.x + Mathf.Abs(across.z) * bounds.extents.z;
        Vector3 center = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        left = center + across * width;
        right = center - across * width;
    }

    private IEnumerator ReachForBasket(Transform target)
    {
        basketReachTarget = target;
        try
        {
            yield return new WaitForSeconds(1.6f);
        }
        finally { basketReachTarget = null; }
    }

    private IEnumerator WalkToKitchenStation(
        Transform station,
        string description, NavMeshPath suppliedPath = null)
    {
        kitchenRouteReached = false;
        interactionStatus = description;

        NavMeshPath path = suppliedPath;
        if (station == null ||
            (path == null && !TryApproach(station, out path)) ||
            path.corners.Length == 0)
        {
            interactionStatus = "Kein begehbarer Weg: " + description;
            yield break;
        }

        Vector3 routeEnd = path.corners[path.corners.Length - 1];

        agent.isStopped = false;

        if (!agent.SetPath(path))
        {
            interactionStatus = "Pfad konnte nicht gesetzt werden";
            yield break;
        }

        Vector3 lastPosition = agent.nextPosition;
        float blockedSeconds = 0f;

        while (agent.isOnNavMesh)
        {
            if (!agent.pathPending)
            {
                float distanceToEnd =
                    Vector3.Distance(agent.nextPosition, routeEnd);

                float remaining = agent.remainingDistance;

                bool validRemaining =
                    !float.IsInfinity(remaining) &&
                    !float.IsNaN(remaining);

                // Ankunft auch bei unbekannter Restdistanz erkennen.
                if (distanceToEnd <= agent.stoppingDistance + 0.12f ||
                    (validRemaining &&
                     remaining <= agent.stoppingDistance + 0.08f))
                {
                    kitchenRouteReached = true;
                    break;
                }

                if (!agent.hasPath ||
                    agent.pathStatus != NavMeshPathStatus.PathComplete)
                    break;

                // Tatsächliche Positionsänderung statt Restdistanz prüfen.
                if (Vector3.Distance(
                        agent.nextPosition, lastPosition) > 0.03f)
                {
                    lastPosition = agent.nextPosition;
                    blockedSeconds = 0f;
                }
                else
                {
                    blockedSeconds += Time.deltaTime;
                }

                if (blockedSeconds > 15f)
                {
                    Debug.LogWarning(
                        $"Zombie steht tatsächlich still. " +
                        $"Abstand zum Ziel: {distanceToEnd:F2} m, " +
                        $"Geschwindigkeit: {agent.velocity.magnitude:F2}, " +
                        $"Pfadstatus: {agent.pathStatus}"
                    );
                    break;
                }
            }

            agent.speed = Mathf.Max(0.1f, walkSpeed);

            animator.SetFloat(Gait, 1f);
            animator.SetBool(
                IsWalking,
                agent.velocity.magnitude > 0.01f
            );

            animator.SetFloat(
                GaitSpeed,
                agent.velocity.magnitude /
                Mathf.Max(
                    0.001f,
                    0.4548387f *
                    transform.TransformVector(Vector3.forward).magnitude
                )
            );

            yield return null;
        }

        agent.isStopped = true;
        agent.ResetPath();
        animator.SetBool(IsWalking, false);

        if (!kitchenRouteReached)
        {
            interactionStatus = "Weg blockiert: " + description;
            yield break;
        }

        yield return FaceInteraction(station.position);
    }

    // Deliver the basket, fill the cooking pot at the pond and carry it back upstairs.
    private IEnumerator DeliverBasketToKitchen()
    {
        harvesting = true;
        // Reserve navigation for this task; on failure, keep the reported status and wait.
        deliveryFinished = true;
        try
        {
            DoorInteractable door = deliveryDoor != null ? deliveryDoor.GetComponent<DoorInteractable>() : null;
            Transform destination = kitchenDestination;
            if (destination == null)
            {
                GameObject existingPan = GameObject.Find("Pan");
                if (existingPan != null) destination = existingPan.transform;
            }
            if (door == null || destination == null)
            {
                interactionStatus = "Korblieferung: Tuer oder Kuechenziel fehlt";
                yield break;
            }
            if (carriedBasket == null)
            {
                interactionStatus = "Korblieferung: Kein Korb in den Haenden";
                yield break;
            }
            door.OpenForNpc();
            interactionStatus = "Oeffne die Schlosstuer";
            float doorWait = 0f;
            while (!door.IsOpen && doorWait < 10f)
            {
                doorWait += Time.deltaTime;
                yield return null;
            }
            if (!door.IsOpen)
            {
                interactionStatus = "Tuer konnte nicht geoeffnet werden";
                yield break;
            }

            yield return WalkToKitchenStation(destination, "Korb ueber die Treppe in die Kueche bringen");
            if (!kitchenRouteReached) yield break;
            // Keep a fixed floor position: the fallback destination may be the carried pot.
            Vector3 kitchenReturnPosition = agent.nextPosition;

            // Kitchen placement does not use the exterior door-clearance rule.
            if (!PutBasketDown(checkDoorClearance: false))
            {
                interactionStatus = "Kein sicherer Abstellplatz fuer den Korb";
                yield break;
            }
            interactionStatus = "Korb abgestellt - gehe zum Kochtopf";
            yield return new WaitForSeconds(0.5f);
            yield return PickUpCookingPot();
            if (!potHeld) yield break;
            yield return FillCookingPotAtPond();
            if (!pondFillCompleted) yield break;

            door.OpenForNpc();
            interactionStatus = "Oeffne die Schlosstuer fuer den Rueckweg";
            doorWait = 0f;
            while (!door.IsOpen && doorWait < 10f)
            {
                doorWait += Time.deltaTime;
                yield return null;
            }
            if (!door.IsOpen)
            {
                interactionStatus = "Rueckweg: Tuer konnte nicht geoeffnet werden";
                yield break;
            }
            var returnPath = new NavMeshPath();
            if (!agent.isOnNavMesh || !agent.CalculatePath(kitchenReturnPosition, returnPath) ||
                returnPath.status != NavMeshPathStatus.PathComplete || returnPath.corners.Length == 0)
            {
                interactionStatus = "Kein vollstaendiger Rueckweg in die Kueche";
                yield break;
            }
            yield return WalkToKitchenStation(destination, "Trage die Pfanne mit Wasser in die Kueche", returnPath);
            if (!kitchenRouteReached) yield break;
            yield return PlacePotAndAddTomatoes();
            if (!kitchenCookingCompleted) yield break;
            deliveryStep = DeliveryStep.Finished;
            interactionText = "Wasser und Tomaten in der Pfanne";
            interactionStatus = "Pfanne abgestellt und Tomaten eingefuellt - warte";
        }
        finally
        {
            potReaching = false;
            harvesting = false;
            travelling = false;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (animator != null) animator.SetBool(IsWalking, false);
        }
    }

    private IEnumerator PickUpCookingPot()
    {
        if (cookingPot == null)
        {
            GameObject pan = GameObject.Find("Pan");
            if (pan != null) cookingPot = pan.GetComponent<PickupInteractable>();
        }
        if (cookingPot == null || !cookingPot.isActiveAndEnabled || cookingPot.IsHeld ||
            !cookingPot.TryGetComponent(out potBody))
        {
            interactionStatus = "Kochtopf fehlt oder ist bereits aufgenommen";
            yield break;
        }
        if (leftHand == null || rightHand == null || leftUpperArm == null || rightUpperArm == null ||
            leftForearm == null || rightForearm == null)
        {
            interactionStatus = "Armzuordnung fuer den Kochtopf fehlt";
            yield break;
        }
        yield return ApproachCookingPot();
        if (!interactionReady) yield break;
        // The player may have picked up the pot while the zombie was approaching it.
        if (cookingPot.IsHeld || !cookingPot.isActiveAndEnabled)
        {
            interactionStatus = "Kochtopf inzwischen nicht mehr verfuegbar";
            yield break;
        }
        if (interactionSpine == null)
        {
            interactionStatus = "Wirbelsaeulenknochen fuer das Buecken fehlt";
            yield break;
        }
        potReaching = true;
        interactionStatus = "Greife den Kochtopf mit beiden Haenden";
        // Give LateUpdate time to bend the torso before checking actual hand contact.
        yield return new WaitForSeconds(1.6f);
        if (cookingPot == null || cookingPot.IsHeld || !cookingPot.isActiveAndEnabled)
        {
            potReaching = false;
            interactionStatus = "Kochtopf inzwischen nicht mehr verfuegbar";
            yield break;
        }
        GetPotGrips(out Vector3 leftGrip, out Vector3 rightGrip);
        if (Vector3.Distance(leftHand.position + leftHand.up * FingerGripDistance, leftGrip) > 0.06f ||
            Vector3.Distance(FingerGripPosition, rightGrip) > 0.06f)
        {
            potReaching = false;
            interactionStatus = "Kochtopf nicht in Reichweite der Haende";
            yield break;
        }
        potWasKinematic = potBody.isKinematic;
        potUsedGravity = potBody.useGravity;
        potPickupEnabled = cookingPot.enabled;
        cookingPot.enabled = false;
        potBody.isKinematic = true;
        potBody.useGravity = false;
        potCenterLocal = potBody.transform.InverseTransformPoint(PotBounds().center);
        potRelativeRotation = Quaternion.Inverse(transform.rotation) * potBody.rotation;
        potHomePosition = potBody.position;
        potHomeRotation = potBody.rotation;
        potHomeStance = agent.nextPosition;
        potLiftStart = potBody.position;
        potLiftStarted = Time.time;
        potHeld = true;
        potReaching = false;
        potBody.GetComponent<BucketContents>()?.BeginCarry(null);
        yield return new WaitForSeconds(1.2f);
    }

    private IEnumerator PlacePotAndAddTomatoes()
    {
        kitchenCookingCompleted = false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(potHomeStance, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            interactionStatus = "Abstellplatz der Pfanne nicht erreichbar";
            yield break;
        }
        yield return WalkToKitchenStation(cookingPot.transform, "Gehe zum Abstellplatz der Pfanne", path);
        if (!kitchenRouteReached) yield break;
        yield return FaceInteraction(potHomePosition);
        interactionStatus = "Stelle die Pfanne an ihren urspruenglichen Platz";
        placingPot = true;
        potDipPosition = potHomePosition;
        try
        {
            while (potDipWeight < 1f)
            {
                potDipWeight = Mathf.MoveTowards(potDipWeight, 1f, Time.deltaTime / 1.2f);
                yield return null;
            }
            yield return new WaitForFixedUpdate();
            ReleaseCookingPot();
        }
        finally { placingPot = false; potDipWeight = 0f; }
        yield return new WaitForSeconds(0.8f);

        BucketContents basket = taskBasket != null ? taskBasket.GetComponent<BucketContents>() : null;
        BucketContents pot = potBody.GetComponent<BucketContents>();
        if (basket == null || pot == null)
        {
            interactionStatus = "Korb oder Pfannen-Inhaltserkennung fehlt";
            yield break;
        }
        var tomatoes = new System.Collections.Generic.List<PickupInteractable>();
        foreach (PickupInteractable fruit in FindObjectsByType<PickupInteractable>(FindObjectsSortMode.None))
            if ((fruit.CompareTag("Tomato") || fruit.name.StartsWith("Ripe_Tomato")) &&
                fruit.TryGetComponent(out Rigidbody body) && basket.ContainsBody(body)) tomatoes.Add(fruit);
        if (tomatoes.Count == 0)
        {
            interactionStatus = "Keine Tomaten im Korb gefunden";
            yield break;
        }
        yield return EmptyKitchenBasket(basket, tomatoes);
        if (!basketEmptied) yield break;
        try
        {
            foreach (PickupInteractable fruit in tomatoes)
            {
                if (fruit == null || fruit.IsHeld || !fruit.enabled) yield break;
                Rigidbody body = fruit.GetComponent<Rigidbody>();
                if (basket.ContainsBody(body))
                {
                    interactionStatus = "Tomate liegt noch im Korb - Ausleeren pruefen";
                    yield break;
                }
                interactionStatus = "Hebe eine ausgeschuettete Tomate auf";
                yield return ApproachInteraction(body.worldCenterOfMass);
                if (!interactionReady) yield break;
                reaching = true;
                Vector3 start = FingerGripPosition;
                for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                {
                    harvestHandTarget = Vector3.Lerp(start, body.worldCenterOfMass, Mathf.SmoothStep(0f, 1f, t / 0.8f));
                    yield return null;
                }
                harvestHandTarget = body.worldCenterOfMass;
                yield return null;
                if (fruit.IsHeld || !fruit.enabled || !CanGripTomato(fruit))
                {
                    interactionStatus = "Ausgeschuettete Tomate nicht greifbar";
                    yield break;
                }
                heldTomato = fruit;
                fruit.enabled = false;
                body.isKinematic = true;
                body.useGravity = false;
                fruit.transform.SetParent(null, true);
                reaching = false;
                var tomatoPath = new NavMeshPath();
                if (!agent.CalculatePath(potHomeStance, tomatoPath) || tomatoPath.status != NavMeshPathStatus.PathComplete)
                {
                    interactionStatus = "Weg zur abgestellten Pfanne blockiert";
                    yield break;
                }
                yield return WalkToKitchenStation(cookingPot.transform, "Trage die Tomate zur Pfanne", tomatoPath);
                if (!kitchenRouteReached) yield break;
                Bounds bounds = PotBounds();
                Vector3 target = new Vector3(bounds.center.x, bounds.max.y + 0.15f, bounds.center.z);
                reaching = true;
                start = FingerGripPosition;
                Vector3 release = rightUpperArm.position + transform.forward * 0.25f;
                release.y = Mathf.Max(release.y, target.y + 0.1f);
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    harvestHandTarget = Vector3.Lerp(start, release, Mathf.SmoothStep(0f, 1f, t / 0.6f));
                    yield return null;
                }
                yield return null;
                // A real ballistic throw; only a landing inside the vessel counts.
                const float flightTime = 0.65f;
                Vector3 velocity = (target - body.worldCenterOfMass - 0.5f * Physics.gravity * flightTime * flightTime) / flightTime;
                heldTomato = null;
                reaching = false;
                fruit.enabled = true;
                fruit.ReleaseNpcHarvest();
                body.linearVelocity = velocity;
                interactionStatus = "Werfe die Tomate in die Pfanne";
                float settled = 0f;
                for (float t = 0f; t < 6f && settled < 0.5f; t += Time.deltaTime)
                {
                    settled = pot.ContainsBody(body) && body.linearVelocity.sqrMagnitude < 0.09f
                        ? settled + Time.deltaTime : 0f;
                    yield return null;
                }
                if (settled < 0.5f)
                {
                    interactionStatus = "Tomate nicht sicher in der Pfanne gelandet - warte";
                    yield break;
                }
            }
            kitchenCookingCompleted = true;
        }
        finally { reaching = false; ReleaseCargo(); }
    }

    private IEnumerator EmptyKitchenBasket(BucketContents basket,
        System.Collections.Generic.List<PickupInteractable> tomatoes)
    {
        basketEmptied = false;
        yield return WalkToKitchenStation(taskBasket, "Gehe zum Korb zum Ausleeren");
        if (!kitchenRouteReached) yield break;
        yield return ReachForBasket(taskBasket);
        Transform previousTarget = currentTarget;
        currentTarget = taskBasket;
        try { TryPickUpBasket(); }
        finally { currentTarget = previousTarget; }
        if (carriedBasket == null)
        {
            interactionStatus = "Korb konnte zum Ausleeren nicht aufgenommen werden";
            yield break;
        }
        yield return new WaitForSeconds(1.2f);
        interactionStatus = "Kippe den Korb und schuette die Tomaten aus";
        try
        {
            while (basketPourAngle < 120f)
            {
                basketPourAngle = Mathf.MoveTowards(basketPourAngle, 120f, 65f * Time.deltaTime);
                yield return null;
            }
            // Contents use the same gravity/collision simulation as the carried bucket.
            for (float elapsed = 0f; elapsed < 5f; elapsed += Time.deltaTime)
            {
                bool empty = true;
                foreach (PickupInteractable fruit in tomatoes)
                    if (fruit != null && basket.ContainsBody(fruit.GetComponent<Rigidbody>())) empty = false;
                if (empty) { basketEmptied = true; break; }
                yield return null;
            }
            while (basketPourAngle > 0f)
            {
                basketPourAngle = Mathf.MoveTowards(basketPourAngle, 0f, 65f * Time.deltaTime);
                yield return null;
            }
            yield return new WaitForFixedUpdate();
            if (!PutBasketDown(checkDoorClearance: false))
            {
                basketEmptied = false;
                interactionStatus = "Kein Abstellplatz fuer den geleerten Korb";
                yield break;
            }
            if (!basketEmptied)
            {
                interactionStatus = "Tomaten sind noch im Korb - Ausleeren nicht abgeschlossen";
                yield break;
            }
            interactionStatus = "Tomaten rollen kurz aus";
            // Nur die ausgeschuetteten Tomaten bremsen. Die Dämpfung wird danach
            // wiederhergestellt, damit Ernte, Tragen und Werfen unveraendert bleiben.
            var originalDamping = new System.Collections.Generic.Dictionary<Rigidbody, Vector2>();
            float stable = 0f;
            const float stableRequired = 0.25f;
            const float maxWait = 8f;
            try
            {
                for (float elapsed = 0f; elapsed < maxWait && stable < stableRequired; elapsed += Time.fixedDeltaTime)
                {
                    yield return new WaitForFixedUpdate();
                    bool allStopped = true;
                    foreach (PickupInteractable fruit in tomatoes)
                    {
                        if (fruit == null || !fruit.TryGetComponent(out Rigidbody body) || body.isKinematic ||
                            basket.ContainsBody(body))
                        {
                            allStopped = false;
                            continue;
                        }

                        // Erst kurz frei rollen lassen, danach die physikalische
                        // Translation und Rotation sanft daempfen.
                        if (elapsed >= tomatoFreeRollSeconds && !body.IsSleeping() &&
                            Mathf.Abs(body.linearVelocity.y) < 0.4f && !originalDamping.ContainsKey(body))
                        {
                            originalDamping.Add(body, new Vector2(body.linearDamping, body.angularDamping));
                            body.linearDamping = Mathf.Max(body.linearDamping, tomatoLinearDampingWhenSettling);
                            body.angularDamping = Mathf.Max(body.angularDamping, tomatoAngularDampingWhenSettling);
                        }

                        // Nicht allein die Bewegung des Mittelpunkts pruefen:
                        // Tomaten koennen sich auch noch auf der Stelle drehen.
                        bool stopped = body.IsSleeping() ||
                            (body.linearVelocity.sqrMagnitude < 0.005f &&
                             body.angularVelocity.sqrMagnitude < 0.09f);
                        if (!stopped) allStopped = false;
                    }
                    stable = allStopped ? stable + Time.fixedDeltaTime : 0f;
                }
            }
            finally
            {
                foreach (var entry in originalDamping)
                {
                    if (entry.Key == null) continue;
                    entry.Key.linearDamping = entry.Value.x;
                    entry.Key.angularDamping = entry.Value.y;
                }
            }
            if (stable < stableRequired)
            {
                basketEmptied = false;
                interactionStatus = "Tomaten rollen noch - sicherer Stillstand nicht erreicht";
                Debug.LogWarning("[Zombie] Tomaten nach dem Ausschuetten nicht innerhalb von 8 Sekunden zur Ruhe gekommen.", this);
                yield break;
            }
            interactionStatus = "Tomaten liegen still - beginne mit dem Aufheben";
        }
        finally { basketPourAngle = 0f; }
    }

    private Bounds PotBounds()
    {
        Bounds result = new Bounds(potBody.position, Vector3.zero);
        bool found = false;
        foreach (Collider part in potBody.GetComponentsInChildren<Collider>())
        {
            if (!part.enabled || part.isTrigger || part.attachedRigidbody != potBody) continue;
            if (!found) result = part.bounds;
            else result.Encapsulate(part.bounds);
            found = true;
        }
        return result;
    }

    private IEnumerator FillCookingPotAtPond()
    {
        pondFillCompleted = false;
        if (!potBody.TryGetComponent(out RainCollector collector) || !collector.isActiveAndEnabled)
        {
            interactionStatus = "Wasserkomponente am Kochtopf fehlt";
            yield break;
        }
        if (pondDestination == null)
            pondDestination = GameObject.Find("Pond")?.transform;
        HP3_WaterBody water = pondDestination != null
            ? pondDestination.GetComponentInChildren<HP3_WaterBody>() : null;
        if (water == null)
        {
            interactionStatus = "Teichziel mit HP3_WaterBody fehlt";
            yield break;
        }
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        NavMeshPath best = null;
        Vector3 fillPoint = default;
        float shortest = float.PositiveInfinity;
        foreach (HP3_WaterBody.ShorePoint shore in water.MeasureShore())
        {
            Vector3 direction = shore.outward;
            Vector3 rim = shore.water - Vector3.up * 0.08f;
            if (!HP3_WaterBody.IsPointUnderwater(rim)) continue;
            Vector3 stance = shore.water + direction * (agent.radius + 0.25f);
            if (!NavMesh.SamplePosition(stance, out NavMeshHit hit, 0.35f, filter) ||
                water.TryGetOpenWaterSurface(hit.position, out _, out _) ||
                Vector3.Distance(Vector3.ProjectOnPlane(hit.position - rim, Vector3.up), Vector3.zero) > 0.7f)
                continue;
            var candidate = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, candidate) || candidate.status != NavMeshPathStatus.PathComplete ||
                candidate.corners.Length == 0) continue;
            float length = 0f;
            Vector3 previous = agent.nextPosition;
            foreach (Vector3 corner in candidate.corners)
            {
                length += Vector3.Distance(previous, corner);
                previous = corner;
            }
            if (length >= shortest) continue;
            shortest = length;
            best = candidate;
            fillPoint = rim;
        }
        if (best == null)
        {
            interactionStatus = "Kein erreichbarer trockener Standplatz am Teich";
            yield break;
        }
        yield return WalkToKitchenStation(pondDestination, "Trage die Pfanne zum Teich", best);
        if (!kitchenRouteReached) yield break;
        yield return FaceInteraction(fillPoint);
        if (!water.TryGetOpenWaterSurface(fillPoint, out Vector3 currentSurface, out float bottomY) ||
            currentSurface.y - bottomY < 0.15f)
        {
            interactionStatus = "Uferpunkt nicht mehr als offenes Wasser verfuegbar";
            yield break;
        }
        fillPoint.y = currentSurface.y - 0.08f;
        potDipPosition = potBody.position + fillPoint - collector.RimPosition;
        interactionStatus = "Senke die Pfanne ins Wasser";
        try
        {
            while (potDipWeight < 1f)
            {
                potDipWeight = Mathf.MoveTowards(potDipWeight, 1f, Time.deltaTime);
                yield return null;
            }
            interactionStatus = "Fuelle die Pfanne am Teich";
            float timeout = 0f;
            while (collector.FillLevel < 0.99f && timeout < 15f)
            {
                timeout += Time.deltaTime;
                yield return null;
            }
            interactionStatus = collector.FillLevel >= 0.99f
                ? "Pfanne voll - hebe sie aus dem Wasser" : "Pfanne nicht gefuellt: Wasserkontakt pruefen";
            while (potDipWeight > 0f)
            {
                potDipWeight = Mathf.MoveTowards(potDipWeight, 0f, Time.deltaTime);
                yield return null;
            }
            pondFillCompleted = collector.FillLevel >= 0.99f;
        }
        finally { potDipWeight = 0f; }
    }

    private bool IsFreeKitchenStance(Vector3 feet, float floorY)
    {
        // The cooking approach must stay on the floor, even if furniture was baked walkable.
        if (Mathf.Abs(feet.y - floorY) > 0.15f) return false;
        float radius = Mathf.Max(0.05f, agent.radius * 0.9f);
        Vector3 bottom = feet + Vector3.up * (radius + 0.12f);
        Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.12f, agent.height - radius);
        foreach (Collider obstacle in Physics.OverlapCapsule(bottom, top, radius,
            Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (obstacle.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    private bool IsKitchenFloorPath(NavMeshPath path, float floorY)
    {
        Vector3 previous = agent.nextPosition;
        foreach (Vector3 corner in path.corners)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, corner) / 0.15f));
            for (int step = 1; step <= steps; step++)
                if (!IsFreeKitchenStance(Vector3.Lerp(previous, corner, step / (float)steps), floorY))
                    return false;
            previous = corner;
        }
        return true;
    }

    // Positioning checks navigation. Actual reach is checked after bending at the pot.
    private IEnumerator ApproachCookingPot()
    {
        interactionReady = false;
        if (!agent.isOnNavMesh) yield break;
        Bounds bounds = PotBounds();
        float kitchenFloorY = agent.nextPosition.y;
        Physics.SyncTransforms();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        NavMeshPath bestPath = null;
        Vector3 bestPosition = default;
        float bestLength = float.PositiveInfinity;

        // Zaehler fuer die nacheinander bestandenen Pruefungen (max. 32 x 12 = 384).
        int navMeshCount = 0;
        int floorCount = 0;
        int clearanceCount = 0;
        int pathCount = 0;

        float leftReach = Vector3.Distance(leftUpperArm.position, leftForearm.position)
            + Vector3.Distance(leftForearm.position, leftHand.position);
        float rightReach = Vector3.Distance(rightUpperArm.position, rightForearm.position)
            + Vector3.Distance(rightForearm.position, rightHand.position);
        // Search outside the pot footprint, on the current floor, and only accept complete paths.
        for (int direction = 0; direction < 32; direction++)
        {
            Vector3 away = Quaternion.Euler(0f, direction * 360f / 32f, 0f) * Vector3.forward;
            float edge = Mathf.Abs(away.x) * bounds.extents.x + Mathf.Abs(away.z) * bounds.extents.z;
            for (int ring = 0; ring < 12; ring++)
            {
                Vector3 candidate = bounds.center + away * (edge + agent.radius + 0.04f + ring * 0.1f);
                candidate.y = kitchenFloorY;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.18f, filter))
                    continue;
                navMeshCount++;

                if (!IsFreeKitchenStance(hit.position, kitchenFloorY))
                    continue;
                floorCount++;

                Vector3 closest = bounds.ClosestPoint(new Vector3(hit.position.x, bounds.center.y, hit.position.z));
                if (Vector2.Distance(new Vector2(closest.x, closest.z),
                    new Vector2(hit.position.x, hit.position.z)) < agent.radius + 0.02f) continue;
                clearanceCount++;

                Vector3 forward = Vector3.ProjectOnPlane(bounds.center - hit.position, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.5f) continue;
                var path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete ||
                    path.corners.Length == 0) continue;
                if (!IsKitchenFloorPath(path, kitchenFloorY)) continue;
                pathCount++;

                float length = 0f;
                Vector3 previous = agent.nextPosition;
                foreach (Vector3 corner in path.corners)
                {
                    length += Vector3.Distance(previous, corner);
                    previous = corner;
                }
                // Prefer close stances; distant rings are fallbacks for blocked inner positions.
                float score = length + ring * 2f;
                if (score >= bestLength) continue;
                bestLength = score;
                bestPath = path;
                bestPosition = hit.position;
            }
        }
        if (bestPath == null)
        {
            Debug.LogWarning(
                $"[Zombie Pot Diagnose] Kandidaten: 384 | " +
                $"NavMesh: {navMeshCount} | Stockwerk: {floorCount} | " +
                $"Freier Abstand: {clearanceCount} | " +
                $"Vollstaendige Pfade: {pathCount} | " +
                $"Linker Arm: {leftReach:F2} m | Rechter Arm: {rightReach:F2} m | " +
                $"Topfposition: {bounds.center} | Agent-Y: {agent.nextPosition.y:F2}",
                this);
            interactionStatus = "Kein begehbarer Standplatz neben dem Kochtopf";
            yield break;
        }
        float stoppingDistance = agent.stoppingDistance;
        try
        {
            agent.stoppingDistance = 0.03f;
            agent.isStopped = false;
            if (!agent.SetPath(bestPath)) yield break;
            interactionStatus = "Gehe zum freien Standplatz am Kochtopf";
            float blocked = 0f;
            Vector3 previous = agent.nextPosition;
            while (agent.isOnNavMesh)
            {
                if (Mathf.Abs(agent.nextPosition.y - kitchenFloorY) > 0.15f)
                {
                    interactionStatus = "Topfannaherung abgebrochen: Kuechenboden verlassen";
                    break;
                }
                if (Vector3.Distance(agent.nextPosition, bestPosition) <= 0.08f)
                {
                    interactionReady = true;
                    break;
                }
                if (!agent.pathPending && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)) break;
                if (Vector3.Distance(previous, agent.nextPosition) > 0.03f)
                {
                    previous = agent.nextPosition;
                    blocked = 0f;
                }
                else blocked += Time.deltaTime;
                if (blocked > 10f) break;
                agent.speed = Mathf.Max(0.1f, walkSpeed);
                animator.SetFloat(Gait, 1f);
                animator.SetBool(IsWalking, agent.velocity.magnitude > 0.01f);
                animator.SetFloat(GaitSpeed, agent.velocity.magnitude /
                    Mathf.Max(0.001f, 0.4548387f * transform.TransformVector(Vector3.forward).magnitude));
                yield return null;
            }
        }
        finally
        {
            agent.stoppingDistance = stoppingDistance;
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            animator.SetBool(IsWalking, false);
        }
        if (!interactionReady)
        {
            interactionStatus = "Weg zum Kochtopf blockiert";
            yield break;
        }
        yield return FaceInteraction(PotBounds().center);
    }

    private void MoveCookingPot()
    {
        if (!potHeld || potBody == null) return;
        float armLength = Vector3.Distance(leftUpperArm.position, leftForearm.position)
            + Vector3.Distance(leftForearm.position, leftHand.position);
        // Place the grips, not the pot centre, relative to the shoulders.
        // Otherwise a tall pot puts its rim and the hands up against the face.
        Bounds bounds = PotBounds();
        float gripHeight = bounds.extents.y * 0.5f;
        float potDepth = Mathf.Abs(transform.forward.x) * bounds.extents.x
            + Mathf.Abs(transform.forward.z) * bounds.extents.z;
        float carryDistance = Mathf.Max(armLength * 0.75f, potDepth + agent.radius - 0.05f);
        float wristForward = Mathf.Max(0f, carryDistance - FingerGripDistance);
        float availableDrop = Mathf.Sqrt(Mathf.Max(0f,
            armLength * armLength * 0.9f - wristForward * wristForward));
        float handDrop = Mathf.Min(armLength * 0.5f, availableDrop);
        Vector3 center = (leftUpperArm.position + rightUpperArm.position) * 0.5f
            + transform.forward * carryDistance
            - transform.up * (handDrop + gripHeight);
        Quaternion rotation = transform.rotation * potRelativeRotation;
        Vector3 position = center - rotation * Vector3.Scale(potCenterLocal, potBody.transform.lossyScale);
        position = Vector3.Lerp(potLiftStart, position, Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((Time.time - potLiftStarted) / 1.2f)));
        position = Vector3.Lerp(position, potDipPosition, Mathf.SmoothStep(0f, 1f, potDipWeight));
        if (placingPot) rotation = Quaternion.Slerp(rotation, potHomeRotation, Mathf.SmoothStep(0f, 1f, potDipWeight));
        BucketContents contents = potBody.GetComponent<BucketContents>();
        if (contents != null && contents.TryMoveDynamicContents(potBody, position, rotation)) return;
        potBody.MovePosition(position);
        potBody.MoveRotation(rotation);
    }

    private void GetPotGrips(out Vector3 left, out Vector3 right)
    {
        Bounds bounds = PotBounds();
        Vector3 across = transform.right;
        float halfWidth = Mathf.Abs(across.x) * bounds.extents.x + Mathf.Abs(across.z) * bounds.extents.z;
        Vector3 center = bounds.center + Vector3.up * bounds.extents.y * 0.5f;
        left = center - across * halfWidth;
        right = center + across * halfWidth;
        if (Vector3.Dot(left - right, leftUpperArm.position - rightUpperArm.position) < 0f)
            (left, right) = (right, left);
    }

    private void PoseCookingPot()
    {
        PosePotBend();
        if (basketReachTarget != null)
        {
            GetBasketReachGrips(out Vector3 basketLeft, out Vector3 basketRight);
            Quaternion handPose = Quaternion.LookRotation(-transform.up, transform.forward);
            Vector3 fingerOffset = handPose * Vector3.up * FingerGripDistance;
            PoseCarryingArm(leftUpperArm, leftForearm, leftHand, basketLeft - fingerOffset);
            PoseCarryingArm(rightUpperArm, rightForearm, rightHand, basketRight - fingerOffset);
            leftHand.rotation = rightHand.rotation = handPose;
        }
        if ((!potHeld && !potReaching) || potBody == null) return;
        GetPotGrips(out Vector3 left, out Vector3 right);
        Quaternion rotation = Quaternion.LookRotation(-transform.up, transform.forward);
        Vector3 offset = rotation * Vector3.up * FingerGripDistance;
        Vector3 shoulderCenter = (leftUpperArm.position + rightUpperArm.position) * 0.5f;
        Vector3 leftOut = (leftUpperArm.position - shoulderCenter).normalized;
        Vector3 rightOut = (rightUpperArm.position - shoulderCenter).normalized;
        PoseCarryingArm(leftUpperArm, leftForearm, leftHand, left - offset,
            -transform.up + leftOut * 0.6f);
        PoseCarryingArm(rightUpperArm, rightForearm, rightHand, right - offset,
            -transform.up + rightOut * 0.6f);
        leftHand.rotation = rotation;
        rightHand.rotation = rotation;
        if (potHeld) potBody.GetComponent<BucketContents>()?.RenderCarry(potBody.position, potBody.rotation);
    }

    private void RestorePotBend()
    {
        if (!potSpinePoseApplied || interactionSpine == null) return;
        // Restore only our own pose; preserve a fresh pose written by the Animator.
        if (Quaternion.Angle(interactionSpine.localRotation, potSpineApplied) < 0.01f)
            interactionSpine.localRotation = potSpineBase;
        potSpinePoseApplied = false;
    }

    private void PosePotBend()
    {
        if (interactionSpine == null) return;
        float desired = 0f;
        if (((potReaching || potDipWeight > 0f) && potBody != null) || basketReachTarget != null)
        {
            Vector3 left, right;
            if (basketReachTarget != null) GetBasketReachGrips(out left, out right);
            else GetPotGrips(out left, out right);
            Vector3 wristOffset = transform.forward * FingerGripDistance;
            Vector3 pivot = interactionSpine.position;
            float leftReach = (Vector3.Distance(leftUpperArm.position, leftForearm.position)
                + Vector3.Distance(leftForearm.position, leftHand.position)) * 0.98f;
            float rightReach = (Vector3.Distance(rightUpperArm.position, rightForearm.position)
                + Vector3.Distance(rightForearm.position, rightHand.position)) * 0.98f;
            float bestError = float.PositiveInfinity;
            // Stop at the first reachable angle, including zero; never fold fully forwards.
            // Chest-height objects need arm positioning, never a downward torso bend.
            float shoulderHeight = (leftUpperArm.position.y + rightUpperArm.position.y) * 0.5f;
            int maxBend = (left.y + right.y) * 0.5f >= shoulderHeight - 0.15f ? 0 : 70;
            for (int angle = 0; angle <= maxBend; angle += 2)
            {
                Quaternion bend = Quaternion.AngleAxis(angle, transform.right);
                Vector3 l = pivot + bend * (leftUpperArm.position - pivot);
                Vector3 r = pivot + bend * (rightUpperArm.position - pivot);
                float leftDistance = Vector3.Distance(l, left - wristOffset);
                float rightDistance = Vector3.Distance(r, right - wristOffset);
                if (leftDistance <= leftReach && rightDistance <= rightReach)
                {
                    desired = angle;
                    break;
                }
                float error = Mathf.Max(leftDistance - leftReach, rightDistance - rightReach);
                if (error >= bestError) continue;
                bestError = error;
                desired = angle;
            }
        }
        potBendAngle = Mathf.MoveTowards(potBendAngle, desired, 90f * Time.deltaTime);
        if (potBendAngle < 0.001f) return;
        potSpineBase = interactionSpine.localRotation;
        interactionSpine.rotation = (Quaternion.AngleAxis(potBendAngle, transform.right)
            * interactionSpine.rotation).normalized;
        potSpineApplied = interactionSpine.localRotation;
        potSpinePoseApplied = true;
    }

    private void ReleaseCookingPot()
    {
        RestorePotBend();
        basketReachTarget = null;
        potBendAngle = 0f;
        potReaching = false;
        if (!potHeld) return;
        potHeld = false;
        if (potBody != null)
        {
            potBody.GetComponent<BucketContents>()?.ReleaseContents();
            potBody.isKinematic = potWasKinematic;
            potBody.useGravity = potUsedGravity;
        }
        if (cookingPot != null) cookingPot.enabled = potPickupEnabled;
    }
}
