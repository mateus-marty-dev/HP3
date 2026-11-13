using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public partial class ZombieBehaviour
{
    private Rigidbody cookingPan;
    private PickupInteractable cookingPanPickup;
    private bool panPickupWasEnabled;
    private bool panCarried;
    private bool dippingPan;
    private Vector3 dipPosition;
    private Quaternion panRotation;
    private Vector3 panCenterOffset;
    private bool kitchenRouteReached;

    private IEnumerator WalkToKitchenStation(Transform station, string description)
    {
        kitchenRouteReached = false;
        interactionStatus = description;
        if (station == null || !TryApproach(station, out NavMeshPath path))
        {
            interactionStatus = "Kein begehbarer Weg: " + description;
            yield break;
        }
        agent.isStopped = false;
        agent.SetPath(path);
        float lastDistance = float.PositiveInfinity;
        float blockedSeconds = 0f;
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.08f)
        {
            if (!agent.pathPending && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)) break;
            if (agent.remainingDistance < lastDistance - 0.02f)
            {
                lastDistance = agent.remainingDistance;
                blockedSeconds = 0f;
            }
            else blockedSeconds += Time.deltaTime;
            if (blockedSeconds > 15f) break;
            agent.speed = Mathf.Max(0.1f, walkSpeed);
            animator.SetFloat(Gait, 1f);
            animator.SetBool(IsWalking, agent.velocity.magnitude > 0.01f);
            animator.SetFloat(GaitSpeed, agent.velocity.magnitude /
                Mathf.Max(0.001f, 0.4548387f * transform.TransformVector(Vector3.forward).magnitude));
            yield return null;
        }
        kitchenRouteReached = !agent.pathPending && agent.hasPath &&
            agent.remainingDistance <= agent.stoppingDistance + 0.08f;
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

    private IEnumerator CookTomatoSoup()
    {
        harvesting = true;
        // This coroutine owns navigation until completion or a reported blocker.
        deliveryFinished = true;
        DoorInteractable door = deliveryDoor != null ? deliveryDoor.GetComponent<DoorInteractable>() : null;
        RainCollector panWater = null;
        foreach (RainCollector vessel in FindObjectsByType<RainCollector>(FindObjectsSortMode.None))
            if (vessel.name == "Pan") { panWater = vessel; break; }
        StoveHeatZone stove = FindFirstObjectByType<StoveHeatZone>();
        StoveFireInteractable fire = FindFirstObjectByType<StoveFireInteractable>();
        HP3_WaterBody pond = FindFirstObjectByType<HP3_WaterBody>();
        if (door == null || panWater == null || stove == null || fire == null || pond == null)
        {
            interactionStatus = "Kuechenablauf: Tuer, Pan, Ofen oder Teich fehlt";
            yield break;
        }
        door.OpenForNpc();
        interactionStatus = "Oeffne die Schlosstuer";
        float doorWait = 0f;
        while (!door.IsOpen && doorWait < 10f) { doorWait += Time.deltaTime; yield return null; }
        if (!door.IsOpen) { interactionStatus = "Tuer konnte nicht geoeffnet werden"; yield break; }

        // The initial surface was baked with a closed door.
        Unity.AI.Navigation.NavMeshSurface surface = navigationObject != null
            ? navigationObject.GetComponent<Unity.AI.Navigation.NavMeshSurface>()
            : FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        if (surface == null) { interactionStatus = "Navigationsflaeche fuer offene Tuer fehlt"; yield break; }
        surface.BuildNavMesh();
        yield return null;
        yield return WalkToKitchenStation(panWater.transform, "Korb ueber die Treppe in die Kueche bringen");
        if (!kitchenRouteReached) yield break;
        // Put the basket beside the kitchen workstation, rather than by the door.
        deliveryStep = DeliveryStep.CarryToTomatoes;
        if (!PutBasketDown()) { interactionStatus = "Kein sicherer Abstellplatz fuer den Korb"; yield break; }
        cookingPan = panWater.GetComponent<Rigidbody>();
        cookingPanPickup = panWater.GetComponent<PickupInteractable>();
        if (cookingPan == null || cookingPanPickup == null || cookingPanPickup.IsHeld)
        { interactionStatus = "Pan nicht verfuegbar"; yield break; }
        panPickupWasEnabled = cookingPanPickup.enabled;
        cookingPanPickup.enabled = false;
        cookingPan.transform.SetParent(null, true);
        cookingPan.isKinematic = true;
        cookingPan.useGravity = false;
        panRotation = Quaternion.Inverse(transform.rotation) * cookingPan.rotation;
        panCenterOffset = cookingPan.transform.InverseTransformPoint(BasketBounds(cookingPan.transform).center);
        panCarried = true;
        cookingPan.GetComponent<BucketContents>()?.BeginCarry(null);

        Transform shore = pond.PondRenderer != null ? pond.PondRenderer.transform : pond.transform;
        yield return WalkToKitchenStation(shore, "Mit der Pan die Treppe hinunter zum Teich");
        if (!kitchenRouteReached) yield break;
        BoxCollider waterZone = pond.GetComponent<BoxCollider>();
        if (waterZone == null) { interactionStatus = "Wasserbereich des Teichs fehlt"; yield break; }
        Vector3 waterPoint = waterZone.ClosestPoint(transform.position);
        Vector3 inward = Vector3.ProjectOnPlane(waterZone.bounds.center - waterPoint, Vector3.up).normalized;
        waterPoint += inward * 0.35f;
        waterPoint.y = pond.GetSurfaceY(waterPoint.x, waterPoint.z, Time.time) - 0.08f;
        dipPosition = cookingPan.position + waterPoint - panWater.RimPosition;
        dippingPan = true;
        interactionStatus = "Pan im Teich mit Wasser fuellen";
        float fillWait = 0f;
        while (panWater.FillLevel < 0.95f && fillWait < 20f)
        { fillWait += Time.deltaTime; yield return null; }
        dippingPan = false;
        if (panWater.FillLevel < 0.95f) { interactionStatus = "Pan erreicht das Teichwasser nicht"; yield break; }

        yield return WalkToKitchenStation(taskBasket, "Volle Pan wieder die Treppe hinauf zum Korb bringen");
        if (!kitchenRouteReached) yield break;
        Bounds panBounds = BasketBounds(cookingPan.transform);
        Vector3 ground = transform.position + transform.right * 0.7f;
        if (!NavMesh.SamplePosition(ground, out NavMeshHit groundHit, 1f, agent.areaMask))
        { interactionStatus = "Kein Abstellplatz fuer die Pan"; yield break; }
        Vector3 kitchenPanPosition = cookingPan.position + new Vector3(groundHit.position.x - panBounds.center.x,
            groundHit.position.y - panBounds.min.y + 0.03f, groundHit.position.z - panBounds.center.z);
        dippingPan = true;
        dipPosition = kitchenPanPosition;
        yield return new WaitForFixedUpdate();
        panCarried = false;
        dippingPan = false;
        cookingPan.GetComponent<BucketContents>()?.ReleaseContents();
        cookingPan.isKinematic = false;
        cookingPan.useGravity = true;
        yield return new WaitForSeconds(1f);
        BucketContents basketContents = taskBasket.GetComponent<BucketContents>();
        BucketContents panContents = cookingPan.GetComponent<BucketContents>();
        int transferred = 0;
        foreach (PickupInteractable fruit in FindObjectsByType<PickupInteractable>(FindObjectsSortMode.None))
        {
            if (transferred >= 4) break;
            Rigidbody fruitBody = fruit.GetComponent<Rigidbody>();
            if (!(fruit.CompareTag("Tomato") || fruit.name.StartsWith("Ripe_Tomato")) ||
                !basketContents.ContainsBody(fruitBody)) continue;
            interactionStatus = "Tomate aus dem Korb in die Pan legen";
            yield return ApproachInteraction(fruitBody.worldCenterOfMass);
            if (!interactionReady) yield break;
            reaching = true;
            harvestHandTarget = fruitBody.worldCenterOfMass;
            yield return new WaitForSeconds(0.5f);
            if (!CanGripTomato(fruit))
            { reaching = false; interactionStatus = "Tomate im Korb nicht erreichbar"; yield break; }
            fruit.enabled = false;
            fruitBody.isKinematic = true;
            fruitBody.useGravity = false;
            heldTomato = fruit;
            reaching = false;
            yield return ApproachInteraction(BasketBounds(cookingPan.transform).center);
            if (!interactionReady) yield break;
            reaching = true;
            Vector3 start = FingerGripPosition;
            Bounds opening = BasketBounds(cookingPan.transform);
            Vector3 release = new Vector3(opening.center.x, opening.max.y + 0.2f, opening.center.z);
            for (float time = 0f; time < 1f; time += Time.deltaTime)
            {
                harvestHandTarget = Vector3.Lerp(start, release, Mathf.SmoothStep(0f, 1f, time));
                yield return null;
            }
            harvestHandTarget = release;
            yield return null;
            if (Vector3.Distance(FingerGripPosition, release) > 0.12f)
            { interactionStatus = "Hand erreicht die Pan nicht"; yield break; }
            heldTomato = null;
            reaching = false;
            fruit.enabled = true;
            fruit.ReleaseNpcHarvest();
            float settle = 0f;
            while (settle < 5f && !panContents.ContainsBody(fruitBody))
            { settle += Time.deltaTime; yield return null; }
            if (!panContents.ContainsBody(fruitBody))
            { interactionStatus = "Tomate verfehlt Pan, bitte Lage pruefen"; yield break; }
            transferred++;
        }
        if (transferred != 4) { interactionStatus = "Es liegen nicht vier Tomaten in der Pan"; yield break; }
        cookingPan.isKinematic = true;
        cookingPan.useGravity = false;
        panCarried = true;
        panContents.BeginCarry(null);
        yield return WalkToKitchenStation(stove.transform, "Pan mit Wasser und Tomaten zum Ofen tragen");
        if (!kitchenRouteReached) yield break;
        Vector3 grate = stove.CookingPosition;
        bool foundGrate = false;
        float highest = float.NegativeInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(grate + Vector3.up * 0.4f, Vector3.down, 1.5f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.rigidbody == cookingPan || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.point.y <= highest) continue;
            highest = hit.point.y; grate = hit.point; foundGrate = true;
        }
        if (!foundGrate) { interactionStatus = "Ofengitter hat keine tragende Kollisionsflaeche"; yield break; }
        panBounds = BasketBounds(cookingPan.transform);
        dipPosition = cookingPan.position + new Vector3(grate.x - panBounds.center.x,
            grate.y - panBounds.min.y + 0.02f, grate.z - panBounds.center.z);
        dippingPan = true;
        yield return new WaitForFixedUpdate();
        ReleaseCookingPan();
        yield return new WaitForSeconds(1f);
        fire.Interact();
        interactionStatus = fire.IsBurning ? "Pan steht auf dem Gitter, Feuer brennt" : "Ofenfeuer konnte nicht gestartet werden";
        harvesting = false;
        deliveryStep = DeliveryStep.Finished;
    }

    private void MoveCookingPan()
    {
        if (!panCarried || cookingPan == null) return;
        Quaternion rotation = transform.rotation * panRotation;
        Vector3 position = dippingPan ? dipPosition :
            transform.position + transform.forward * 0.5f + Vector3.up * 0.85f
            - rotation * Vector3.Scale(panCenterOffset, cookingPan.transform.lossyScale);
        BucketContents contents = cookingPan.GetComponent<BucketContents>();
        if (contents != null && contents.TryMoveDynamicContents(cookingPan, position, rotation)) return;
        cookingPan.MovePosition(position);
        cookingPan.MoveRotation(rotation);
    }

    private void PoseCookingPan()
    {
        if (!panCarried || cookingPan == null || rightHand == null || leftHand == null) return;
        Bounds bounds = BasketBounds(cookingPan.transform);
        Vector3 across = (leftUpperArm.position - rightUpperArm.position).normalized;
        float halfWidth = Mathf.Min(bounds.extents.x, bounds.extents.z);
        Vector3 center = bounds.center + Vector3.up * bounds.extents.y * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(-transform.up, transform.forward);
        Vector3 offset = rotation * Vector3.up * FingerGripDistance;
        PoseCarryingArm(leftUpperArm, leftForearm, leftHand, center + across * halfWidth - offset);
        PoseCarryingArm(rightUpperArm, rightForearm, rightHand, center - across * halfWidth - offset);
        leftHand.rotation = rotation;
        rightHand.rotation = rotation;
        cookingPan.GetComponent<BucketContents>()?.RenderCarry(cookingPan.position, cookingPan.rotation);
    }

    private void ReleaseCookingPan()
    {
        panCarried = false;
        dippingPan = false;
        if (cookingPan == null) return;
        cookingPan.GetComponent<BucketContents>()?.ReleaseContents();
        cookingPan.isKinematic = false;
        cookingPan.useGravity = true;
        if (cookingPanPickup != null) cookingPanPickup.enabled = panPickupWasEnabled;
    }
}
