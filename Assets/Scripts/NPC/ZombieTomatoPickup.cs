using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Isolierter HP.3-Test: Eine Tomate aufnehmen und in einen Korb werfen.
/// Auf demselben Zombie muss ZombieBehaviour fuer diesen Test DEAKTIVIERT sein,
/// damit nicht zwei Komponenten denselben NavMeshAgent und dieselben Arme steuern.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(NavMeshAgent))]
public sealed class ZombieTomatoPickup : MonoBehaviour
{
    [Header("Testobjekte")]
    [Tooltip("Hier die Tomate als GameObject aus der Hierarchy hineinziehen. PickupInteractable wird erst im Play Mode gesucht.")]
    [SerializeField] private GameObject tomatoObject;
    [SerializeField] private Transform basket;

    private PickupInteractable tomato;

    [Header("Auftrag starten (F-Taste)")]
    [Tooltip("Petrusilius. Wird automatisch ueber PlayerInteraction gesucht, falls leer.")]
    [SerializeField] private Transform player;
    [Min(0.5f)][SerializeField] private float interactionDistance = 2.5f;
    [Min(0.5f)][SerializeField] private float maxHeightDifference = 1.8f;

    [Header("Navigation und Greiftoleranz")]
    [Min(0.1f)][SerializeField] private float walkSpeed = 1.0f;
    [Min(0.2f)][SerializeField] private float pickupDistance = 0.65f;
    [Min(0.1f)][SerializeField] private float maximumPickupHeight = 0.9f;
    [Header("Tatsaechlicher Handkontakt")]
    [Tooltip("Nur visueller Richtwert. Das Aufnehmen richtet sich nach dem erreichbaren Standplatz und der Gegenstandshoehe, nicht nach millimetergenauer IK.")]
    [Range(0.02f, 0.4f)][SerializeField] private float handContactTolerance = 0.22f;

    [Header("Bewegung")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform rightUpperArm;
    [SerializeField] private Transform rightForearm;
    [SerializeField] private Transform rightHand;
    [Range(0f, 75f)][SerializeField] private float bendDegrees = 60f;
    [Min(0.1f)][SerializeField] private float pickupSeconds = 0.75f;
    [Header("Tomate sanft in den Korb legen")]
    [Min(0.05f)][SerializeField] private float heightAboveBasketRim = 0.15f;
    [Min(0.1f)][SerializeField] private float basketPlacementSeconds = 0.7f;
    [Min(1f)][SerializeField] private float basketSettleTimeout = 6f;

    [Header("Diagnose (Play Mode)")]
    [SerializeField] private string status = "Bereit";

    private NavMeshAgent agent;
    private Animator animator;
    private Rigidbody tomatoBody;
    private bool originalPickupEnabled;
    private bool originalKinematic;
    private bool originalGravity;
    private bool carrying;
    private bool reaching;
    private bool throwing;
    private float carryStarted;
    private Vector3 pickupStartPosition;
    private Vector3 reachTarget;
    private Coroutine currentRoutine;
    private bool arrived;
    private Vector3 basketPlacementTarget;
    private Vector3 basketPlacementStart;
    private float basketPlacementStarted;

    // Der dynamische Korb darf nicht von der kinematisch getragenen
    // Tomate weggedrueckt werden. Wir merken uns NUR selbst geaenderte Paare.
    private readonly List<Collider> collisionFruit = new List<Collider>();
    private readonly List<Collider> collisionBasket = new List<Collider>();

    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int Gait = Animator.StringToHash("Gait");
    private static readonly int GaitSpeed = Animator.StringToHash("GaitSpeed");

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        FindBones();
        FindPlayer();
    }

    private void Start()
    {
        // Kein Autostart: Der Test beginnt nur per F-Taste in Zombie-Naehe.
        status = player != null
            ? "Bereit: Beim Zombie F druecken"
            : "Player fehlt: Petrusilius im Inspector zuweisen";
    }

