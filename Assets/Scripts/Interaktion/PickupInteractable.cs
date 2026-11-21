
using System;
using System.Collections;
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

    private Quaternion actionRotation = Quaternion.identity;
    private Vector3 actionPosition = Vector3.zero;
    private Coroutine restorePlayerCollision;

    public bool IsHeld => isHeld;

    // NEW: True when picked up, false when dropped.
    public event Action<bool> HeldStateChanged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        objectColliders = GetComponentsInChildren<Collider>();

        if (rb != null)
        {
            rb.interpolation =
                RigidbodyInterpolation.Interpolate;
        }
    }

    public void SetActionRotation(Quaternion rotation)
    {
        actionRotation = rotation;
    }

    public void SetActionPosition(Vector3 position)
    {
        actionPosition = position;
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
        if (!isHeld || rb == null || holdPoint == null)
            return;

        Vector3 targetPosition =
            holdPoint.TransformPoint(
                holdPositionOffset + actionPosition
            );

        Quaternion targetRotation =
            holdPoint.rotation *
            Quaternion.Euler(holdRotationOffset) *
            actionRotation;

        targetPosition -=
            targetRotation *
            Vector3.Scale(
                heldVisualCenter,
                transform.lossyScale
            );

        BucketContents contents = GetComponent<BucketContents>();

        if (contents != null &&
            contents.TryMoveDynamicContents(
                rb, targetPosition, targetRotation))
        {
            return;
        }

        rb.MovePosition(targetPosition);
        rb.MoveRotation(targetRotation);

        GetComponent<BucketContents>()
            ?.MoveContents(targetPosition, targetRotation);
    }

    private void LateUpdate()
    {
        if (!isHeld || rb == null || holdPoint == null)
            return;

        Quaternion rotation =
            holdPoint.rotation *
            Quaternion.Euler(holdRotationOffset) *
            actionRotation;

        Vector3 position =
            holdPoint.TransformPoint(
                holdPositionOffset + actionPosition
            ) -
            rotation *
            Vector3.Scale(
                heldVisualCenter,
                transform.lossyScale
            );

        GetComponent<BucketContents>()
            ?.RenderCarry(position, rotation);
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

        if (restorePlayerCollision != null)
        {
            StopCoroutine(restorePlayerCollision);
            restorePlayerCollision = null;
        }

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

        GetComponent<BucketContents>()
            ?.BeginCarry(playerCollider);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.useGravity = false;

        if (GetComponent<GentleItemPhysics>() != null)
        {
            rb.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
        }

        rb.isKinematic = true;

        IgnorePlayerCollision(true);

        // Notify listeners, including the readable book.
        HeldStateChanged?.Invoke(true);
    }

    public void ReleaseNpcHarvest()
    {
        if (!isHeld)
            Drop();
    }

    private void Drop()
    {
        if (rb == null)
            return;

        isHeld = false;

        actionPosition = Vector3.zero;
        actionRotation = Quaternion.identity;

        GetComponent<BucketContents>()
            ?.ReleaseContents();

        rb.isKinematic = false;
        rb.useGravity = true;

        GentleItemPhysics tuning =
            GetComponent<GentleItemPhysics>();

        if (tuning != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            tuning.UpdateCollisionMode();

            restorePlayerCollision =
                StartCoroutine(RestoreCollisionWhenClear());
        }
        else
        {
            IgnorePlayerCollision(false);
        }

        HeldStateChanged?.Invoke(false);
    }

    private IEnumerator RestoreCollisionWhenClear()
    {
        yield return new WaitForFixedUpdate();

        while (playerCollider != null && !isHeld)
        {
            bool overlaps = false;

            foreach (Collider col in objectColliders)
            {
                if (col == null ||
                    !col.enabled ||
                    col.isTrigger)
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                    col,
                    col.transform.position,
                    col.transform.rotation,
                    playerCollider,
                    playerCollider.transform.position,
                    playerCollider.transform.rotation,
                    out _,
                    out _))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
                break;

            yield return new WaitForFixedUpdate();
        }

        if (!isHeld)
            IgnorePlayerCollision(false);

        restorePlayerCollision = null;
    }

    private void OnDisable()
    {
        GetComponent<BucketContents>()?.ReleaseContents();

        if (restorePlayerCollision == null)
            return;

        StopCoroutine(restorePlayerCollision);
        restorePlayerCollision = null;

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
