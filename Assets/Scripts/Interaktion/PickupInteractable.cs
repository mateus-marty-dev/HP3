using UnityEngine;

public class PickupInteractable : Interactable
{
    [SerializeField] private Transform holdPoint;

    [Header("Collision Ignore While Held")]
    [SerializeField] private Collider bucketCollider;
    [SerializeField] private Collider playerCollider;

    private Rigidbody rb;
    private bool isHeld = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!isHeld)
            return;

        rb.MovePosition(holdPoint.position);
        rb.MoveRotation(holdPoint.rotation);
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

        Physics.IgnoreCollision(bucketCollider, playerCollider, true);
    }

    private void Drop()
    {
        isHeld = false;

        rb.isKinematic = false;
        rb.useGravity = true;

        Physics.IgnoreCollision(bucketCollider, playerCollider, false);
    }
}