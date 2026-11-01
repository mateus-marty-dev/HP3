using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PickupInteractable))]
public class ShovelTool : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PickupInteractable pickup;

    [Header("Movement (Hold Point Local Axes)")]
    [SerializeField] private Vector3 forwardOffset = new Vector3(0f, 0f, 0.3f);
    [SerializeField, Min(0.01f)] private float forwardTime = 0.18f;
    [SerializeField, Min(0.01f)] private float returnTime = 0.25f;

    private Coroutine movement;

    private void Awake()
    {
        if (pickup == null)
            pickup = GetComponent<PickupInteractable>();
    }

    private void Update()
    {
        if (pickup == null || !pickup.IsHeld)
        {
            CancelMovement();
            return;
        }

        if (Input.GetMouseButtonDown(0) && movement == null)
            movement = StartCoroutine(MoveShovel());
    }

    private IEnumerator MoveShovel()
    {
        yield return MoveOffset(Vector3.zero, forwardOffset, forwardTime);
        yield return MoveOffset(forwardOffset, Vector3.zero, returnTime);

        pickup.SetActionPosition(Vector3.zero);
        movement = null;
    }

    private IEnumerator MoveOffset(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            if (!pickup.IsHeld)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / safeDuration));
            pickup.SetActionPosition(Vector3.Lerp(from, to, t));
            yield return null;
        }
    }

    private void CancelMovement()
    {
        if (movement == null)
            return;

        StopCoroutine(movement);
        movement = null;

        if (pickup != null)
            pickup.SetActionPosition(Vector3.zero);
    }

    private void OnDisable()
    {
        CancelMovement();
    }
}
