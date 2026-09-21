using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 3f;
    [Min(0f)] [SerializeField] private float pickupAimRadius = 0.15f;

    private PickupInteractable heldObject;

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;
        if (heldObject != null)
        {
            heldObject.Interact();
            heldObject = null;
            return;
        }
        TryInteract();
    }

    private void TryInteract()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Interactable interactable = null;
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            interactable = hit.collider.GetComponentInParent<Interactable>();

        // Prefer nearby visible fruit over watering the soil behind it.
        // Other interactions, such as doors, keep their direct targeting.
        if (interactable == null || interactable is GardenBed)
            interactable = FindNearbyPickup(ray) ?? interactable;

        if (interactable == null || !interactable.isActiveAndEnabled) return;
        interactable.Interact();
        PickupInteractable pickup = interactable as PickupInteractable;
        if (pickup != null && pickup.IsHeld) heldObject = pickup;
    }

    private PickupInteractable FindNearbyPickup(Ray ray)
    {
        if (pickupAimRadius <= 0f) return null;
        PickupInteractable closest = null;
        float closestDistance = float.PositiveInfinity;

        // Sphere casts omit shapes overlapping their starting sphere, and rays
        // omit shapes containing the camera. Include these close-range targets.
        foreach (Collider candidate in Physics.OverlapSphere(ray.origin, pickupAimRadius,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            Consider(candidate);

        foreach (RaycastHit candidate in Physics.SphereCastAll(ray, pickupAimRadius,
                     interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            Consider(candidate.collider);
        return closest;

        void Consider(Collider candidate)
        {
            PickupInteractable pickup = candidate.GetComponentInParent<PickupInteractable>();
            if (pickup == null || !pickup.isActiveAndEnabled || pickup.IsHeld) return;

            Vector3 nearestPoint = candidate.ClosestPoint(ray.origin);
            if ((nearestPoint - ray.origin).sqrMagnitude < 0.000001f)
            {
                closest = pickup;
                closestDistance = 0f;
                return;
            }

            Vector3 direction = candidate.bounds.center - ray.origin;
            float distance = direction.magnitude;
            if (distance >= closestDistance
                || Vector3.Distance(ray.origin, nearestPoint) > interactionDistance
                || Vector3.Dot(direction, ray.direction) < 0f)
                return;

            // Wider targeting must not reach through walls or soil.
            if (!Physics.Raycast(ray.origin, direction.normalized, out RaycastHit visible,
                    distance + 0.01f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || visible.collider.GetComponentInParent<PickupInteractable>() != pickup)
                return;
            closest = pickup;
            closestDistance = distance;
        }
    }
}