    private void FindPlayer()
    {
        if (player != null) return;

        // HP.3 verwendet PlayerInteraction fuer die E-Taste.
        // Wir verwenden dieselbe Spielfigur, aber ausschliesslich die F-Taste.
        PlayerInteraction interaction = FindFirstObjectByType<PlayerInteraction>();
        if (interaction != null) player = interaction.transform;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F) || currentRoutine != null) return;

        FindPlayer();
        if (player == null)
        {
            status = "Player nicht gefunden: Petrusilius zuweisen";
            return;
        }

        Vector3 difference = player.position - transform.position;
        float horizontalDistance = Vector3.ProjectOnPlane(difference, Vector3.up).magnitude;
        if (horizontalDistance > interactionDistance ||
            Mathf.Abs(difference.y) > maxHeightDifference)
        {
            status = $"Zu weit vom Zombie entfernt ({horizontalDistance:F2} m)";
            return;
        }

        StartTest();
    }

    [ContextMenu("TEST: Eine Tomate aufnehmen und in den Korb werfen")]
    public void StartTest()
    {
        if (!Application.isPlaying || currentRoutine != null) return;
        currentRoutine = StartCoroutine(PickupAndThrow());
    }

    private void FindBones()
    {
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
        {
            if (spine == null && bone.name == "Spine") spine = bone;
            if (rightUpperArm == null && bone.name == "shoulder.R.001") rightUpperArm = bone;
            if (rightForearm == null && bone.name == "forearm.R") rightForearm = bone;
            if (rightHand == null && bone.name == "hand.R") rightHand = bone;
        }
    }

    /// <summary>
    /// Die Pflanzen in HP.3 erhalten PickupInteractable erst bei Reife.
    /// Deshalb wird im Inspector ein GameObject statt der Komponente gespeichert.
    /// </summary>
    private bool ResolveTomato()
    {
        if (tomatoObject == null)
        {
            status = "Tomato Object fehlt: GameObject aus der Hierarchy hineinziehen";
            return false;
        }

        tomato = tomatoObject.GetComponent<PickupInteractable>()
            ?? tomatoObject.GetComponentInParent<PickupInteractable>(true)
            ?? tomatoObject.GetComponentInChildren<PickupInteractable>(true);

        if (tomato == null)
        {
            // ZombieBehaviour ist fuer diesen Einzeltest deaktiviert und
            // reift die Tomaten nicht automatisch. Das uebernimmt dieser Test.
            PlantGrowth plant = tomatoObject.GetComponentInParent<PlantGrowth>(true);
            if (plant != null)
            {
                plant.RipenForNpcTest();
                tomato = tomatoObject.GetComponent<PickupInteractable>()
                    ?? tomatoObject.GetComponentInParent<PickupInteractable>(true)
                    ?? tomatoObject.GetComponentInChildren<PickupInteractable>(true);
            }
        }

        if (tomato == null)
        {
            status = "Tomate besitzt noch kein PickupInteractable. " +
                     "Bitte echte Frucht aus Harvest Fruits waehlen.";
            return false;
        }

        return true;
    }

    private IEnumerator PickupAndThrow()
    {
        try
        {
            if (GetComponent<ZombieBehaviour>()?.enabled == true)
            {
                status = "ZombieBehaviour zuerst im Inspector deaktivieren";
                Debug.LogWarning(status, this);
                yield break;
            }
            if (basket == null)
            {
                status = "Basket fehlt: Korb aus der Hierarchy zuweisen";
                yield break;
            }
            if (!ResolveTomato()) yield break;
            if (!tomato.TryGetComponent(out tomatoBody))
            {
                status = "Tomate besitzt keinen Rigidbody";
                yield break;
            }
            if (!tomato.isActiveAndEnabled || tomato.IsHeld)
            {
                status = "Tomate ist bereits aufgenommen oder nicht aktiv";
                yield break;
            }
            agent.radius = 0.25f;
            agent.height = 1.8f;
            if (!agent.isOnNavMesh &&
                NavMesh.SamplePosition(transform.position, out NavMeshHit startingGround, 1.0f, NavMesh.AllAreas))
                agent.Warp(startingGround.position);
            if (!agent.isOnNavMesh)
            {
                status = "Zombie steht nicht auf einem NavMesh";
                yield break;
            }

            agent.speed = walkSpeed;
            agent.acceleration = 3f;
            agent.angularSpeed = 180f;
            agent.stoppingDistance = 0.06f;

            Bounds fruitBounds = GetBounds(tomato.transform);
            if (!TryFindPath(fruitBounds, out NavMeshPath fruitPath))
            {
                status = "Kein NavMesh-Standplatz neben der Tomate";
                yield break;
            }
            yield return WalkPath(fruitPath, "Gehe zur Tomate");
            if (!arrived) yield break;

            yield return FaceTowards(tomatoBody.worldCenterOfMass);
            fruitBounds = GetBounds(tomato.transform);
            Vector3 closest = fruitBounds.ClosestPoint(transform.position);
            float horizontalDistance = Vector3.ProjectOnPlane(closest - transform.position, Vector3.up).magnitude;
            float heightAboveFeet = fruitBounds.center.y - agent.nextPosition.y;
            // Absichtlich tolerant: Die Standposition ist entscheidend, nicht die
            // exakte Finger-/Collider-Ueberlappung des stilisierten Rigs.
            if (horizontalDistance > pickupDistance || heightAboveFeet > maximumPickupHeight)
            {
                status = $"Tomate nicht greifbar: horizontal {horizontalDistance:F2} m, Hoehe {heightAboveFeet:F2} m";
                yield break;
            }

            status = "Buecke mich und greife die Tomate";
            reachTarget = tomatoBody.worldCenterOfMass;
            reaching = true;
            yield return new WaitForSeconds(pickupSeconds);
            yield return null; // LateUpdate darf die Armhaltung fertig berechnen.
            if (tomato == null || tomato.IsHeld || !tomato.isActiveAndEnabled) yield break;

            // Die Standposition und die Hoehe der Frucht wurden oben bereits
            // geprueft. Die Arm-IK ist bei einem stilisierten Zombie nicht
            // millimetergenau (z.B. 0.27 m Restabstand beim Greifen).
            // Ein kleiner Restabstand darf darum den Auftrag NICHT abbrechen.
            // Statt die Frucht schlagartig zur Hand zu teleportieren, beginnt
            // sie an ihrem echten Schwerpunkt und wird beim Aufrichten sanft
            // mit der Hand angehoben (PickupGripGoal / FixedUpdate / LateUpdate).
            float handDistance = DistanceHandToTomato();
            bool assistedGrip = handDistance > handContactTolerance;

            // Die Tomate wird fuer das Tragen kinematisch. Collider bleiben aktiv.
            originalPickupEnabled = tomato.enabled;
            originalKinematic = tomatoBody.isKinematic;
            originalGravity = tomatoBody.useGravity;
            tomato.enabled = false;
            tomatoBody.isKinematic = true;
            tomatoBody.useGravity = false;
            tomato.transform.SetParent(null, true);
            IgnoreBasketCollisionsWhileCarrying();
            // Bei der tatsächlichen Kontaktposition beginnen, nicht am Pivot
            // oder an einer bereits hochgezogenen Tomate.
            pickupStartPosition = tomatoBody.worldCenterOfMass;
            carryStarted = Time.time;
            carrying = true;
            reaching = false;
            status = assistedGrip
                ? $"Tomate aufgenommen (Griff-Korrektur {handDistance:F2} m)"
                : "Tomate aufgenommen";
            yield return new WaitForSeconds(pickupSeconds);

            Bounds basketBounds = GetBounds(basket);
            if (!TryFindPath(basketBounds, out NavMeshPath basketPath))
            {
                status = "Kein NavMesh-Standplatz neben dem Korb";
                yield break;
            }
            yield return WalkPath(basketPath, "Trage Tomate zum Korb");
            if (!arrived) yield break;
            yield return FaceTowards(basketBounds.center);

            // Wie in ZombieBehaviour.DropTomatoIntoBasket(): ueber die Oeffnung
            // fuehren und OHNE Wurfimpuls loslassen. Die Tomate faellt durch
            // die Schwerkraft, erst nachdem der Korbrand sicher ueberquert ist.
            basketBounds = GetBounds(basket);
            Bounds tomatoBounds = GetBounds(tomato.transform);
            float fruitRadius = Mathf.Max(tomatoBounds.extents.x, tomatoBounds.extents.y,
                tomatoBounds.extents.z);
            float safeHeight = basketBounds.max.y + fruitRadius + heightAboveBasketRim;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - basketBounds.center,
                Vector3.up).normalized;
            if (away.sqrMagnitude < 0.001f) away = -transform.forward;
            float edge = Mathf.Abs(away.x) * basketBounds.extents.x +
                Mathf.Abs(away.z) * basketBounds.extents.z;

            // Schritt 1: Auf der dem Zombie zugewandten Seite ANHEBEN.
            // Kollisionen zum Korb sind beim Tragen bewusst deaktiviert.
            Vector3 outsideTarget = basketBounds.center +
                away * (edge + fruitRadius + 0.18f);
            outsideTarget.y = safeHeight;
            StartBasketPlacement(outsideTarget);
            status = "Hebe Tomate vor dem Korbrand an";
            yield return new WaitForSeconds(basketPlacementSeconds);

            // Schritt 2: Erst oberhalb des Korbrandes zur Mitte fuehren.
            StartBasketPlacement(new Vector3(basketBounds.center.x, safeHeight,
                basketBounds.center.z));
            status = "Fuehre Tomate ueber die Korboeffnung";
            yield return new WaitForSeconds(basketPlacementSeconds);
            yield return new WaitForFixedUpdate();

            // Kein Verlassen einer kinematisch gefuehrten Tomate innerhalb
            // des Korbrandes: sie soll oberhalb frei fallen.
            float fruitBottom = GetBounds(tomato.transform).min.y;
            if (fruitBottom < basketBounds.max.y + 0.02f)
            {
                status = $"Tomate noch zu tief ueber dem Korb ({fruitBottom - basketBounds.max.y:F2} m)";
                yield break;
            }

            // ReleaseNpcHarvest() stellt die uebliche Physik wieder her.
            // Keine ballistische Zusatzgeschwindigkeit, die den Korb umwirft.
            carrying = false;
            throwing = false;
            tomato.enabled = originalPickupEnabled;
            tomato.ReleaseNpcHarvest();
            tomatoBody.isKinematic = false;
            tomatoBody.useGravity = true;
            tomatoBody.linearVelocity = Vector3.zero;
            tomatoBody.angularVelocity = Vector3.zero;
            RestoreBasketCollisions();
            tomatoBody.WakeUp();
            status = "Tomate sanft losgelassen - warte auf Landung im Korb";

            BucketContents contents = basket.GetComponent<BucketContents>();
            float settled = 0f;
            bool inside = false;
            for (float elapsed = 0f; elapsed < basketSettleTimeout && settled < 0.5f;
                elapsed += Time.deltaTime)
            {
                inside = contents != null && contents.ContainsBody(tomatoBody);
                bool slow = tomatoBody.linearVelocity.sqrMagnitude < 0.04f;
                settled = inside && slow ? settled + Time.deltaTime : 0f;
                yield return null;
            }
            status = settled >= 0.5f ? "Erfolg: Tomate liegt ruhig im Korb" :
                "Abgelegt, aber Tomate nicht sicher im Korb: Ablageposition pruefen";
        }
        finally
        {
            reaching = false;
            throwing = false;
            RestoreBasketCollisions();
            if (carrying && tomatoBody != null)
            {
                // Testabbruch: physikalischen Zustand wiederherstellen.
                carrying = false;
                tomatoBody.isKinematic = originalKinematic;
                tomatoBody.useGravity = originalGravity;
                if (tomato != null) tomato.enabled = originalPickupEnabled;
            }
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (animator != null) animator.SetBool(IsWalking, false);
            currentRoutine = null;
        }
    }

    private void IgnoreBasketCollisionsWhileCarrying()
    {
        RestoreBasketCollisions();
        if (tomato == null || basket == null) return;

        foreach (Collider fruit in tomato.GetComponentsInChildren<Collider>())
        {
            if (fruit == null || !fruit.enabled || fruit.isTrigger) continue;
            foreach (Collider vessel in basket.GetComponentsInChildren<Collider>())
            {
                if (vessel == null || !vessel.enabled || vessel.isTrigger) continue;
                if (Physics.GetIgnoreCollision(fruit, vessel)) continue;
                Physics.IgnoreCollision(fruit, vessel, true);
                collisionFruit.Add(fruit);
                collisionBasket.Add(vessel);
            }
        }
    }

    private void RestoreBasketCollisions()
    {
        for (int i = 0; i < collisionFruit.Count; i++)
        {
            if (collisionFruit[i] != null && collisionBasket[i] != null)
                Physics.IgnoreCollision(collisionFruit[i], collisionBasket[i], false);
        }
        collisionFruit.Clear();
        collisionBasket.Clear();
    }

    private Bounds GetBounds(Transform item)
    {
        Bounds bounds = new Bounds(item.position, Vector3.zero);
        bool any = false;
        foreach (Collider collider in item.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            if (!any) { bounds = collider.bounds; any = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        if (!any)
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        return bounds;
    }

    private bool TryFindPath(Bounds target, out NavMeshPath best)
    {
        best = null;
        float bestScore = float.PositiveInfinity;
        float floorY = agent.nextPosition.y;
        for (int direction = 0; direction < 24; direction++)
        {
            Vector3 away = Quaternion.Euler(0f, direction * 15f, 0f) * Vector3.forward;
            float edge = Mathf.Abs(away.x) * target.extents.x + Mathf.Abs(away.z) * target.extents.z;
            for (int ring = 0; ring < 6; ring++)
            {
                Vector3 candidate = target.center + away * (edge + agent.radius + 0.06f + ring * 0.1f);
                candidate.y = floorY;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.25f, agent.areaMask)) continue;
                if (Mathf.Abs(hit.position.y - floorY) > 0.2f) continue;
                NavMeshPath path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path) ||
                    path.status != NavMeshPathStatus.PathComplete || path.corners.Length == 0) continue;
                float length = 0f;
                Vector3 previous = agent.nextPosition;
                foreach (Vector3 corner in path.corners)
                {
                    length += Vector3.Distance(previous, corner);
                    previous = corner;
                }
                float score = length + ring * 0.25f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = path;
            }
        }
        return best != null;
    }

    private IEnumerator WalkPath(NavMeshPath path, string message)
    {
        arrived = false;
        status = message;
        Vector3 goal = path.corners[path.corners.Length - 1];
        agent.isStopped = false;
        if (!agent.SetPath(path)) yield break;
        Vector3 previous = agent.nextPosition;
        float stillSeconds = 0f;
        float totalSeconds = 0f;
        while (agent.isOnNavMesh && totalSeconds < 90f)
        {
            float remaining = agent.remainingDistance;
            bool validRemaining = !float.IsInfinity(remaining) && !float.IsNaN(remaining);
            if (!agent.pathPending &&
                (Vector3.Distance(agent.nextPosition, goal) <= 0.15f ||
                 (validRemaining && remaining <= agent.stoppingDistance + 0.09f)))
            {
                arrived = true;
                break;
            }
            if (!agent.pathPending && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)) break;
            if (Vector3.Distance(previous, agent.nextPosition) > 0.03f)
            {
                previous = agent.nextPosition;
                stillSeconds = 0f;
            }
            else stillSeconds += Time.deltaTime;
            if (stillSeconds > 10f) break;
            animator.SetFloat(Gait, 1f);
            animator.SetBool(IsWalking, agent.velocity.magnitude > 0.01f);
            animator.SetFloat(GaitSpeed, agent.velocity.magnitude /
                Mathf.Max(0.001f, 0.4548387f * transform.TransformVector(Vector3.forward).magnitude));
            totalSeconds += Time.deltaTime;
            yield return null;
        }
        agent.isStopped = true;
        agent.ResetPath();
        animator.SetBool(IsWalking, false);
        if (!arrived) status = "Navigation unterbrochen: " + message;
    }

    private IEnumerator FaceTowards(Vector3 target)
    {
        bool initial = agent.updateRotation;
        agent.updateRotation = false;
        Vector3 forward = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
        if (forward.sqrMagnitude > 0.001f)
        {
            Quaternion facing = Quaternion.LookRotation(forward);
            float elapsed = 0f;
            while (Quaternion.Angle(transform.rotation, facing) > 2f && elapsed < 3f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 160f * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        agent.updateRotation = initial;
    }

    private Vector3 CarryHandTarget()
    {
        if (rightUpperArm != null)
            return rightUpperArm.position + transform.forward * 0.28f - transform.up * 0.16f;
        return transform.position + transform.up * 1.1f + transform.forward * 0.3f;
    }

    private float PickupLiftProgress()
    {
        return Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((Time.time - carryStarted) / Mathf.Max(0.1f, pickupSeconds)));
    }

    private void StartBasketPlacement(Vector3 destination)
    {
        // Hand und Tomate fahren gemeinsam entlang derselben sanften Bahn.
        basketPlacementStart = PickupGripGoal();
        basketPlacementTarget = destination;
        basketPlacementStarted = Time.time;
        throwing = true;
    }

    private Vector3 PickupGripGoal()
    {
        if (throwing)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                (Time.time - basketPlacementStarted) / basketPlacementSeconds));
            return Vector3.Lerp(basketPlacementStart, basketPlacementTarget, progress);
        }
        // Die Hand UND die Frucht durchlaufen dieselbe Hebebewegung.
        return Vector3.Lerp(pickupStartPosition, CarryHandTarget(), PickupLiftProgress());
    }

    private float DistanceHandToTomato()
    {
        if (tomato == null || rightHand == null) return float.PositiveInfinity;
        float nearest = float.PositiveInfinity;
        Vector3 handPoint = rightHand.position;
        foreach (Collider shape in tomato.GetComponentsInChildren<Collider>())
        {
            if (!shape.enabled || shape.isTrigger) continue;
            nearest = Mathf.Min(nearest,
                Vector3.Distance(handPoint, shape.ClosestPoint(handPoint)));
        }
        // Nur wenn das Objekt keinen aktiven Collider hat, den Schwerpunkt verwenden.
        if (float.IsPositiveInfinity(nearest) && tomatoBody != null)
            nearest = Vector3.Distance(handPoint, tomatoBody.worldCenterOfMass);
        return nearest;
    }

    private void FixedUpdate()
    {
        if (!carrying || tomatoBody == null) return;
        Vector3 goal = PickupGripGoal();
        // Der Rigidbody-Mittelpunkt folgt genau demselben Ziel wie die Hand.
        // Der Pivot darf deshalb auch ausserhalb der Tomate liegen.
        Vector3 massOffset = tomatoBody.worldCenterOfMass - tomatoBody.position;
        tomatoBody.MovePosition(goal - massOffset);
    }

    private void LateUpdate()
    {
        // Beim Greifen buecken und WAeHREND des Anhebens wieder aufrichten.
        float bendWeight = reaching ? 1f :
            carrying && !throwing ? 1f - PickupLiftProgress() : 0f;
        if (spine != null && bendWeight > 0f)
            spine.rotation = Quaternion.AngleAxis(bendDegrees * bendWeight, transform.right)
                * spine.rotation;

        if (rightUpperArm == null || rightForearm == null || rightHand == null) return;
        if (!reaching && !carrying) return;
        Vector3 goal = reaching ? reachTarget : PickupGripGoal();
        PoseArm(goal);
    }

    private void PoseArm(Vector3 target)
    {
        Vector3 shoulder = rightUpperArm.position;
        float upper = Vector3.Distance(shoulder, rightForearm.position);
        float lower = Vector3.Distance(rightForearm.position, rightHand.position);
        if (upper < 0.001f || lower < 0.001f) return;
        Vector3 dir = (target - shoulder).normalized;
        if (dir.sqrMagnitude < 0.001f) return;
        float d = Mathf.Clamp(Vector3.Distance(shoulder, target),
            Mathf.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
        float along = (upper * upper - lower * lower + d * d) / (2f * d);
        float bend = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
        Vector3 pole = Vector3.ProjectOnPlane(-transform.up, dir).normalized;
        if (pole.sqrMagnitude < 0.001f) pole = Vector3.ProjectOnPlane(transform.forward, dir).normalized;
        Vector3 elbow = shoulder + dir * along + pole * bend;
        rightUpperArm.rotation = Quaternion.FromToRotation(rightForearm.position - shoulder, elbow - shoulder) *
            rightUpperArm.rotation;
        Vector3 wrist = shoulder + dir * d;
        rightForearm.rotation = Quaternion.FromToRotation(rightHand.position - rightForearm.position,
            wrist - rightForearm.position) * rightForearm.rotation;
    }

    private void OnDisable()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        RestoreBasketCollisions();
        // StopCoroutine ruft finally nicht in jedem Pfad garantiert sofort auf:
        // Sicherung fuer im Test gehaltene Tomaten.
        if (carrying && tomatoBody != null)
        {
            carrying = false;
            tomatoBody.isKinematic = originalKinematic;
            tomatoBody.useGravity = originalGravity;
            if (tomato != null) tomato.enabled = originalPickupEnabled;
        }
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        if (animator != null) animator.SetBool(IsWalking, false);
    }
}
