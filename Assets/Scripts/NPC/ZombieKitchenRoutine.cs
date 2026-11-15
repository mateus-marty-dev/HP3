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


    private IEnumerator WalkToKitchenStation(
        Transform station,
        string description)
    {
        kitchenRouteReached = false;
        interactionStatus = description;

        if (station == null ||
            !TryApproach(station, out NavMeshPath path) ||
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


    // Bring the basket upstairs, put it down, then pick up the cooking pot and wait.
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
            deliveryStep = DeliveryStep.Finished;
            interactionText = "Kochtopf aufgenommen";
            interactionStatus = "Kochtopf in den Haenden - warte auf den naechsten Schritt";
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
        potReaching = true;
        interactionStatus = "Greife den Kochtopf mit beiden Haenden";
        yield return new WaitForSeconds(0.6f);
        GetPotGrips(out Vector3 leftGrip, out Vector3 rightGrip);
        if (Vector3.Distance(leftHand.position + leftHand.up * FingerGripDistance, leftGrip) > 0.25f ||
            Vector3.Distance(FingerGripPosition, rightGrip) > 0.25f)
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
        potLiftStart = potBody.position;
        potLiftStarted = Time.time;
        potHeld = true;
        potReaching = false;
        potBody.GetComponent<BucketContents>()?.BeginCarry(null);
        yield return new WaitForSeconds(0.8f);
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

    private IEnumerator ApproachCookingPot()
    {
        interactionReady = false;
        if (!agent.isOnNavMesh) yield break;
        Bounds bounds = PotBounds();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        NavMeshPath bestPath = null;
        Vector3 bestPosition = default;
        float bestLength = float.PositiveInfinity;
        float leftReach = Vector3.Distance(leftUpperArm.position, leftForearm.position)
            + Vector3.Distance(leftForearm.position, leftHand.position);
        float rightReach = Vector3.Distance(rightUpperArm.position, rightForearm.position)
            + Vector3.Distance(rightForearm.position, rightHand.position);
        // Search outside the pot footprint, on the current floor, and only accept complete paths.
        for (int direction = 0; direction < 32; direction++)
        {
            Vector3 away = Quaternion.Euler(0f, direction * 360f / 32f, 0f) * Vector3.forward;
            float edge = Mathf.Abs(away.x) * bounds.extents.x + Mathf.Abs(away.z) * bounds.extents.z;
            for (int ring = 0; ring < 5; ring++)
            {
                Vector3 candidate = bounds.center + away * (edge + agent.radius + 0.04f + ring * 0.1f);
                candidate.y = agent.nextPosition.y;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.18f, filter) ||
                    Mathf.Abs(hit.position.y - agent.nextPosition.y) > 0.25f) continue;
                Vector3 closest = bounds.ClosestPoint(new Vector3(hit.position.x, bounds.center.y, hit.position.z));
                if (Vector2.Distance(new Vector2(closest.x, closest.z),
                    new Vector2(hit.position.x, hit.position.z)) < agent.radius + 0.02f) continue;
                Vector3 forward = Vector3.ProjectOnPlane(bounds.center - hit.position, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.5f) continue;
                Quaternion facing = Quaternion.LookRotation(forward);
                Quaternion turn = facing * Quaternion.Inverse(transform.rotation);
                Vector3 root = hit.position + Vector3.up * agent.baseOffset;
                Vector3 shoulderLeft = root + turn * (leftUpperArm.position - transform.position);
                Vector3 shoulderRight = root + turn * (rightUpperArm.position - transform.position);
                Vector3 across = facing * Vector3.right;
                float width = Mathf.Abs(across.x) * bounds.extents.x + Mathf.Abs(across.z) * bounds.extents.z;
                Vector3 gripCenter = bounds.center + Vector3.up * bounds.extents.y * 0.5f;
                Vector3 left = gripCenter - across * width;
                Vector3 right = gripCenter + across * width;
                if (Vector3.Dot(left - right, shoulderLeft - shoulderRight) < 0f)
                    (left, right) = (right, left);
                Vector3 wristOffset = forward * FingerGripDistance;
                if (Vector3.Distance(shoulderLeft, left - wristOffset) > leftReach + 0.05f ||
                    Vector3.Distance(shoulderRight, right - wristOffset) > rightReach + 0.05f) continue;
                var path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete ||
                    path.corners.Length == 0) continue;
                float length = 0f;
                Vector3 previous = agent.nextPosition;
                foreach (Vector3 corner in path.corners)
                {
                    length += Vector3.Distance(previous, corner);
                    previous = corner;
                }
                if (length >= bestLength) continue;
                bestLength = length;
                bestPath = path;
                bestPosition = hit.position;
            }
        }
        if (bestPath == null)
        {
            interactionStatus = "Kein erreichbarer Standplatz in Griffweite des Kochtopfs";
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
        Vector3 center = (leftUpperArm.position + rightUpperArm.position) * 0.5f
            + transform.forward * (armLength * 0.6f) - transform.up * (armLength * 0.35f);
        Quaternion rotation = transform.rotation * potRelativeRotation;
        Vector3 position = center - rotation * Vector3.Scale(potCenterLocal, potBody.transform.lossyScale);
        position = Vector3.Lerp(potLiftStart, position, Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((Time.time - potLiftStarted) / 0.8f)));
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
        if ((!potHeld && !potReaching) || potBody == null) return;
        GetPotGrips(out Vector3 left, out Vector3 right);
        Quaternion rotation = Quaternion.LookRotation(-transform.up, transform.forward);
        Vector3 offset = rotation * Vector3.up * FingerGripDistance;
        PoseCarryingArm(leftUpperArm, leftForearm, leftHand, left - offset);
        PoseCarryingArm(rightUpperArm, rightForearm, rightHand, right - offset);
        leftHand.rotation = rotation;
        rightHand.rotation = rotation;
        if (potHeld) potBody.GetComponent<BucketContents>()?.RenderCarry(potBody.position, potBody.rotation);
    }

    private void ReleaseCookingPot()
    {
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
