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

    private Quaternion actionRotation = Quaternion.identity;

    public void SetActionRotation(Quaternion rotation)
    {
        actionRotation = rotation;
    }


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Alle Collider dieses Objekts und seiner Children.
        // Beim Hammer z.B. Kopf + Griff.
        objectColliders = GetComponentsInChildren<Collider>();

        if (rb != null)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
    }


    public void ConfigureHarvest(
        Transform targetHoldPoint,
        Collider targetPlayerCollider,
        Vector3 positionOffset,
        Vector3 visualCenter)
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

        if (holdPoint == null || rb == null)
            return;


        Vector3 targetPosition =
            holdPoint.TransformPoint(holdPositionOffset);


        Quaternion targetRotation =
     holdPoint.rotation *
     Quaternion.Euler(holdRotationOffset) *
     actionRotation;


        // Wichtig für importierte Früchte:
        // Der sichtbare Mittelpunkt wird am HoldPoint gehalten,
        // auch wenn der Pivot ungünstig liegt.
        targetPosition -=
            targetRotation *
            Vector3.Scale(
                heldVisualCenter,
                transform.lossyScale
            );


        rb.MovePosition(targetPosition);
        rb.MoveRotation(targetRotation);


        // Falls dieses Objekt ein Kübel ist.
        GetComponent<BucketContents>()?
            .MoveContents(targetPosition, targetRotation);
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


        // Wird für geerntetes Gemüse verwendet.
        if (detachOnPickup)
        {
            transform.SetParent(null, true);

            holdRotationOffset =
                (
                    Quaternion.Inverse(holdPoint.rotation) *
                    transform.rotation
                ).eulerAngles;

            detachOnPickup = false;

            interactionText = "E - Aufheben";
        }


        isHeld = true;


        // Falls es ein Kübel ist.
        GetComponent<BucketContents>()?
            .BeginCarry(playerCollider);


        // Physik während des Tragens deaktivieren.
        rb.useGravity = false;
        rb.isKinematic = true;


        // Kollision zwischen getragenem Objekt
        // und Player deaktivieren.
        IgnorePlayerCollision(true);
    }


    private void Drop()
    {
        isHeld = false;


        GetComponent<BucketContents>()?
            .ReleaseContents();


        rb.isKinematic = false;
        rb.useGravity = true;


        // Nach dem Ablegen wieder mit Player kollidieren.
        IgnorePlayerCollision(false);
    }


    private void IgnorePlayerCollision(bool ignore)
    {
        if (playerCollider == null)
            return;


        foreach (Collider col in objectColliders)
        {
            if (col == null)
                continue;


            Physics.IgnoreCollision(
                col,
                playerCollider,
                ignore
            );
        }
    }
}