
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 3f;

    [Min(0f)]
    [SerializeField] private float pickupAimRadius = 0.15f;

    [Header("Lighting the stove")]
    [Min(0.1f)]
    [SerializeField] private float stoveInteractionDistance = 5f;

    [Min(0f)]
    [SerializeField] private float stoveAimRadius = 0.5f;

    private PickupInteractable heldObject;
    private GUIStyle bookPromptStyle;

    public bool IsTargetingReadableBook =>
        FindTargetBook() != null;

    private ReadableBookInteractable FindTargetBook()
    {
        if (heldObject != null || playerCamera == null)
            return null;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        ReadableBookInteractable book =
            hit.collider.GetComponentInParent<ReadableBookInteractable>();

        return book != null && book.isActiveAndEnabled
            ? book
            : null;
    }

    private void OnGUI()
    {
        ReadableBookInteractable book = FindTargetBook();

        if (book == null)
            return;

        if (bookPromptStyle == null)
        {
            bookPromptStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
        }

        string prompt = book.CanRead
            ? book.interactionText
            : "E - Buch aufheben";

        float width =
            Mathf.Min(560f, Screen.width - 24f);

        GUI.Box(
            new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height - 86f,
                width,
                38f
            ),
            prompt,
            bookPromptStyle
        );
    }

    private void Update()
    {
        // E: Pick up / drop / general interaction.
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObject != null)
            {
                PickupInteractable previous = heldObject;

                previous.Interact();

                if (!previous.IsHeld)
                    heldObject = null;

                return;
            }

            TryInteract();
            return;
        }

        // Book controls are unavailable while holding an item.
        if (heldObject != null)
            return;

        bool pageForward =
            Input.GetMouseButtonDown(0) ||
            Input.GetKeyDown(KeyCode.F);

        bool pageBackward =
            Input.GetMouseButtonDown(1) ||
            Input.GetKeyDown(KeyCode.Q);

        if (!pageForward && !pageBackward)
            return;

        ReadableBookInteractable book = FindTargetBook();

        if (book == null ||
            !book.CanRead ||
            book.IsAnimating)
        {
            return;
        }

        if (pageForward)
            book.Interact();
        else
            book.InteractReverse();
    }

    private void TryInteract()
    {
        if (playerCamera == null)
            return;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        Interactable interactable = null;
        float visibleDistance = interactionDistance;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            visibleDistance = hit.distance;

            // The book always uses pickup when E is pressed.
            ReadableBookInteractable book =
                hit.collider.GetComponentInParent<ReadableBookInteractable>();

            if (book != null)
            {
                if (book.IsAnimating)
                    return;

                PickupInteractable bookPickup =
                    book.GetComponent<PickupInteractable>();

                if (bookPickup == null)
                {
                    bookPickup =
                        book.GetComponentInParent<PickupInteractable>();
                }

                if (bookPickup == null ||
                    !bookPickup.isActiveAndEnabled)
                {
                    return;
                }

                bookPickup.Interact();

                if (bookPickup.IsHeld)
                    heldObject = bookPickup;

                return;
            }

            interactable =
                hit.collider.GetComponentInParent<Interactable>();
        }

        // Existing zombie interaction.
        // Walls still block the request.
        foreach (RaycastHit candidate in Physics.RaycastAll(
                     ray,
                     interactionDistance,
                     Physics.DefaultRaycastLayers,
                     QueryTriggerInteraction.Collide))
        {
            if (!candidate.collider.isTrigger ||
                candidate.distance > visibleDistance)
            {
                continue;
            }

            ZombieBehaviour zombie =
                candidate.collider.GetComponentInParent<ZombieBehaviour>();

            if (zombie == null ||
                !zombie.isActiveAndEnabled)
            {
                continue;
            }

            interactable = zombie;
            visibleDistance = candidate.distance;
        }

        if (interactable == null)
            interactable = FindNearbyStove(ray);

        // Preserve existing garden / pickup priority.
        if (interactable == null || interactable is GardenBed)
        {
            interactable =
                FindNearbyPickup(ray) ?? interactable;
        }

        if (interactable == null ||
            !interactable.isActiveAndEnabled)
        {
            return;
        }

        interactable.Interact();

        PickupInteractable pickup =
            interactable as PickupInteractable;

        if (pickup != null && pickup.IsHeld)
            heldObject = pickup;
    }

    private StoveFireInteractable FindNearbyStove(Ray ray)
    {
        if (stoveAimRadius <= 0f)
            return null;

        StoveFireInteractable closest = null;
        float closestDistance = float.PositiveInfinity;

        foreach (Collider candidate in Physics.OverlapSphere(
                     ray.origin,
                     stoveAimRadius,
                     Physics.DefaultRaycastLayers,
                     QueryTriggerInteraction.Ignore))
        {
            Consider(candidate);
        }

        foreach (RaycastHit candidate in Physics.SphereCastAll(
                     ray,
                     stoveAimRadius,
                     stoveInteractionDistance,
                     Physics.DefaultRaycastLayers,
                     QueryTriggerInteraction.Ignore))
        {
            Consider(candidate.collider);
        }

        return closest;

        void Consider(Collider candidate)
        {
            StoveFireInteractable stove =
                candidate.GetComponentInParent<StoveFireInteractable>();

            if (stove == null ||
                !stove.isActiveAndEnabled ||
                stove.IsBurning)
            {
                return;
            }

            if (Vector3.Distance(
                ray.origin,
                candidate.ClosestPoint(ray.origin)
            ) > stoveInteractionDistance)
            {
                return;
            }

            Vector3 direction =
                candidate.bounds.center - ray.origin;

            float distance = direction.magnitude;

            if (distance >= closestDistance ||
                Vector3.Dot(direction, ray.direction) < 0f)
            {
                return;
            }

            // Check the actual wood.
            // Do not ignite through walls.
            foreach (RaycastHit obstruction in Physics.RaycastAll(
                         ray.origin,
                         direction.normalized,
                         distance,
                         Physics.DefaultRaycastLayers,
                         QueryTriggerInteraction.Ignore))
            {
                if (obstruction.collider.transform.IsChildOf(transform))
                    continue;

                if (obstruction.collider
                    .GetComponentInParent<StoveFireInteractable>() == stove)
                {
                    continue;
                }

                return;
            }

            closest = stove;
            closestDistance = distance;
        }
    }

    private PickupInteractable FindNearbyPickup(Ray ray)
    {
        if (pickupAimRadius <= 0f)
            return null;

        PickupInteractable closest = null;
        float closestDistance = float.PositiveInfinity;

        foreach (Collider candidate in Physics.OverlapSphere(
                     ray.origin,
                     pickupAimRadius,
                     Physics.DefaultRaycastLayers,
                     QueryTriggerInteraction.Ignore))
        {
            Consider(candidate);
        }

        foreach (RaycastHit candidate in Physics.SphereCastAll(
                     ray,
                     pickupAimRadius,
                     interactionDistance,
                     Physics.DefaultRaycastLayers,
                     QueryTriggerInteraction.Ignore))
        {
            Consider(candidate.collider);
        }

        return closest;

        void Consider(Collider candidate)
        {
            PickupInteractable pickup =
                candidate.GetComponentInParent<PickupInteractable>();

            if (pickup == null ||
                !pickup.isActiveAndEnabled ||
                pickup.IsHeld)
            {
                return;
            }

            // Do not pick up a book during an animation.
            ReadableBookInteractable book =
                pickup.GetComponent<ReadableBookInteractable>();

            if (book != null && book.IsAnimating)
                return;

            Vector3 nearestPoint =
                candidate.ClosestPoint(ray.origin);

            if ((nearestPoint - ray.origin).sqrMagnitude < 0.000001f)
            {
                closest = pickup;
                closestDistance = 0f;
                return;
            }

            Vector3 direction =
                candidate.bounds.center - ray.origin;

            float distance = direction.magnitude;

            if (distance >= closestDistance ||
                Vector3.Distance(ray.origin, nearestPoint) >
                interactionDistance ||
                Vector3.Dot(direction, ray.direction) < 0f)
            {
                return;
            }

            if (!Physics.Raycast(
                ray.origin,
                direction.normalized,
                out RaycastHit visible,
                distance + 0.01f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) ||
                visible.collider.GetComponentInParent<PickupInteractable>()
                != pickup)
            {
                return;
            }

            closest = pickup;
            closestDistance = distance;
        }
    }
}
