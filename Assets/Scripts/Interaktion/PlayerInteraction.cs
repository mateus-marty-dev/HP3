using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 3f;

    private PickupInteractable heldObject;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Wenn wir bereits etwas tragen:
            // direkt ablegen, kein Raycast nötig.
            if (heldObject != null)
            {
                heldObject.Interact();
                heldObject = null;
                return;
            }

            TryInteract();
        }
    }

    private void TryInteract()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            Interactable interactable =
                hit.collider.GetComponentInParent<Interactable>();

            if (interactable == null)
                return;

            interactable.Interact();

            // War das ein Pickup-Objekt?
            PickupInteractable pickup =
                interactable as PickupInteractable;

            if (pickup != null && pickup.IsHeld)
            {
                heldObject = pickup;
            }
        }
    }
}