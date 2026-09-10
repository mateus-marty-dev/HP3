using UnityEngine;

public class PickupInteractable : Interactable
{
    [SerializeField] private Transform holdPoint;

    [Header("Hold Offset")]
    [SerializeField] private Vector3 holdPositionOffset;
    [SerializeField] private Vector3 holdRotationOffset;

    [Header("Player")]
    [SerializeField] private Collider playerCollider;

    private Rigidbody rb;
    private Collider[] objectColliders;

    private bool isHeld = false;

    public bool IsHeld => isHeld;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        objectColliders = GetComponentsInChildren<Collider>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void FixedUpdate()
    {
        if (!isHeld)
            return;

        Vector3 targetPosition =
            holdPoint.TransformPoint(holdPositionOffset);

        Quaternion targetRotation =
            holdPoint.rotation *
            Quaternion.Euler(holdRotationOffset);

        rb.MovePosition(targetPosition);
        rb.MoveRotation(targetRotation);
    }

    public override void Interact()
    {
        if (!isHeld)
            PickUp();
        else
            Drop();
    }

    private void PickUp()
    {
        isHeld = true;

        rb.useGravity = false;
        rb.isKinematic = true;

        IgnorePlayerCollision(true);
    }

    private void Drop()
    {
        isHeld = false;

        rb.isKinematic = false;
        rb.useGravity = true;

        IgnorePlayerCollision(false);
    }

    private void IgnorePlayerCollision(bool ignore)
    {
        if (playerCollider == null)
            return;

        foreach (Collider col in objectColliders)
        {
            if (col != null)
            {
                Physics.IgnoreCollision(
                    col,
                    playerCollider,
                    ignore
                );
            }
        }
    }
}