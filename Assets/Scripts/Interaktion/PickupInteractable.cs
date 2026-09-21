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
    private bool detachOnPickup;
    private Vector3 heldVisualCenter;

    public bool IsHeld => isHeld;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        objectColliders = GetComponentsInChildren<Collider>();

        if (rb != null)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void ConfigureHarvest(Transform targetHoldPoint, Collider targetPlayerCollider,
        Vector3 positionOffset, Vector3 visualCenter)
    {
        holdPoint = targetHoldPoint;
        playerCollider = targetPlayerCollider;
        holdPositionOffset = positionOffset;
        heldVisualCenter = visualCenter;
        detachOnPickup = true;
        interactionText = "E - Pfluecken";
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

        // Imported fruit may have its pivot at the plant root. Hold the visible
        // fruit center at the target instead of moving that distant pivot there.
        targetPosition -= targetRotation * Vector3.Scale(heldVisualCenter, transform.lossyScale);

        rb.MovePosition(targetPosition);
        rb.MoveRotation(targetRotation);
        GetComponent<BucketContents>()?.MoveContents(targetPosition, targetRotation);
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
        if (rb == null || holdPoint == null)
            return;

        if (detachOnPickup)
        {
            transform.SetParent(null, true);
            holdRotationOffset = (Quaternion.Inverse(holdPoint.rotation) * transform.rotation).eulerAngles;
            detachOnPickup = false;
            interactionText = "E - Aufheben";
        }

        isHeld = true;
        GetComponent<BucketContents>()?.BeginCarry(playerCollider);

        rb.useGravity = false;
        rb.isKinematic = true;

        IgnorePlayerCollision(true);
    }

    private void Drop()
    {
        isHeld = false;
        GetComponent<BucketContents>()?.ReleaseContents();

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
