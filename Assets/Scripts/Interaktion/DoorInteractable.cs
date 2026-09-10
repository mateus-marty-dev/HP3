using UnityEngine;

public class DoorInteractable : Interactable
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;

    private bool isOpen = false;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;

    private void Start()
    {
        closedRotation = transform.localRotation;

        // Falls deine Tür um Z dreht:
        openRotation = closedRotation * Quaternion.Euler(0f, 0f, openAngle);

        targetRotation = closedRotation;
    }

    private void Update()
    {
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRotation,
            openSpeed * Time.deltaTime
        );
    }

    public override void Interact()
    {
        isOpen = !isOpen;

        targetRotation = isOpen
            ? openRotation
            : closedRotation;
    }
}