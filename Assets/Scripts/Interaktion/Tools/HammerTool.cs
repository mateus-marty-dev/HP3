using System.Collections;
using UnityEngine;

public class HammerTool : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PickupInteractable pickup;

    [Header("Swing")]
    [SerializeField] private float swingAngle = 70f;
    [SerializeField] private float swingForwardTime = 0.12f;
    [SerializeField] private float swingReturnTime = 0.18f;

    private bool isSwinging = false;

    private void Awake()
    {
        if (pickup == null)
            pickup = GetComponent<PickupInteractable>();
    }

    private void Update()
    {
        // Hammer funktioniert nur, wenn er gehalten wird.
        if (pickup == null || !pickup.IsHeld)
            return;

        if (Input.GetMouseButtonDown(0) && !isSwinging)
        {
            StartCoroutine(Swing());
        }
    }

    private IEnumerator Swing()
    {
        isSwinging = true;

        Transform holdPoint = transform;

        Quaternion startRotation = holdPoint.localRotation;

        Quaternion hitRotation =
            startRotation * Quaternion.Euler(swingAngle, 0f, 0f);

        // Nach vorne schlagen
        float time = 0f;

        while (time < swingForwardTime)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / swingForwardTime);

            holdPoint.localRotation =
                Quaternion.Slerp(startRotation, hitRotation, t);

            yield return null;
        }

        // Zurück
        time = 0f;

        while (time < swingReturnTime)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / swingReturnTime);

            holdPoint.localRotation =
                Quaternion.Slerp(hitRotation, startRotation, t);

            yield return null;
        }

        holdPoint.localRotation = startRotation;

        isSwinging = false;
    }
}