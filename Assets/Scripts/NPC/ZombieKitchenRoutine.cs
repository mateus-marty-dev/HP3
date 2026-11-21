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
    [Header("Kueche: Herd anzuenden")]
    [Tooltip("StoveFireInteractable am Herd. Falls leer, wird der naechste Herd zur Pfanne gesucht.")]
    [SerializeField] private StoveFireInteractable kitchenStove;
    [Tooltip("Optionaler NavMesh-Standpunkt vor dem Herd. Wenn er nicht erreichbar ist, sucht der Zombie andere Standorte.")]
    [SerializeField] private Transform kitchenStoveStandPoint;
    [Header("Herd: Abstand und automatische Standplatzsuche")]
    [Tooltip("Maximaler horizontaler Abstand zum Feuer beim Anzuenden. Bei einem tief im Herd liegenden Fire_Wood etwas groesser waehlen.")]
    [Min(0.7f)][SerializeField] private float kitchenStoveReachDistance = 2.2f;
    [Min(1)][SerializeField] private int cookingTomatoesRequired = 4;
    private bool kitchenFireLit;

    [Header("Kueche: Topf auf Herdplatte stellen")]
    [Tooltip("Empty genau ueber der Herdplatte: X/Z = Mitte, Y = Oberflaeche, auf der der Topfboden stehen soll.")]
    [SerializeField] private Transform kitchenHobPlacementPoint;
    [Tooltip("Kleiner Spalt zwischen Topfboden und Herdplatte, damit die Collider nicht ineinander starten.")]
    [Range(0f, 0.1f)][SerializeField] private float kitchenHobBottomClearance = 0.02f;
    [Tooltip("Maximaler horizontaler Abstand zwischen Zombie-Standplatz und Herdplattenmitte.")]
    [Min(0.5f)][SerializeField] private float kitchenHobReachDistance = 1.4f;
    [Min(0.25f)][SerializeField] private float kitchenHobLowerSeconds = 1.5f;
    [Header("Herdplatte: Standplatz und Ablage flexibel korrigieren")]
    [Tooltip("Wie oft der Zombie beim Abstellen einen anderen NavMesh-Standplatz versucht.")]
    [Range(1, 8)][SerializeField] private int kitchenHobPlacementAttempts = 5;
    [Tooltip("Bevorzugter kurzer Abstand von der Herdplattenmitte. Muss kleiner oder gleich Kitchen Hob Reach Distance sein.")]
    [Range(0.5f, 1.5f)][SerializeField] private float kitchenHobPreferredDistance = 1.0f;
    [Tooltip("Mindestabstand zwischen erfolglosen Standplaetzen.")]
    [Range(0.15f, 0.8f)][SerializeField] private float kitchenHobRetryStanceSeparation = 0.35f;
    private bool kitchenPotPlacedOnHob;

    [Header("Kochtopf: Standplatz bei Fehlgriff korrigieren")]
    [Tooltip("Bei zu grossem Handabstand verschiedene Standplaetze rund um den Topf testen.")]
    [Range(1, 8)][SerializeField] private int cookingPotPickupAttempts = 5;
    [Tooltip("Abstand zwischen zwei erfolglosen Standplaetzen; verhindert wiederholtes Anlaufen derselben Position.")]
    [Range(0.15f, 0.8f)][SerializeField] private float cookingPotRetryStanceSeparation = 0.35f;
    [Tooltip("Erlaubte Ungenauigkeit zwischen Hand und Griff beim Kochtopf.")]
    [Range(0.05f, 0.25f)][SerializeField] private float cookingPotGripTolerance = 0.12f;
    [Header("Kochtopf: kontrollierte Greifhilfe bei fehlenden Standplaetzen")]
    [Tooltip("Nur wenn bereits ein Standplatz erreicht wurde und danach keine Alternative existiert: maximaler Handabstand.")]
    [Range(0.15f, 0.45f)][SerializeField] private float cookingPotFallbackGripTolerance = 0.38f;
    [Tooltip("Maximaler horizontaler Abstand vom Zombie zur Oberflaeche des Topfs fuer den letzten Greifversuch.")]
    [Range(0.1f, 0.6f)][SerializeField] private float cookingPotFallbackSurfaceDistance = 0.35f;

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

    [Header("Bodenobjekte greifen (zunaechst Kuechentomaten)")]
    [Tooltip("Visuelle Handdistanz-Kontrolle. Der Griff darf wie im getesteten Einzeltest assistiert werden.")]
    [Range(0.05f, 0.25f)][SerializeField] private float groundPickupTolerance = 0.15f;
    [Range(0f, 75f)][SerializeField] private float groundPickupMaxBend = 60f;
    [Range(1, 6)][SerializeField] private int groundPickupStanceAttempts = 4;
    private bool groundPickupReaching;
    private float groundPickupBendAngle;
    private bool groundPickupReady;

    [Header("Kuechentomaten: bewahrte Einzeltest-Parameter")]
    [Tooltip("Maximaler horizontaler Abstand zum Tomaten-Collider am Standplatz.")]
    [Min(0.2f)][SerializeField] private float kitchenTomatoPickupDistance = 0.65f;
    [Tooltip("Maximale Tomatenhoehe ueber der Standflaeche.")]
    [Min(0.1f)][SerializeField] private float kitchenTomatoMaxPickupHeight = 0.9f;
    [Tooltip("Zeit zum Greifen und anschliessenden Anheben.")]
    [Min(0.1f)][SerializeField] private float kitchenTomatoPickupSeconds = 0.75f;
    [Tooltip("Sicherheitsabstand des Tomaten-Unterrandes ueber dem Pfannenrand.")]
    [Min(0.05f)][SerializeField] private float kitchenTomatoAboveRim = 0.15f;
    [Min(0.1f)][SerializeField] private float kitchenTomatoPlacementSeconds = 0.7f;
    [Min(1f)][SerializeField] private float kitchenTomatoSettleTimeout = 6f;

    [Header("Kuechentomaten: unerreichbare Fruechte erneut versuchen")]
    [Tooltip("Sekunden zwischen erneuten Greifversuchen. Verschiebt der Spieler die Tomate und legt sie ab, wird frueher erneut geprueft.")]
    [Min(1f)][SerializeField] private float kitchenTomatoRetryInterval = 5f;

    // Kuechentransport: Tomate und Hand bewegen sich auf derselben Bahn.
    private bool kitchenTomatoCarry;
    private bool kitchenTomatoPlacing;
    private bool kitchenTomatoPlaced;
    private Vector3 kitchenTomatoLiftStart;
    private float kitchenTomatoLiftStarted;
    private Vector3 kitchenTomatoPlaceStart;
    private Vector3 kitchenTomatoPlaceTarget;
    private float kitchenTomatoPlaceStarted;
    private readonly System.Collections.Generic.List<Collider> kitchenIgnoreFruit =
        new System.Collections.Generic.List<Collider>();
    private readonly System.Collections.Generic.List<Collider> kitchenIgnoreVessel =
        new System.Collections.Generic.List<Collider>();

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

    // Fuegt sich als Abkuerzung in den bestehenden E-Auftrag ein:
    // Wasser + mindestens vier Tomaten in der Pfanne -> direkt zum Herd.
    // Die beiden Bedingungen werden bei jedem neuen Auftrag erneut geprueft.
    // Die wichtigste Abkuerzung wird DIREKT bei E in ZombieBehaviour.Interact()
    // geprueft: volle Pfanne + mindestens vier Tomaten -> nur zum Herd gehen.
    private bool TryStartPreparedCookingTask()
    {
        bool ready = IsCookingPotReadyForFire(out int tomatoCount, out float waterLevel);
        Debug.Log($"[Zombie Herd Startcheck] E gedrueckt: Wasser {waterLevel:P0}, " +
            $"Tomaten in Pfanne {tomatoCount}/{cookingTomatoesRequired}. " +
            (ready ? "Pfanne fertig: direkt zum Herd." : "Noch nicht fertig: normaler Auftrag."), this);
        if (!ready) return false;

        taskStarted = true;
        tomatoDelivery = true;
        deliveryFinished = true;
        harvesting = true;
        travelling = false;
        interactionText = "Kochauftrag laeuft";
        interactionStatus = $"Pfanne bereits vorbereitet ({tomatoCount} Tomaten, " +
            $"Wasser {waterLevel:P0}) - gehe direkt zum Herd";
        StartCoroutine(IgniteKitchenStove(openEntryDoor: true));
        return true;
    }

    private bool IsCookingPotReadyForFire(out int tomatoCount, out float waterLevel)
    {
        tomatoCount = 0;
        waterLevel = 0f;
        if (cookingPot == null)
        {
            GameObject pan = GameObject.Find("Pan");
            if (pan != null) cookingPot = pan.GetComponent<PickupInteractable>();
        }
        if (cookingPot == null || !cookingPot.isActiveAndEnabled || cookingPot.IsHeld)
            return false;

        RainCollector water = cookingPot.GetComponent<RainCollector>();
        if (water == null || !water.isActiveAndEnabled) return false;
        waterLevel = water.FillLevel;

        // 'Voll' heisst hier derselbe Grenzwert wie beim Fuellen am Teich.
        // HasWater ist NICHT genug: das waere schon bei wenigen Tropfen wahr.
        if (waterLevel < 0.99f) return false;

        Rigidbody bodyOfPot = cookingPot.GetComponent<Rigidbody>();
        if (bodyOfPot == null) return false;
        potBody = bodyOfPot;
        Bounds vesselBounds = PotBounds();
        BucketContents contents = cookingPot.GetComponent<BucketContents>();

        // 'ContainsBody' ist fuer bewegte dynamische Fruechte geeignet, lehnt
        // aber u.a. kinematische Rigidbodies ab. Deshalb pruefen wir zusaetzlich
        // die physische Lage der Tomate INNERHALB des stehenden Topfes.
        Physics.SyncTransforms();
        foreach (Rigidbody fruitBody in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (fruitBody == null || fruitBody == bodyOfPot || !fruitBody.gameObject.activeInHierarchy)
                continue;
            GameObject fruit = fruitBody.gameObject;
            bool isTomato = fruit.CompareTag("Tomato") ||
                fruit.name.StartsWith("Ripe_Tomato", System.StringComparison.OrdinalIgnoreCase) ||
                fruit.name.StartsWith("Tomato", System.StringComparison.OrdinalIgnoreCase);
            if (!isTomato) continue;

            PickupInteractable pickup = fruit.GetComponent<PickupInteractable>();
            if (pickup != null && pickup.IsHeld) continue;

            // Zuerst die bestehende praezise Behaelterpruefung benutzen.
            // Fallback fuer vom Spieler vor E platzierte/kinematische Tomaten.
            bool contained = contents != null && contents.ContainsBody(fruitBody);
            if (!contained)
                contained = IsTomatoPhysicallyInPot(fruitBody, water, vesselBounds);
            if (contained) tomatoCount++;
        }

        return tomatoCount >= cookingTomatoesRequired;
    }

    // Wird nur fuer die Vorabkontrolle verwendet; bewegt keine Gegenstaende.
    // Eine Frucht neben der Pfanne auf dem Boden soll nicht mitgezaehlt werden.
    private bool IsTomatoPhysicallyInPot(
        Rigidbody tomato, RainCollector water, Bounds potBounds)
    {
        if (tomato == null || water == null) return false;

        Vector3 center = tomato.worldCenterOfMass;
        float bottomY = potBounds.min.y + 0.02f;
        // Der Boden der Pfanne liegt oft ueber dem Kuechenboden.
        // Diesen Abstand brauchen wir, um Tomaten daneben auszuschliessen.
        foreach (Collider part in potBody.GetComponentsInChildren<Collider>())
        {
            if (part == null || !part.enabled || part.isTrigger ||
                part.attachedRigidbody != potBody) continue;
            if (part.name.StartsWith("ColliderBottom", System.StringComparison.OrdinalIgnoreCase))
                bottomY = Mathf.Max(bottomY, part.bounds.max.y - 0.01f);
        }

        float rimY = water.RimPosition.y;
        if (rimY <= bottomY + 0.02f) rimY = potBounds.max.y;
        if (center.y < bottomY || center.y > rimY + 0.02f)
            return false;

        // Konservativer horizontaler Innenbereich; Griffe der Pfanne koennen
        // die Collider-Bounds vergroessern, deshalb 10 % Abstand zum Rand.
        float radiusX = Mathf.Max(0.05f, potBounds.extents.x * 0.90f);
        float radiusZ = Mathf.Max(0.05f, potBounds.extents.z * 0.90f);
        float x = (center.x - water.RimPosition.x) / radiusX;
        float z = (center.z - water.RimPosition.z) / radiusZ;
        return x * x + z * z <= 1f;
    }

    private StoveFireInteractable FindKitchenStove()
    {
        if (kitchenStove != null && kitchenStove.isActiveAndEnabled)
            return kitchenStove;
        StoveFireInteractable nearest = null;
        float nearestDistance = float.PositiveInfinity;
        Vector3 source = cookingPot != null ? cookingPot.transform.position : transform.position;
        foreach (StoveFireInteractable stove in
            FindObjectsByType<StoveFireInteractable>(FindObjectsSortMode.None))
        {
            if (stove == null || !stove.isActiveAndEnabled) continue;
            float distance = (stove.transform.position - source).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = stove;
        }
        kitchenStove = nearest;
        return kitchenStove;
    }

    // Fire_Wood liegt im Ofen und muss selbst nicht auf dem NavMesh stehen.
    // Der Zombie braucht einen freien Platz auf dem Kuechenboden davor.
    private float StoveKitchenFloorY()
    {
        // Wichtig: nicht die aktuelle Position des Zombies und nicht den
        // waehrend des Tragens veraenderlichen Topfboden als Etagenhoehe nehmen.
        // Ein moeglicher NavMesh-Punkt auf dem Herd darf dadurch nicht zur
        // Referenz fuer den eigentlichen Kuechenboden werden.
        // Ein expliziter Herd-Standpunkt steht auf dem Kuechenboden.
        // Er ist die zuverlaessigere Hoehenreferenz als die moeglicherweise
        // auf eine hoehere NavMesh-Flaeche projizierte Korb-Zielposition.
        if (kitchenStoveStandPoint != null)
            return kitchenStoveStandPoint.position.y;
        if (agent != null && kitchenDestination != null)
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            if (NavMesh.SamplePosition(kitchenDestination.position, out NavMeshHit floor,
                0.55f, filter)) return floor.position.y;
            return kitchenDestination.position.y;
        }
        if (kitchenStoveStandPoint != null)
            return kitchenStoveStandPoint.position.y;
        if (potBody != null && !potHeld) return PotBounds().min.y;
        return agent != null ? agent.nextPosition.y : transform.position.y;
    }

    // Grundriss des eigentlichen Ofenkorpus, nicht dessen Feuerpartikel
    // und nicht die ganze Schloss- oder Zimmergeometrie.
    // In HP.3 sind StovePotPlacePoint und Zombie_StoveStandPoint
    // Kinder von Brick_Stove. Der groesste Renderer ist dessen Gehaeuse.
    private bool TryGetKitchenStoveBodyBounds(out Bounds stoveBounds)
    {
        stoveBounds = default;
        Transform root = kitchenHobPlacementPoint != null
            ? kitchenHobPlacementPoint.parent
            : kitchenStoveStandPoint != null ? kitchenStoveStandPoint.parent : null;
        if (root == null) return false;

        float largestFootprint = 0f;
        foreach (Renderer visual in root.GetComponentsInChildren<Renderer>())
        {
            if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy)
                continue;
            Bounds b = visual.bounds;
            float area = b.size.x * b.size.z;
            if (area < 0.01f || area <= largestFootprint) continue;
            stoveBounds = b;
            largestFootprint = area;
        }
        return largestFootprint > 0f;
    }

    // Ein gueltiger Standplatz ist nicht bloss nah am Feuer:
    // Der NavMesh-Agent muss auf dem Kuechenboden AUSSERHALB des Herdkorpus stehen.
    private bool IsSafeKitchenStoveStance(Vector3 position)
    {
        float floorY = StoveKitchenFloorY();
        if (Mathf.Abs(position.y - floorY) > 0.22f) return false;
        if (TryGetKitchenStoveBodyBounds(out Bounds body))
        {
            float clearance = (agent != null ? agent.radius : 0.25f) + 0.07f;
            if (position.x >= body.min.x - clearance &&
                position.x <= body.max.x + clearance &&
                position.z >= body.min.z - clearance &&
                position.z <= body.max.z + clearance)
                return false;
        }
        return true;
    }

    private bool IsZombieAtStove(StoveFireInteractable stove)
    {
        if (stove == null || agent == null || !agent.isOnNavMesh) return false;

        Vector3 feet = agent.nextPosition;
        float distance = Vector3.ProjectOnPlane(
            stove.transform.position - feet, Vector3.up).magnitude;
        if (distance > Mathf.Max(0.7f, kitchenStoveReachDistance)) return false;

        // Die Herd-Interaktion darf NICHT von der strengen Rechteck-Pruefung
        // fuer die Topfablage abhaengen. Die Renderer-Bounds des Ofens
        // schliessen in dieser Szene auch einen gueltigen Platz direkt
        // vor dem Feuer ein. Der Zombie kann von dort aus anzuenden.
        // Wir verlangen nur den echten Kuechenboden (nicht die Herdplatte).
        float floorY = StoveKitchenFloorY();
        if (Mathf.Abs(feet.y - floorY) > 0.35f) return false;

        // Der Platz liegt unterhalb der Herdplatten-Oberflaeche.
        // Damit kann ein Zombie auf der Herdplatte selbst nicht anzuenden.
        if (kitchenHobPlacementPoint != null &&
            feet.y >= kitchenHobPlacementPoint.position.y - 0.12f)
            return false;

        return true;
    }

    // Bevorzuge den Inspector-Punkt, suche aber bei einem fehlerhaften Punkt
    // mehrere andere begehbare Positionen um den Herd. Der Punkt Fire_Wood
    // selbst ist KEIN geeignetes NavMesh-Ziel.
    private bool TryFindStoveApproachPath(StoveFireInteractable stove,
        out NavMeshPath bestPath, out Vector3 bestPosition)
    {
        bestPath = null;
        bestPosition = Vector3.zero;
        if (stove == null || agent == null || !agent.isOnNavMesh) return false;

        Physics.SyncTransforms();
        float floorY = StoveKitchenFloorY();
        Vector3 fire = stove.transform.position;
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        int navmeshCandidates = 0;
        int correctFloor = 0;
        int completePaths = 0;
        float bestScore = float.PositiveInfinity;

        // Einen expliziten Standpunkt erst auf das NavMesh projizieren;
        // ein Empty kann einige Zentimeter neben dem begehbaren Boden stehen.
        if (kitchenStoveStandPoint != null)
        {
            Vector3 requested = kitchenStoveStandPoint.position;
            if (NavMesh.SamplePosition(requested, out NavMeshHit manual, 0.85f, filter) &&
                IsSafeKitchenStoveStance(manual.position))
            {
                var route = new NavMeshPath();
                if (agent.CalculatePath(manual.position, route) &&
                    route.status == NavMeshPathStatus.PathComplete && route.corners.Length > 0)
                {
                    float d = Vector3.ProjectOnPlane(fire - manual.position, Vector3.up).magnitude;
                    if (d <= kitchenStoveReachDistance + 0.3f)
                    {
                        bestPath = route;
                        bestPosition = manual.position;
                        Debug.Log($"[Zombie Herd Navigation] Manueller Standpunkt gefunden: " +
                            $"Abstand zum Feuer {d:F2} m, Ziel {manual.position}", this);
                        return true;
                    }
                }
            }
            Debug.LogWarning("[Zombie Herd Navigation] Manueller Standpunkt nicht passend " +
                "oder nicht erreichbar; automatische Suche startet.", this);
        }

        // Die Kueche bzw. die Pfanne liegt normalerweise vor der Herdfront.
        Vector3 front = kitchenDestination != null
            ? kitchenDestination.position - fire
            : cookingPot != null ? cookingPot.transform.position - fire
            : agent.nextPosition - fire;
        front = Vector3.ProjectOnPlane(front, Vector3.up).normalized;
        if (front.sqrMagnitude < 0.1f) front = Vector3.forward;

        // Von nahe bis weiter weg: auch dann funktionieren, wenn die Ofenmitte
        // hinter einer Wand oder weit von der NavMesh-Kante entfernt liegt.
        for (int ring = 0; ring < 13; ring++)
        {
            float radius = 0.45f + ring * 0.2f;
            for (int direction = 0; direction < 24; direction++)
            {
                Vector3 away = Quaternion.AngleAxis(direction * 15f, Vector3.up) * front;
                Vector3 requested = fire + away * radius;
                requested.y = floorY;
                if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 0.4f, filter))
                    continue;
                navmeshCandidates++;
                if (!IsSafeKitchenStoveStance(hit.position)) continue;
                correctFloor++;

                float distanceToFire =
                    Vector3.ProjectOnPlane(hit.position - fire, Vector3.up).magnitude;
                if (distanceToFire > kitchenStoveReachDistance + 0.2f) continue;

                var route = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, route) ||
                    route.status != NavMeshPathStatus.PathComplete ||
                    route.corners.Length == 0) continue;
                completePaths++;

                float length = 0f;
                Vector3 previous = agent.nextPosition;
                foreach (Vector3 corner in route.corners)
                {
                    length += Vector3.Distance(previous, corner);
                    previous = corner;
                }
                // Nahe am Feuer bleiben, aber auch einen kurzen realen Laufweg
                // bevorzugen. Die Zielhoehe wurde bereits verifiziert.
                float score = distanceToFire * 2.5f + length * 0.35f;
                if (score >= bestScore) continue;
                bestScore = score;
                bestPath = route;
                bestPosition = hit.position;
            }
        }

        if (bestPath == null)
        {
            Debug.LogWarning($"[Zombie Herd Navigation] Kein Weg zum Feuer. " +
                $"NavMesh-Punkte: {navmeshCandidates}, Kuechenboden: {correctFloor}, " +
                $"vollstaendige Wege: {completePaths}. " +
                $"Zombie: {agent.nextPosition}, Herd: {fire}, " +
                $"Kuechenboden-Y: {floorY:F2}, Reichweite: {kitchenStoveReachDistance:F2}m", this);
            return false;
        }
        Debug.Log($"[Zombie Herd Navigation] Weg gefunden: {bestPosition}, " +
            $"Abstand zum Feuer {Vector3.ProjectOnPlane(bestPosition - fire, Vector3.up).magnitude:F2}m, " +
            $"gepruefte Wege: {completePaths}", this);
        return true;
    }

    // Die Herdplatte liegt ueber dem Kuechen-NavMesh. Der Zielpunkt markiert
    // den TOPF-BODEN auf der Herdoberflaeche, nicht den Pivot des Topfes.
    // Darum muss der Marker nicht begehbar sein.
    private bool IsCookingPotAlreadyOnHob()
    {
        if (kitchenHobPlacementPoint == null || cookingPot == null) return false;
        if (potBody == null && !cookingPot.TryGetComponent(out potBody)) return false;
        Bounds b = PotBounds();
        Vector3 hob = kitchenHobPlacementPoint.position;
        float horizontal = Vector3.ProjectOnPlane(b.center - hob, Vector3.up).magnitude;
        float bottomHeight = b.min.y - hob.y;
        return horizontal <= 0.2f && bottomHeight >= -0.04f && bottomHeight <= 0.15f;
    }

    // Standplatz SUCHE vor der Herdplatte: der Zielmarker ist keine Laufflaeche.
    // Bereits erfolglose Positionen werden fuer den naechsten Versuch ausgeschlossen.
    private bool TryFindHobApproachPath(
        out NavMeshPath bestPath, out Vector3 bestPosition,
        System.Collections.Generic.List<Vector3> excludedStances = null)
    {
        bestPath = null;
        bestPosition = default;
        if (agent == null || !agent.isOnNavMesh || kitchenHobPlacementPoint == null)
            return false;

        Physics.SyncTransforms();
        Vector3 hob = kitchenHobPlacementPoint.position;
        float floorY = StoveKitchenFloorY();
        // Preferred Distance ist nur eine Praeferenz, KEINE harte
        // Obergrenze. Sonst muss der Zombie zu nah an den Herd heran
        // und findet ausserhalb des Ofenkorpus keinen legalen Platz.
        float reach = Mathf.Max(0.5f, kitchenHobReachDistance);
        float bestScore = float.PositiveInfinity;
        NavMeshPath selectedPath = null;
        Vector3 selectedPosition = default;
        int navSamples = 0, floorSamples = 0, completePaths = 0;
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        bool IsExcluded(Vector3 position)
        {
            if (excludedStances == null) return false;
            foreach (Vector3 previous in excludedStances)
                if (Vector3.ProjectOnPlane(position - previous, Vector3.up).sqrMagnitude <
                    kitchenHobRetryStanceSeparation * kitchenHobRetryStanceSeparation)
                    return true;
            return false;
        }

        void Consider(Vector3 proposed, float snapDistance, bool preferredManual)
        {
            proposed.y = floorY;
            if (!NavMesh.SamplePosition(proposed, out NavMeshHit hit, snapDistance, filter))
                return;
            navSamples++;
            // Auch eine auf gleicher Hoehe eingebrannte Herd-Oberflaeche
            // ist keine sichere Standflaeche: Grundriss explizit sperren.
            if (!IsSafeKitchenStoveStance(hit.position)) return;
            floorSamples++;
            if (IsExcluded(hit.position)) return;
            float horizontal = Vector3.ProjectOnPlane(hit.position - hob, Vector3.up).magnitude;
            if (horizontal > reach) return;

            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) ||
                path.status != NavMeshPathStatus.PathComplete || path.corners.Length == 0)
                return;
            completePaths++;

            float length = 0f;
            Vector3 last = agent.nextPosition;
            foreach (Vector3 corner in path.corners)
            {
                length += Vector3.Distance(last, corner);
                last = corner;
            }
            // Bevorzuge den gewuenschten Abstand, aber nur auf dem
            // gueltigen Kuechenboden (nicht auf der Herdplatte).
            float preferred = Mathf.Clamp(kitchenHobPreferredDistance, 0.5f, reach);
            float score = Mathf.Abs(horizontal - preferred) * 2f +
                length * 0.25f + (preferredManual ? -0.15f : 0f);
            if (score >= bestScore) return;
            bestScore = score;
            selectedPath = path;
            selectedPosition = hit.position;
        }

        if (kitchenStoveStandPoint != null)
            Consider(kitchenStoveStandPoint.position, 0.30f, true);

        // Weitere begehbare Standpunkte vor und neben der Herdplatte.
        for (int ring = 0; ring < 10; ring++)
        {
            float radius = 0.38f + ring * 0.10f;
            for (int direction = 0; direction < 24; direction++)
            {
                Vector3 away = Quaternion.Euler(0f, direction * 15f, 0f) * Vector3.forward;
                Consider(hob + away * radius, 0.20f, false);
            }
        }

        bestPath = selectedPath;
        bestPosition = selectedPosition;
        if (bestPath == null)
            Debug.LogWarning($"[Zombie Herdplatte] Kein weiterer Ablagestandplatz. " +
                $"NavMesh {navSamples}, gleiche Etage {floorSamples}, " +
                $"gueltige Wege {completePaths}, bereits versucht {excludedStances?.Count ?? 0}, " +
                $"Zombie {agent.nextPosition}, Ziel {hob}, " +
                $"bevorzugter Abstand {reach:F2} m.", this);
        return bestPath != null;
    }

    // NACH dem Anzuenden. Die bewahrten Greif-, Traege- und BucketContents-
    // Methoden bleiben unveraendert. Nur die Ablage passt den Standplatz an.
    private IEnumerator PlaceCookingPotOnHob()
    {
        kitchenPotPlacedOnHob = false;
        if (kitchenHobPlacementPoint == null)
        {
            interactionStatus = "Herdplatte: Kitchen Hob Placement Point im Inspector zuweisen";
            Debug.LogWarning("[Zombie Herdplatte] Empty auf die Oberflaeche der " +
                "Herdplatte setzen und Kitchen Hob Placement Point zuweisen.", this);
            yield break;
        }
        if (cookingPot == null)
        {
            GameObject pan = GameObject.Find("Pan");
            if (pan != null) cookingPot = pan.GetComponent<PickupInteractable>();
        }
        if (cookingPot == null || !cookingPot.isActiveAndEnabled || cookingPot.IsHeld ||
            !cookingPot.TryGetComponent(out potBody))
        {
            interactionStatus = "Pfanne nicht verfuegbar oder vom Spieler gehalten";
            yield break;
        }
        if (IsCookingPotAlreadyOnHob())
        {
            kitchenPotPlacedOnHob = true;
            interactionStatus = "Pfanne steht bereits auf der Herdplatte";
            yield break;
        }

        // Collider-Boden statt versetztem FBX-/Blender-Pivot ausrichten.
        Bounds beforePickup = PotBounds();
        Vector3 pivotOffsetXZ = potBody.position - beforePickup.center;
        float pivotAboveBottom = potBody.position.y - beforePickup.min.y;
        Vector3 hob = kitchenHobPlacementPoint.position;
        Vector3 destination = new Vector3(
            hob.x + pivotOffsetXZ.x,
            hob.y + pivotAboveBottom + kitchenHobBottomClearance,
            hob.z + pivotOffsetXZ.z);

        interactionStatus = "Gehe zum Topf und hebe ihn mit beiden Haenden an";
        yield return PickUpCookingPot();
        if (!potHeld)
        {
            interactionStatus = "Pfanne konnte nicht aufgenommen werden: " + interactionStatus;
            yield break;
        }

        var rejectedStances = new System.Collections.Generic.List<Vector3>();
        int maximum = Mathf.Max(1, kitchenHobPlacementAttempts);
        // Preferred Distance ist nur eine Praeferenz, KEINE harte
        // Obergrenze. Sonst muss der Zombie zu nah an den Herd heran
        // und findet ausserhalb des Ofenkorpus keinen legalen Platz.
        float reach = Mathf.Max(0.5f, kitchenHobReachDistance);
        bool placed = false;
        string lastProblem = "kein Standplatz getestet";

        // Kein Loslassen, solange der Platz oder die tatsaechliche
        // Position des Topfes ungeeignet ist.
        for (int attempt = 1; attempt <= maximum; attempt++)
        {
            if (!potHeld || potBody == null || !agent.isOnNavMesh)
            {
                interactionStatus = "Ablage abgebrochen: Topf/Navigation nicht mehr verfuegbar";
                yield break;
            }

            Vector3 current = agent.nextPosition;
            float horizontal = Vector3.ProjectOnPlane(current - hob, Vector3.up).magnitude;
            bool currentNotExcluded = true;
            foreach (Vector3 rejected in rejectedStances)
                if (Vector3.ProjectOnPlane(current - rejected, Vector3.up).sqrMagnitude <
                    kitchenHobRetryStanceSeparation * kitchenHobRetryStanceSeparation)
                {
                    currentNotExcluded = false;
                    break;
                }

            // "Nah an der Platte" reicht NICHT: Ein Agent, der auf oder
            // IM Herd steht, darf den Standort nicht als gueltig akzeptieren.
            bool atGoodStance = horizontal <= reach && currentNotExcluded &&
                IsSafeKitchenStoveStance(current);
            Vector3 targetStance = current;
            if (!atGoodStance)
            {
                if (!TryFindHobApproachPath(out NavMeshPath path,
                    out targetStance, rejectedStances))
                {
                    lastProblem = "keinen weiteren begehbaren Standplatz gefunden";
                    break;
                }
                interactionStatus = $"Trage Pfanne zur Herdplatte (Standplatz {attempt}/{maximum})";
                yield return WalkToKitchenStation(kitchenHobPlacementPoint,
                    "Trage Pfanne zur Herdplatte", path);
                if (!kitchenRouteReached)
                {
                    rejectedStances.Add(targetStance);
                    lastProblem = "Weg zum neuen Standplatz blockiert";
                    Debug.LogWarning($"[Zombie Herdplatte] Versuch {attempt}/{maximum}: " +
                        $"Weg zu {targetStance} nicht erreicht. Suche Alternative.", this);
                    continue;
                }
            }

            horizontal = Vector3.ProjectOnPlane(agent.nextPosition - hob, Vector3.up).magnitude;
            if (horizontal > reach + 0.10f ||
                !IsSafeKitchenStoveStance(agent.nextPosition))
            {
                lastProblem = !IsSafeKitchenStoveStance(agent.nextPosition)
                    ? "Standplatz liegt auf oder im Herd statt auf dem Kuechenboden"
                    : $"noch {horizontal:F2} m von Herdplatte entfernt";
                rejectedStances.Add(agent.nextPosition);
                continue;
            }
            yield return FaceInteraction(hob);

            // Sanft absenken; VOR dem Loslassen mit den wirklichen
            // Topf-Collidern kontrollieren, ob die Platte getroffen wurde.
            interactionStatus = $"Stelle Pfanne auf Herdplatte ({attempt}/{maximum})";
            placingPot = true;
            potDipPosition = destination;
            try
            {
                while (potDipWeight < 1f)
                {
                    potDipWeight = Mathf.MoveTowards(potDipWeight, 1f,
                        Time.deltaTime / Mathf.Max(0.25f, kitchenHobLowerSeconds));
                    yield return null;
                }
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                Bounds preview = PotBounds();
                float previewHorizontal = Vector3.ProjectOnPlane(
                    preview.center - hob, Vector3.up).magnitude;
                float previewBottom = preview.min.y - hob.y;
                if (previewHorizontal <= 0.24f &&
                    previewBottom >= -0.04f && previewBottom <= 0.17f)
                {
                    ReleaseCookingPot();
                    if (potBody != null && !potBody.isKinematic)
                    {
                        potBody.linearVelocity = Vector3.zero;
                        potBody.angularVelocity = Vector3.zero;
                    }
                    placed = true;
                }
                else
                {
                    lastProblem = $"Topf nicht richtig ueber Platte: " +
                        $"Mitte {previewHorizontal:F2} m, Boden {previewBottom:F2} m";
                    rejectedStances.Add(agent.nextPosition);
                    Debug.LogWarning($"[Zombie Herdplatte] Versuch {attempt}/{maximum}: " +
                        lastProblem + ". Zombie waehlt anderen Standplatz.", this);
                }
            }
            finally
            {
                placingPot = false;
                potDipWeight = 0f;
            }
            if (placed) break;

            // Weiter nur, solange die Pfanne in den Haenden bleibt.
            // Der letzte fehlgeschlagene Ablageversuch hat sie nicht losgelassen.
            yield return new WaitForSeconds(0.25f);
        }

        if (!placed)
        {
            interactionStatus = $"Pfanne noch in den Haenden: Ablage nach {maximum} " +
                $"Standplatzversuchen nicht moeglich. {lastProblem}";
            Debug.LogWarning("[Zombie Herdplatte] " + interactionStatus, this);
            yield break;
        }
        yield return new WaitForFixedUpdate();
        if (!IsCookingPotAlreadyOnHob())
        {
            Bounds actual = PotBounds();
            interactionStatus = "Pfanne abgelegt, aber noch nicht stabil auf der Herdplatte: " +
                $"Mitte {Vector3.ProjectOnPlane(actual.center - hob, Vector3.up).magnitude:F2} m, " +
                $"Boden {actual.min.y - hob.y:F2} m";
            Debug.LogWarning("[Zombie Herdplatte] " + interactionStatus, this);
            yield break;
        }
        kitchenPotPlacedOnHob = true;
        interactionStatus = "Pfanne steht auf der Herdplatte - bereit zum Kochen";
    }

    private IEnumerator IgniteKitchenStove(bool openEntryDoor = false)
    {
        kitchenFireLit = false;
        try
        {
            // Auch wenn alle Zutaten bereits zu Beginn vorhanden sind,
            // muss der Zombie zum Herd gehen, nicht dort hinteleportieren.
            if (!IsCookingPotReadyForFire(out int tomatoCount, out float waterLevel))
            {
                interactionStatus = "Feuer noch nicht anzunden: " +
                    "Wasser oder vier Tomaten fehlen in der Pfanne";
                Debug.LogWarning("[Zombie Herd] Zutatenkontrolle fehlgeschlagen: " +
                    $"{tomatoCount}/{cookingTomatoesRequired} Tomaten, " +
                    $"Wasserstand {waterLevel:F2}", this);
                yield break;
            }

            StoveFireInteractable stove = FindKitchenStove();
            if (stove == null)
            {
                interactionStatus = "Kein StoveFireInteractable am Herd gefunden";
                yield break;
            }
            // Brennt das Feuer bereits, wird es nicht erneut angezuendet.
            // Der Topf muss aber TROTZDEM auf die Herdplatte gestellt werden.

            if (openEntryDoor && deliveryDoor != null)
            {
                DoorInteractable door = deliveryDoor.GetComponent<DoorInteractable>();
                if (door != null && !door.IsOpen)
                {
                    interactionStatus = "Oeffne die Schlosstuer auf dem Weg zum Herd";
                    door.OpenForNpc();
                    float wait = 0f;
                    while (!door.IsOpen && wait < 10f)
                    {
                        wait += Time.deltaTime;
                        yield return null;
                    }
                    if (!door.IsOpen)
                    {
                        interactionStatus = "Schlosstuer zum Herd konnte nicht geoeffnet werden";
                        yield break;
                    }
                }
            }

            // Ist der Zombie bereits neben dem Herd, muss er nicht nochmals
            // einen komplizierten Pfad zum innen liegenden Fire_Wood suchen.
            if (!IsZombieAtStove(stove))
            {
                if (!TryFindStoveApproachPath(stove,
                    out NavMeshPath firePath, out Vector3 fireStand))
                {
                    interactionStatus = "Kein begehbarer Standplatz vor dem Herd " +
                        "(siehe [Zombie Herd Navigation] in der Console)";
                    yield break;
                }

                interactionStatus = "Gehe zum NavMesh-Standplatz vor dem Herd";
                yield return WalkToKitchenStation(stove.transform,
                    "Gehe zum Herd und zuende Feuer an", firePath);
                if (!kitchenRouteReached)
                {
                    Debug.LogWarning($"[Zombie Herd Navigation] Ziel {fireStand} " +
                        $"nicht erreicht. Zombie ist bei {agent.nextPosition}.", this);
                    yield break;
                }
            }
            if (!IsZombieAtStove(stove))
            {
                interactionStatus = "Feuer nicht erreichbar: Abstand oder falsche Bodenhoehe";
                float fireDistance = Vector3.ProjectOnPlane(
                    agent.nextPosition - stove.transform.position, Vector3.up).magnitude;
                Debug.LogWarning($"[Zombie Herd Navigation] Feuerpruefung fehlgeschlagen: " +
                    $"Abstand {fireDistance:F2} m (Maximum {kitchenStoveReachDistance:F2} m), " +
                    $"Agent-Y {agent.nextPosition.y:F2}, " +
                    $"Kuechenboden-Y {StoveKitchenFloorY():F2}, " +
                    $"Herdplatten-Y {(kitchenHobPlacementPoint != null ? kitchenHobPlacementPoint.position.y.ToString("F2") : "nicht zugewiesen")}",
                    this);
                yield break;
            }
            yield return FaceInteraction(stove.transform.position);

            // Gleiche vorhandene Feuerlogik wie Petrusilius bei E:
            // Partikel, Licht, Sound und Holzverkohlung bleiben beim Herd.
            if (!stove.IsBurning) stove.Interact();
            if (!stove.IsBurning)
            {
                interactionStatus = "Feuer wurde nicht entzuendet: " +
                    "StoveFireInteractable und Flame-Zuweisung pruefen";
                yield break;
            }
            kitchenFireLit = true;
            // Feuer ist bereit: als naechstes kommt der Topf AUF die Herdplatte.
            yield return PlaceCookingPotOnHob();
            if (!kitchenPotPlacedOnHob) yield break;

            // Auch nach der Ablage darf kein "Auftrag erledigt" erscheinen,
            // wenn der Zombie auf dem Herd steht. Versuche ihn wieder auf
            // den sicheren Standplatz vor der Feuerstelle zu bringen.
            if (!IsSafeKitchenStoveStance(agent.nextPosition))
            {
                interactionStatus = "Pfanne steht auf dem Herd - gehe von der Herdplatte herunter";
                if (!TryFindStoveApproachPath(stove, out NavMeshPath escapePath,
                    out Vector3 escapeStand))
                {
                    interactionStatus = "Pfanne abgestellt, aber Zombie steht noch auf dem Herd: " +
                        "Herdgrundflaeche im NavMesh sperren und neu backen";
                    Debug.LogWarning("[Zombie Herd Sicherheit] " + interactionStatus, this);
                    yield break;
                }
                yield return WalkToKitchenStation(stove.transform,
                    "Verlasse die Herdoberflaeche", escapePath);
                if (!kitchenRouteReached || !IsSafeKitchenStoveStance(agent.nextPosition))
                {
                    interactionStatus = "Pfanne abgestellt, aber Zombie erreicht keinen " +
                        "sicheren Kuechenboden vor dem Herd";
                    Debug.LogWarning("[Zombie Herd Sicherheit] " + interactionStatus, this);
                    yield break;
                }
            }
            deliveryStep = DeliveryStep.Finished;
            interactionText = "Kochauftrag erledigt";
            interactionStatus = "Feuer brennt, volle Pfanne steht auf der Herdplatte";
        }
        finally
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (animator != null) animator.SetBool(IsWalking, false);
            harvesting = false;
            travelling = false;
        }
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
            interactionStatus = "Korb abgestellt - pruefe Wasserstand der Pfanne";
            yield return new WaitForSeconds(0.5f);

            // Die Pfanne besitzt bereits einen RainCollector. Dessen FillLevel
            // ist derselbe Messwert, den der Zombie am Teich verwendet.
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

            RainCollector waterInPot = potBody.GetComponent<RainCollector>();
            bool potAlreadyFull = waterInPot != null && waterInPot.isActiveAndEnabled &&
                waterInPot.FillLevel >= 0.99f;

            if (potAlreadyFull)
            {
                // Eine volle Pfanne bleibt in der Kueche stehen. Den vom
                // Zombie erreichbaren Standplatz trotzdem speichern, da er
                // dorthin nach jeder aufgenommenen Tomate zurueckkehrt.
                interactionStatus = "Pfanne bereits voll - Teichbesuch ueberspringen";
                yield return ApproachCookingPot();
                if (!interactionReady) yield break;
                if (cookingPot == null || cookingPot.IsHeld || !cookingPot.isActiveAndEnabled)
                {
                    interactionStatus = "Volle Pfanne inzwischen nicht mehr verfuegbar";
                    yield break;
                }
                potHomePosition = potBody.position;
                potHomeRotation = potBody.rotation;
                potHomeStance = agent.nextPosition;
                yield return PlacePotAndAddTomatoes(potAlreadyFull: true);
            }
            else
            {
                // Bisheriger Ablauf fuer eine leere oder teilweise gefuellte
                // Pfanne bleibt unveraendert.
                interactionStatus = "Pfanne braucht Wasser - gehe zum Kochtopf";
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
            }
            if (!kitchenCookingCompleted) yield break;
            // Erst wenn die Tomaten wirklich in der Pfanne liegen,
            // den Herd zuenden. Nicht mehr bei der Pfanne stehen bleiben.
            yield return IgniteKitchenStove();
            if (!kitchenFireLit) yield break;
        }
        finally
        {
            groundPickupReaching = false;
            groundPickupReady = false;
            reaching = false;
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
        if (interactionSpine == null)
        {
            interactionStatus = "Wirbelsaeulenknochen fuer das Buecken fehlt";
            yield break;
        }

        // Nach einem Fehlgriff die bisherige Position ausschliessen. So sucht
        // der Zombie wirklich einen neuen Standplatz, statt denselben Weg
        // und dieselbe Armhaltung immer wieder zu wiederholen.
        var failedStances = new System.Collections.Generic.List<Vector3>();
        bool gripReached = false;
        float lastLeftDistance = float.PositiveInfinity;
        float lastRightDistance = float.PositiveInfinity;
        int maximum = Mathf.Max(1, cookingPotPickupAttempts);
        for (int attempt = 1; attempt <= maximum; attempt++)
        {
            if (cookingPot == null || cookingPot.IsHeld || !cookingPot.isActiveAndEnabled)
            {
                interactionStatus = "Kochtopf inzwischen nicht mehr verfuegbar";
                yield break;
            }

            interactionStatus = $"Kochtopf: suche Standplatz ({attempt}/{maximum})";
            yield return ApproachCookingPot(failedStances);
            if (!interactionReady)
            {
                // Kein weiterer NavMesh-Standplatz? Dann den BEREITS erreichten
                // Platz einmal als letzte Option verwenden. Das ist nur
                // erlaubt, wenn der Zombie nahe bei der Topfoberflaeche steht;
                // die Pfanne darf nicht aus der Ferne in seine Hand springen.
                if (failedStances.Count > 0 && potBody != null &&
                    agent != null && agent.isOnNavMesh)
                {
                    Bounds reachableBounds = PotBounds();
                    Vector3 feetAtPotHeight = new Vector3(
                        agent.nextPosition.x, reachableBounds.center.y, agent.nextPosition.z);
                    Vector3 nearestPoint = reachableBounds.ClosestPoint(feetAtPotHeight);
                    float distanceFromSurface = Vector3.ProjectOnPlane(
                        feetAtPotHeight - nearestPoint, Vector3.up).magnitude;

                    if (distanceFromSurface <= cookingPotFallbackSurfaceDistance &&
                        cookingPot != null && !cookingPot.IsHeld && cookingPot.isActiveAndEnabled)
                    {
                        interactionStatus = "Kochtopf: kein zweiter Standplatz; " +
                            "versuche vorsichtiges Greifen vom aktuellen Platz";
                        yield return FaceInteraction(reachableBounds.center);
                        potReaching = true;
                        yield return new WaitForSeconds(0.75f);

                        if (cookingPot != null && cookingPot.isActiveAndEnabled &&
                            !cookingPot.IsHeld && potBody != null)
                        {
                            GetPotGrips(out Vector3 fallbackLeft, out Vector3 fallbackRight);
                            lastLeftDistance = Vector3.Distance(
                                leftHand.position + leftHand.up * FingerGripDistance,
                                fallbackLeft);
                            lastRightDistance = Vector3.Distance(
                                FingerGripPosition, fallbackRight);

                            if (lastLeftDistance <= cookingPotFallbackGripTolerance &&
                                lastRightDistance <= cookingPotFallbackGripTolerance)
                            {
                                Debug.Log(
                                    $"[Zombie Topf Greifen] Letzter Standplatz gelungen. " +
                                    $"Handabstaende L {lastLeftDistance:F2} m, " +
                                    $"R {lastRightDistance:F2} m; " +
                                    $"Topfoberflaeche {distanceFromSurface:F2} m entfernt.",
                                    this);
                                gripReached = true;
                                break;
                            }
                        }
                        potReaching = false;
                    }
                    Debug.LogWarning(
                        $"[Zombie Topf Greifen] Keine weitere NavMesh-Position. " +
                        $"Topfabstand {distanceFromSurface:F2} m " +
                        $"(max. {cookingPotFallbackSurfaceDistance:F2} m), " +
                        $"Haende L {lastLeftDistance:F2} m, " +
                        $"R {lastRightDistance:F2} m " +
                        $"(max. {cookingPotFallbackGripTolerance:F2} m).",
                        this);
                }

                // Keine erreichbare Alternative und auch der letzte
                // kontrollierte Griff war nicht moeglich.
                interactionStatus = $"Kochtopf: kein weiterer erreichbarer Standplatz. " +
                    $"Handabstaende L {lastLeftDistance:F2} m, R {lastRightDistance:F2} m";
                yield break;
            }

            potReaching = true;
            interactionStatus = $"Kochtopf greifen ({attempt}/{maximum})";
            // LateUpdate kann die Wirbelsaeule beugen und beide Haende ausrichten.
            yield return new WaitForSeconds(1.6f);
            if (cookingPot == null || cookingPot.IsHeld || !cookingPot.isActiveAndEnabled)
            {
                potReaching = false;
                interactionStatus = "Kochtopf inzwischen nicht mehr verfuegbar";
                yield break;
            }
            GetPotGrips(out Vector3 leftGrip, out Vector3 rightGrip);
            lastLeftDistance = Vector3.Distance(
                leftHand.position + leftHand.up * FingerGripDistance, leftGrip);
            lastRightDistance = Vector3.Distance(FingerGripPosition, rightGrip);
            if (lastLeftDistance <= cookingPotGripTolerance &&
                lastRightDistance <= cookingPotGripTolerance)
            {
                gripReached = true;
                break;
            }

            // Fehlgriff: Arme entspannen, bisherige Position markieren,
            // dann einen anderen Standplatz auf dem NavMesh probieren.
            potReaching = false;
            failedStances.Add(agent.nextPosition);
            interactionStatus = $"Pfanne noch zu weit: L {lastLeftDistance:F2} m, " +
                $"R {lastRightDistance:F2} m. Korrigiere Standplatz ({attempt}/{maximum})";
            Debug.Log($"[Zombie Topf Greifen] Versuch {attempt}/{maximum}: " +
                $"L {lastLeftDistance:F2}m, R {lastRightDistance:F2}m; " +
                $"neuer Standplatz wird gesucht. Zombie {agent.nextPosition}", this);
            yield return new WaitForSeconds(0.3f);
        }
        if (!gripReached)
        {
            potReaching = false;
            interactionStatus = $"Kochtopf nach {maximum} Standplatz-Versuchen nicht greifbar. " +
                $"Handabstand L {lastLeftDistance:F2} m, R {lastRightDistance:F2} m";
            Debug.LogWarning("[Zombie Topf Greifen] " + interactionStatus, this);
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

    private IEnumerator PlacePotAndAddTomatoes(bool potAlreadyFull = false)
    {
        kitchenCookingCompleted = false;
        if (!potAlreadyFull)
        {
            // Nur eine tatsaechlich getragene Pfanne wieder abstellen.
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
        }
        else
        {
            // Pfanne stand bereits voll in der Kueche: Keine Aufnahme,
            // keine Bewegung und kein ReleaseCookingPot() ausfuehren.
            interactionStatus = "Pfanne ist voll - beginne mit den Tomaten";
        }

        BucketContents basket = taskBasket != null ? taskBasket.GetComponent<BucketContents>() : null;
        BucketContents pot = potBody.GetComponent<BucketContents>();
        if (basket == null || pot == null)
        {
            interactionStatus = "Korb oder Pfannen-Inhaltserkennung fehlt";
            yield break;
        }
        var tomatoes = new System.Collections.Generic.List<PickupInteractable>();
        // Zuerst die tatsaechlich vom Zombie geernteten Fruechte verwenden.
        // Basket.ContainsBody() allein kann beim Ausschuetten oder bei
        // dynamischen Inhalten nur einen Teil der vier Fruechte erfassen.
        foreach (PickupInteractable fruit in harvestedTomatoes)
            if (fruit != null && fruit.TryGetComponent(out Rigidbody harvestedBody) &&
                !tomatoes.Contains(fruit)) tomatoes.Add(fruit);
        // Falls schon Tomaten im Korb waren, diese ebenfalls beruecksichtigen.
        foreach (PickupInteractable fruit in FindObjectsByType<PickupInteractable>(FindObjectsSortMode.None))
            if ((fruit.CompareTag("Tomato") || fruit.name.StartsWith("Ripe_Tomato")) &&
                fruit.TryGetComponent(out Rigidbody body) && basket.ContainsBody(body) &&
                !tomatoes.Contains(fruit)) tomatoes.Add(fruit);
        Debug.Log($"[Zombie Kuechentomaten] {tomatoes.Count} Tomaten registriert " +
            $"({harvestedTomatoes.Count} eigene Ernten).", this);
        if (tomatoes.Count == 0)
        {
            interactionStatus = "Keine Tomaten im Korb gefunden";
            yield break;
        }
        yield return EmptyKitchenBasket(basket, tomatoes);
        if (!basketEmptied) yield break;

        // Die urspruengliche foreach-Schleife brach den GESAMTEN Auftrag ab,
        // sobald nur eine Tomate nicht erreichbar war. Jetzt werden alle
        // weiteren Tomaten versucht, bevor eine problematische erneut drankommt.
        var pending = new System.Collections.Generic.List<PickupInteractable>(tomatoes);
        int total = pending.Count;
        int completed = 0;
        // Die Aufgabe bleibt aktiv, solange noch Tomaten fehlen.
        // Der Spieler kann unerreichbare Tomaten jederzeit verschieben.
        string lastProblem = "";

        try
        {
            for (int pass = 1; pending.Count > 0; pass++)
            {
                // Falls im Play Mode ein Gegenstand verschoben wurde, aktuelle
                // Colliderpositionen fuer die neue Standplatzsuche verwenden.
                Physics.SyncTransforms();
                // Nicht von FindObjectsByType-Reihenfolge abhaengen. Die
                // naechstliegende Tomate zuerst, problematische spaeter.
                pending.Sort((a, b) =>
                {
                    float da = a != null ? (a.transform.position - agent.nextPosition).sqrMagnitude
                        : float.PositiveInfinity;
                    float db = b != null ? (b.transform.position - agent.nextPosition).sqrMagnitude
                        : float.PositiveInfinity;
                    return da.CompareTo(db);
                });

                // Snapshot: Nur erfolgreich abgelegte Tomaten aus pending entfernen.
                foreach (PickupInteractable fruit in
                    new System.Collections.Generic.List<PickupInteractable>(pending))
                {
                    bool placed = false;
                    Rigidbody body = fruit != null ? fruit.GetComponent<Rigidbody>() : null;
                    string fruitName = fruit != null ? fruit.name : "(zerstoerte Tomate)";

                    // Hat der Spieler die Tomate bereits selbst in die Pfanne
                    // gelegt, muss der Zombie sie nicht nochmals aufheben.
                    if (body != null && pot.ContainsBody(body))
                    {
                        pending.Remove(fruit);
                        completed++;
                        interactionStatus = $"Tomate liegt schon in der Pfanne: {completed}/{total}";
                        continue;
                    }

                    try
                    {
                        if (fruit == null || body == null || fruit.IsHeld || !fruit.isActiveAndEnabled)
                        {
                            lastProblem = $"{fruitName}: derzeit nicht verfuegbar";
                            continue;
                        }
                        if (basket.ContainsBody(body))
                        {
                            lastProblem = $"{fruitName}: liegt noch im Korb";
                            continue;
                        }

                        interactionStatus = $"Tomate {completed + 1}/{total}: Gehe zu {fruitName} " +
                            $"(Versuch {pass})";
                        yield return ApproachAndReachFloorPickup(fruit);
                        if (!groundPickupReady)
                        {
                            lastProblem = $"{fruitName}: {interactionStatus}";
                            continue;
                        }
                        if (fruit.IsHeld || !fruit.isActiveAndEnabled)
                        {
                            lastProblem = $"{fruitName}: inzwischen nicht verfuegbar";
                            continue;
                        }

                        // Die bereits funktionierende Greifbewegung bleibt unveraendert.
                        float handGap = float.PositiveInfinity;
                        TryGetPickupSurface(fruit, FingerGripPosition, out _, out handGap);
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                        heldTomato = fruit;
                        fruit.enabled = false;
                        body.isKinematic = true;
                        body.useGravity = false;
                        fruit.transform.SetParent(null, true);
                        kitchenTomatoLiftStart = body.worldCenterOfMass;
                        kitchenTomatoLiftStarted = Time.time;
                        kitchenTomatoPlacing = false;
                        kitchenTomatoCarry = true;
                        groundPickupReaching = false;
                        reaching = false;
                        IgnoreKitchenTomatoVesselCollisions(fruit);
                        interactionStatus = handGap > groundPickupTolerance
                            ? $"Tomate {completed + 1}/{total} aufgenommen " +
                              $"(Griff-Korrektur {handGap:F2} m)"
                            : $"Tomate {completed + 1}/{total} aufgenommen";
                        yield return new WaitForSeconds(kitchenTomatoPickupSeconds);

                        var tomatoPath = new NavMeshPath();
                        if (!agent.isOnNavMesh || !agent.CalculatePath(potHomeStance, tomatoPath) ||
                            tomatoPath.status != NavMeshPathStatus.PathComplete ||
                            tomatoPath.corners.Length == 0)
                        {
                            lastProblem = $"{fruitName}: Rueckweg zur Pfanne blockiert";
                            continue;
                        }
                        yield return WalkToKitchenStation(cookingPot.transform,
                            $"Tomate {completed + 1}/{total} zur Pfanne tragen", tomatoPath);
                        if (!kitchenRouteReached)
                        {
                            lastProblem = $"{fruitName}: {interactionStatus}";
                            continue;
                        }

                        // Bewaehrte Handbewegung und sanftes Ablegen ebenfalls unveraendert.
                        yield return PlaceKitchenTomatoInPot(fruit, pot);
                        if (!kitchenTomatoPlaced)
                        {
                            lastProblem = $"{fruitName}: {interactionStatus}";
                            continue;
                        }
                        placed = true;
                    }
                    finally
                    {
                        // Beim fehlgeschlagenen Versuch darf keine Tomate
                        // unsichtbar an der Hand haengen bleiben. Erst die
                        // gemeinsame Bewegung stoppen, dann Physik freigeben.
                        if (!placed && heldTomato == fruit)
                        {
                            kitchenTomatoCarry = false;
                            kitchenTomatoPlacing = false;
                            RestoreKitchenTomatoVesselCollisions();
                            ReleaseCargo();
                            if (body != null)
                            {
                                body.isKinematic = false;
                                body.useGravity = true;
                                body.linearVelocity = Vector3.zero;
                                body.angularVelocity = Vector3.zero;
                            }
                        }
                        groundPickupReaching = false;
                        groundPickupReady = false;
                        reaching = false;
                        if (!placed)
                            Debug.LogWarning($"[Zombie Kuechentomaten] " +
                                $"Versuch {pass}: {lastProblem}", this);
                    }

                    if (placed)
                    {
                        pending.Remove(fruit);
                        completed++;
                        interactionStatus = $"Tomaten in Pfanne: {completed}/{total}";
                    }
                    yield return new WaitForSeconds(0.15f);
                }

                if (pending.Count > 0)
                {
                    // NICHT abbrechen: Andere Tomaten sind bereits bearbeitet;
                    // jetzt auf den Spieler oder den naechsten Zeitversuch warten.
                    yield return WaitForKitchenTomatoRetry(pending, completed, total, lastProblem);
                }
            }
            kitchenCookingCompleted = true;
        }
        finally
        {
            groundPickupReaching = false;
            groundPickupReady = false;
            reaching = false;
            kitchenTomatoCarry = false;
            kitchenTomatoPlacing = false;
            RestoreKitchenTomatoVesselCollisions();
            ReleaseCargo();
        }
    }

    // Unerreichbare Tomaten bleiben Teil des Auftrags. Der Zombie wartet
    // ruhig, bis der naechste Zeitversuch faellig ist oder eine Tomate
    // vom Spieler an eine andere Position gesetzt und losgelassen wurde.
    private IEnumerator WaitForKitchenTomatoRetry(
        System.Collections.Generic.List<PickupInteractable> pending,
        int completed, int total, string lastProblem)
    {
        var positions = new System.Collections.Generic.Dictionary<PickupInteractable, Vector3>();
        foreach (PickupInteractable fruit in pending)
            if (fruit != null) positions[fruit] = fruit.transform.position;

        float interval = Mathf.Max(1f, kitchenTomatoRetryInterval);
        float elapsed = 0f;
        while (elapsed < interval)
        {
            interactionStatus = $"Warte auf Tomaten: {completed}/{total} in der Pfanne, " +
                $"{pending.Count} fehlen. Neuer Versuch in {interval - elapsed:F1}s. " +
                $"Spieler kann sie verschieben. Letzter Fehler: {lastProblem}";

            float step = Mathf.Min(0.25f, interval - elapsed);
            yield return new WaitForSeconds(step);
            elapsed += step;

            foreach (PickupInteractable fruit in pending)
            {
                if (fruit == null || !fruit.isActiveAndEnabled || fruit.IsHeld ||
                    !positions.TryGetValue(fruit, out Vector3 previous)) continue;

                if ((fruit.transform.position - previous).sqrMagnitude < 0.20f * 0.20f)
                    continue;

                // Nicht versuchen, eine gerade rollende Tomate aufzunehmen.
                if (fruit.TryGetComponent(out Rigidbody body) &&
                    body.linearVelocity.sqrMagnitude > 0.04f) continue;

                interactionStatus = $"{fruit.name} wurde verschoben - pruefe neue Standposition";
                yield break;
            }
        }
        interactionStatus = $"Pruefe die {pending.Count} noch fehlenden Tomaten erneut";
    }

    // Gleiches Verfahren wie im erfolgreich getesteten ZombieTomatoPickup:
    // Hand und Tomate haben beim Anheben und Ablegen dasselbe Bewegungsziel.
    private Vector3 KitchenTomatoGripGoal()
    {
        if (kitchenTomatoPlacing)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                (Time.time - kitchenTomatoPlaceStarted) /
                Mathf.Max(0.1f, kitchenTomatoPlacementSeconds)));
            return Vector3.Lerp(kitchenTomatoPlaceStart, kitchenTomatoPlaceTarget, progress);
        }
        float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
            (Time.time - kitchenTomatoLiftStarted) / Mathf.Max(0.1f, kitchenTomatoPickupSeconds)));
        Vector3 carryTarget = rightUpperArm.position + transform.forward * 0.28f
            - transform.up * 0.15f;
        return Vector3.Lerp(kitchenTomatoLiftStart, carryTarget, lift);
    }

    private void StartKitchenTomatoPlacement(Vector3 destination)
    {
        kitchenTomatoPlaceStart = KitchenTomatoGripGoal();
        kitchenTomatoPlaceTarget = destination;
        kitchenTomatoPlaceStarted = Time.time;
        kitchenTomatoPlacing = true;
    }

    private void IgnoreKitchenTomatoVesselCollisions(PickupInteractable fruit)
    {
        RestoreKitchenTomatoVesselCollisions();
        if (fruit == null) return;
        foreach (Collider fruitCollider in fruit.GetComponentsInChildren<Collider>())
        {
            if (fruitCollider == null || !fruitCollider.enabled || fruitCollider.isTrigger) continue;
            // Beide Gefaesse pruefen: Der leere Korb bleibt neben dem Zombie,
            // die Pfanne ist beim Ablegen ebenfalls physikalisch aktiv.
            IgnoreWith(taskBasket);
            IgnoreWith(cookingPot != null ? cookingPot.transform : null);

            void IgnoreWith(Transform vessel)
            {
                if (vessel == null) return;
                foreach (Collider vesselCollider in vessel.GetComponentsInChildren<Collider>())
                {
                    if (vesselCollider == null || !vesselCollider.enabled || vesselCollider.isTrigger ||
                        Physics.GetIgnoreCollision(fruitCollider, vesselCollider)) continue;
                    Physics.IgnoreCollision(fruitCollider, vesselCollider, true);
                    kitchenIgnoreFruit.Add(fruitCollider);
                    kitchenIgnoreVessel.Add(vesselCollider);
                }
            }
        }
    }

    private void RestoreKitchenTomatoVesselCollisions()
    {
        for (int i = 0; i < kitchenIgnoreFruit.Count; i++)
        {
            if (kitchenIgnoreFruit[i] != null && kitchenIgnoreVessel[i] != null)
                Physics.IgnoreCollision(kitchenIgnoreFruit[i], kitchenIgnoreVessel[i], false);
        }
        kitchenIgnoreFruit.Clear();
        kitchenIgnoreVessel.Clear();
    }

    private IEnumerator PlaceKitchenTomatoInPot(PickupInteractable fruit, BucketContents pot)
    {
        kitchenTomatoPlaced = false;
        if (!kitchenTomatoCarry || fruit == null || pot == null ||
            !fruit.TryGetComponent(out Rigidbody body)) yield break;
        if (!TryGetPickupBounds(fruit, out Bounds fruitBounds))
        {
            interactionStatus = "Tomate hat keinen aktiven Collider";
            yield break;
        }

        Bounds potBounds = PotBounds();
        float fruitRadius = Mathf.Max(fruitBounds.extents.x, fruitBounds.extents.y,
            fruitBounds.extents.z);
        float safeHeight = potBounds.max.y + fruitRadius + kitchenTomatoAboveRim;
        Vector3 away = Vector3.ProjectOnPlane(transform.position - potBounds.center,
            Vector3.up).normalized;
        if (away.sqrMagnitude < 0.001f) away = -transform.forward;
        float edge = Mathf.Abs(away.x) * potBounds.extents.x +
            Mathf.Abs(away.z) * potBounds.extents.z;

        // 1. Erst an der Zombie-Seite des Pfannenrandes hochheben.
        Vector3 outsideTarget = potBounds.center +
            away * (edge + fruitRadius + 0.18f);
        outsideTarget.y = safeHeight;
        StartKitchenTomatoPlacement(outsideTarget);
        interactionStatus = "Hebe Tomate vor dem Pfannenrand an";
        yield return new WaitForSeconds(kitchenTomatoPlacementSeconds);

        // 2. Dann horizontal oberhalb der Oeffnung zur Mitte bewegen.
        StartKitchenTomatoPlacement(new Vector3(potBounds.center.x, safeHeight, potBounds.center.z));
        interactionStatus = "Fuehre Tomate sanft ueber die Pfannenöffnung";
        yield return new WaitForSeconds(kitchenTomatoPlacementSeconds);
        yield return new WaitForFixedUpdate();

        if (!TryGetPickupBounds(fruit, out fruitBounds) ||
            fruitBounds.min.y < potBounds.max.y + 0.02f)
        {
            interactionStatus = "Tomate noch nicht sicher oberhalb des Pfannenrandes";
            yield break;
        }

        // 3. KEIN Wurfimpuls: normale Schwerkraft und gemessene Landung.
        kitchenTomatoCarry = false;
        kitchenTomatoPlacing = false;
        heldTomato = null;
        reaching = false;
        fruit.enabled = true;
        fruit.ReleaseNpcHarvest();
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        RestoreKitchenTomatoVesselCollisions();
        body.WakeUp();
        interactionStatus = "Tomate ohne Wurfimpuls losgelassen - warte auf Landung";

        float settled = 0f;
        for (float elapsed = 0f; elapsed < kitchenTomatoSettleTimeout && settled < 0.5f;
            elapsed += Time.deltaTime)
        {
            settled = pot.ContainsBody(body) && body.linearVelocity.sqrMagnitude < 0.04f
                ? settled + Time.deltaTime : 0f;
            yield return null;
        }
        if (settled < 0.5f)
        {
            interactionStatus = "Tomate nicht sicher in der Pfanne gelandet - Ablegeposition pruefen";
            yield break;
        }
        kitchenTomatoPlaced = true;
        interactionStatus = "Tomate sicher in der Pfanne";
    }

    // Gemeinsame Grundlage fuer Bodenobjekte: Collider statt Pivot,
    // mehrere NavMesh-Standpunkte und eine reale Handabstands-Messung.
    // Vorerst ausschliesslich fuer ausgeschuettete Tomaten verwendet.
    // Die erfolgreiche F-Test-Variante benutzt die tatsaechlich sichtbaren
    // Objektgrenzen. Nicht auf attachedRigidbody filtern: Bei importierten
    // Tomaten koennen die Collider auf untergeordneten Meshes liegen.
    private bool TryGetPickupBounds(PickupInteractable item, out Bounds bounds)
    {
        bounds = default;
        if (item == null) return false;
        bool found = false;
        foreach (Collider shape in item.GetComponentsInChildren<Collider>())
        {
            if (!shape.enabled || shape.isTrigger) continue;
            if (!found) { bounds = shape.bounds; found = true; }
            else bounds.Encapsulate(shape.bounds);
        }
        // Genau wie GetBounds() im funktionierenden ZombieTomatoPickup:
        // Renderer als Fallback, wenn der importierte Frucht-Collider fehlt.
        if (!found)
            foreach (Renderer part in item.GetComponentsInChildren<Renderer>())
            {
                if (!part.enabled) continue;
                if (!found) { bounds = part.bounds; found = true; }
                else bounds.Encapsulate(part.bounds);
            }
        return found;
    }

    private bool TryGetPickupSurface(PickupInteractable item, Vector3 handPoint,
        out Vector3 surfacePoint, out float distance)
    {
        surfacePoint = default;
        distance = float.PositiveInfinity;
        if (item == null) return false;
        foreach (Collider shape in item.GetComponentsInChildren<Collider>())
        {
            if (!shape.enabled || shape.isTrigger) continue;
            Vector3 point = shape.ClosestPoint(handPoint);
            float gap = Vector3.Distance(handPoint, point);
            if (gap >= distance) continue;
            distance = gap;
            surfacePoint = point;
        }
        if (!float.IsInfinity(distance)) return true;
        // Bei nicht vorhandenen aktiven Collidern trotzdem ein visuelles
        // Griffziel finden; eine Distanz von Infinity darf kein Scheitern
        // der eigentlichen Standplatzsuche verschleiern.
        if (!TryGetPickupBounds(item, out Bounds bounds)) return false;
        surfacePoint = bounds.ClosestPoint(handPoint);
        distance = Vector3.Distance(handPoint, surfacePoint);
        return true;
    }

    // Gleiche NavMesh-Standpunktsuche wie im ERFOLGREICHEN F-Test:
    // 24 Richtungen x 6 Ringe, ohne vorgaengige strenge Greifdistanz-Sperre.
    // Den realen Hand-/Objektabstand pruefen wir ERST nach dem Hinlaufen.
    private bool TryPlanFloorPickup(PickupInteractable item,
        System.Collections.Generic.List<Vector3> previouslyTried,
        out NavMeshPath bestPath, out Vector3 bestStand, out string diagnostic)
    {
        bestPath = null;
        bestStand = default;
        diagnostic = "";
        if (agent == null || !agent.isOnNavMesh)
        {
            diagnostic = "Zombie nicht auf NavMesh";
            return false;
        }
        if (!TryGetPickupBounds(item, out Bounds bounds))
        {
            diagnostic = "Keine aktiven Collider oder Renderer an Tomate";
            return false;
        }

        float bestScore = float.PositiveInfinity;
        float floorY = agent.nextPosition.y;
        int navMeshHits = 0, sameFloorHits = 0, completePaths = 0, skippedTried = 0;
        for (int direction = 0; direction < 24; direction++)
        {
            Vector3 away = Quaternion.Euler(0f, direction * 15f, 0f) * Vector3.forward;
            float edge = Mathf.Abs(away.x) * bounds.extents.x +
                         Mathf.Abs(away.z) * bounds.extents.z;
            for (int ring = 0; ring < 6; ring++)
            {
                Vector3 candidate = bounds.center +
                    away * (edge + agent.radius + 0.06f + ring * 0.1f);
                candidate.y = floorY;
                // Absichtlich identisch zum erfolgreichen Test:
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                        0.25f, agent.areaMask)) continue;
                navMeshHits++;
                if (Mathf.Abs(hit.position.y - floorY) > 0.2f) continue;
                sameFloorHits++;

                bool alreadyTried = false;
                foreach (Vector3 previous in previouslyTried)
                    if (Vector3.Distance(previous, hit.position) < 0.17f)
                    { alreadyTried = true; break; }
                if (alreadyTried) { skippedTried++; continue; }

                NavMeshPath path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path) ||
                    path.status != NavMeshPathStatus.PathComplete ||
                    path.corners.Length == 0) continue;
                completePaths++;

                Vector3 nearest = bounds.ClosestPoint(
                    new Vector3(hit.position.x, bounds.center.y, hit.position.z));
                float horizontalGap = Vector3.ProjectOnPlane(
                    hit.position - nearest, Vector3.up).magnitude;
                float length = 0f;
                Vector3 previousCorner = agent.nextPosition;
                foreach (Vector3 corner in path.corners)
                {
                    length += Vector3.Distance(previousCorner, corner);
                    previousCorner = corner;
                }
                // Kein harter Abbruch fuer horizontalGap > 0.65 m:
                // Die groessere Distanz ist nur eine schlechte Bewertung.
                float tooFar = Mathf.Max(0f, horizontalGap - kitchenTomatoPickupDistance);
                float tooClose = Mathf.Max(0f, agent.radius * 0.6f - horizontalGap);
                float score = tooFar * 100f + tooClose * 30f +
                              horizontalGap * 3f + length * 0.2f + ring * 0.1f;
                if (score >= bestScore) continue;
                bestScore = score;
                bestPath = path;
                bestStand = hit.position;
            }
        }
        diagnostic = $"NavMesh {navMeshHits}, Etage {sameFloorHits}, " +
                     $"Pfade {completePaths}, verworfen (schon versucht) {skippedTried}, " +
                     $"Tomate {bounds.center}, Zombie {agent.nextPosition}";
        return bestPath != null;
    }

    private IEnumerator ApproachAndReachFloorPickup(PickupInteractable item)
    {
        groundPickupReady = false;
        groundPickupReaching = false;
        if (item == null || !item.isActiveAndEnabled || item.IsHeld)
        {
            interactionStatus = "Bodenobjekt nicht verfuegbar";
            yield break;
        }
        var tried = new System.Collections.Generic.List<Vector3>();
        float lastGap = float.PositiveInfinity;
        string lastSearchDiagnostic = "";
        try
        {
            for (int attempt = 0; attempt < groundPickupStanceAttempts; attempt++)
            {
                if (item == null || !item.isActiveAndEnabled || item.IsHeld) yield break;
                if (!TryPlanFloorPickup(item, tried, out NavMeshPath path,
                        out Vector3 stand, out lastSearchDiagnostic))
                {
                    interactionStatus = "Kein weiterer Standplatz: " + lastSearchDiagnostic;
                    break;
                }
                tried.Add(stand);
                interactionStatus = $"Gehe zum Bodenobjekt (Versuch {attempt + 1}/{groundPickupStanceAttempts})";
                float originalStoppingDistance = agent.stoppingDistance;
                try
                {
                    // Die Standard-Ankunftstoleranz von 0.3 m ist fuer kleine Objekte zu gross.
                    agent.stoppingDistance = 0.03f;
                    yield return WalkToKitchenStation(item.transform, interactionStatus, path);
                }
                finally { agent.stoppingDistance = originalStoppingDistance; }
                if (!kitchenRouteReached ||
                    Vector3.Distance(agent.nextPosition, stand) > 0.18f) continue;
                if (!TryGetPickupSurface(item, FingerGripPosition, out Vector3 firstTarget, out _))
                {
                    interactionStatus = "Kein Griffpunkt: Tomate hat weder Collider noch Renderer";
                    break;
                }

                groundPickupReaching = true;
                reaching = true;
                groundPickupBendAngle = Mathf.Clamp(
                    (rightUpperArm.position.y - firstTarget.y) * 82f, 0f, groundPickupMaxBend);
                Vector3 startHand = FingerGripPosition;
                interactionStatus = "Beuge mich und greife das Bodenobjekt";
                for (float t = 0f; t < kitchenTomatoPickupSeconds; t += Time.deltaTime)
                {
                    if (!TryGetPickupSurface(item, FingerGripPosition, out Vector3 surface, out _)) yield break;
                    harvestHandTarget = Vector3.Lerp(startHand, surface,
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / kitchenTomatoPickupSeconds)));
                    yield return null;
                }
                // Wie im erfolgreichen Einzeltest: Standposition + Objekthoehe
                // entscheiden; die stilisierte Arm-IK darf 20-30 cm Restfehler haben.
                // Der Gegenstand wird erst nach der Greifpause sanft angehoben.
                for (float t = 0f; t < 0.15f; t += Time.deltaTime)
                {
                    if (!TryGetPickupSurface(item, FingerGripPosition, out Vector3 surface, out _)) yield break;
                    harvestHandTarget = surface;
                    yield return null;
                }
                if (!TryGetPickupBounds(item, out Bounds pickupBounds)) yield break;
                Vector3 closest = pickupBounds.ClosestPoint(transform.position);
                float horizontalDistance = Vector3.ProjectOnPlane(closest - transform.position, Vector3.up).magnitude;
                float heightAboveFeet = pickupBounds.center.y - agent.nextPosition.y;
                TryGetPickupSurface(item, FingerGripPosition, out _, out lastGap);
                if (horizontalDistance <= kitchenTomatoPickupDistance &&
                    heightAboveFeet <= kitchenTomatoMaxPickupHeight)
                {
                    groundPickupReady = true;
                    interactionStatus = $"Bodenobjekt greifbar: horizontal {horizontalDistance:F2} m, " +
                        $"Hoehe {heightAboveFeet:F2} m, Fingerabstand {lastGap:F2} m";
                    yield break;
                }

                // Nur bei ungeeignetem Standpunkt einen neuen Versuch unternehmen.
                groundPickupReaching = false;
                reaching = false;
                yield return null;
            }
            // +unendlich bedeutet nicht, dass die Hand unendlich weit weg ist:
            // Wenn noch kein Standplatz besucht wurde, gab es KEINE Handmessung.
            string gripInfo = float.IsInfinity(lastGap)
                ? "Handabstand nicht gemessen"
                : $"Handabstand {lastGap:F2} m";
            interactionStatus = $"Bodenobjekt nicht greifbar - {gripInfo}; " +
                $"Standpositionen getestet: {tried.Count}. {lastSearchDiagnostic}";
            Debug.LogWarning("[Zombie Bodenaufnahme] " + interactionStatus, this);
        }
        finally
        {
            // Bei Erfolg bleiben die Pose und die Hand kurz bis zur Uebernahme aktiv.
            if (!groundPickupReady)
            {
                groundPickupReaching = false;
                reaching = false;
            }
        }
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
    private IEnumerator ApproachCookingPot(
        System.Collections.Generic.List<Vector3> excludedStances = null)
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

                // Nicht erneut denselben Standplatz waehlen, wenn das Greifen
                // von dort bereits fehlgeschlagen ist. Nur horizontal messen:
                // die Hoehenpruefung erfolgt unveraendert im naechsten Schritt.
                bool excluded = false;
                if (excludedStances != null)
                    foreach (Vector3 previousStance in excludedStances)
                        if (Vector3.ProjectOnPlane(
                            hit.position - previousStance, Vector3.up).sqrMagnitude <
                            cookingPotRetryStanceSeparation * cookingPotRetryStanceSeparation)
                        {
                            excluded = true;
                            break;
                        }
                if (excluded) continue;

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
