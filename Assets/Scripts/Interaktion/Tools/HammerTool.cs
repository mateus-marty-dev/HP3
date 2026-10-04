using System.Collections;
using UnityEngine;

public class HammerTool : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PickupInteractable pickup;
    [SerializeField] private Transform impactPoint;
    [SerializeField] private Collider headCollider;

    [Header("Swing")]
    [SerializeField] private float swingAngle = 70f;
    [SerializeField] private float swingForwardTime = 0.12f;
    [SerializeField] private float swingReturnTime = 0.18f;

    [Header("Hit Detection")]
    [SerializeField] private float impactRadius = 0.12f;

    [Header("Impact")]
    [SerializeField] private float impactForce = 1.5f;

    private bool isSwinging = false;
    private bool hasHit = false;
    private bool wasHeld = false;

    private Vector3 previousImpactPosition;
    private Quaternion currentActionRotation = Quaternion.identity;

    private void Awake()
    {
        if (pickup == null)
            pickup = GetComponent<PickupInteractable>();
    }

    private void Start()
    {
        if (headCollider != null)
        {
            headCollider.enabled = true;
        }
    }

    private void Update()
    {
        if (pickup == null)
            return;

        // -----------------------------------------
        // Collider je nach Pickup-Zustand
        // -----------------------------------------

        if (pickup.IsHeld != wasHeld)
        {
            wasHeld = pickup.IsHeld;

            if (headCollider != null)
            {
                // Getragen = Collider AUS
                // Am Boden = Collider AN
                headCollider.enabled = !pickup.IsHeld;
            }
        }

        if (!pickup.IsHeld)
            return;

        if (impactPoint == null)
            return;

        if (Input.GetMouseButtonDown(0) && !isSwinging)
        {
            StartCoroutine(Swing());
        }
    }

    private IEnumerator Swing()
    {
        isSwinging = true;
        hasHit = false;

        Quaternion startRotation =
            Quaternion.identity;

        Quaternion targetRotation =
            Quaternion.Euler(
                swingAngle,
                0f,
                0f
            );

        currentActionRotation =
            startRotation;

        previousImpactPosition =
            impactPoint.position;

        float time = 0f;

        // =========================================
        // VORWÄRTSSCHLAG
        // =========================================

        while (time < swingForwardTime)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / swingForwardTime
                );

            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            currentActionRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t
                );

            pickup.SetActionRotation(
                currentActionRotation
            );

            // Warten, bis die neue Hammerposition
            // tatsächlich angewendet wurde.
            yield return new WaitForFixedUpdate();

            CheckImpact();

            // Beim Kontakt endet die Vorwärtsbewegung.
            if (hasHit)
                break;
        }

        // =========================================
        // KURZE PAUSE BEIM TREFFER
        // =========================================

        if (hasHit)
        {
            yield return new WaitForSeconds(
                0.04f
            );
        }

        // =========================================
        // RÜCKBEWEGUNG
        // =========================================

        Quaternion returnStart =
            currentActionRotation;

        time = 0f;

        while (time < swingReturnTime)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / swingReturnTime
                );

            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            currentActionRotation =
                Quaternion.Slerp(
                    returnStart,
                    Quaternion.identity,
                    t
                );

            pickup.SetActionRotation(
                currentActionRotation
            );

            yield return new WaitForFixedUpdate();
        }

        currentActionRotation =
            Quaternion.identity;

        pickup.SetActionRotation(
            Quaternion.identity
        );

        isSwinging = false;
    }

    private void CheckImpact()
    {
        if (hasHit || impactPoint == null)
            return;

        Vector3 currentPosition =
            impactPoint.position;

        Vector3 movement =
            currentPosition -
            previousImpactPosition;

        float distance =
            movement.magnitude;

        if (distance <= 0.001f)
        {
            previousImpactPosition =
                currentPosition;

            return;
        }

        RaycastHit hit;

        bool didHit =
            Physics.SphereCast(
                previousImpactPosition,
                impactRadius,
                movement.normalized,
                out hit,
                distance
            );

        if (didHit)
        {
            // Eigenen Hammer ignorieren.
            if (!hit.collider.transform.IsChildOf(transform))
            {
                HandleHit(
                    hit,
                    movement
                );

                hasHit = true;
            }
        }

        previousImpactPosition =
            currentPosition;
    }

    private void HandleHit(
        RaycastHit hit,
        Vector3 hammerMovement)
    {
        Debug.Log(
            "Hammer trifft: " +
            hit.collider.gameObject.name
        );

        // =========================================
        // PHYSIKREAKTION DES GETROFFENEN OBJEKTS
        // =========================================

        Rigidbody targetRb =
            hit.collider.GetComponentInParent<Rigidbody>();

        if (targetRb != null &&
            !targetRb.isKinematic)
        {
            Vector3 impactDirection =
                hammerMovement.normalized;

            targetRb.AddForceAtPosition(
                impactDirection * impactForce,
                hit.point,
                ForceMode.Impulse
            );
        }

        // =========================================
        // ZERSTÖRBARES OBJEKT
        // =========================================

        BreakableObject breakable =
            hit.collider
                .GetComponentInParent<BreakableObject>();

        if (breakable != null)
        {
            breakable.Hit();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (impactPoint == null)
            return;

        Gizmos.DrawWireSphere(
            impactPoint.position,
            impactRadius
        );
    }
}