using UnityEngine;

public class DrawerInteractable : Interactable
{
    [SerializeField] private float pullDistance = 0.35f;
    [SerializeField] private Vector3 pullDirection = Vector3.forward;

    private bool isOpen = false;
    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    public override void Interact()
    {
        if (!isOpen)
        {
            Vector3 worldDirection =
                transform.TransformDirection(pullDirection.normalized);

            transform.position =
                startPosition + worldDirection * pullDistance;

            isOpen = true;
        }
        else
        {
            transform.position = startPosition;
            isOpen = false;
        }
    }
}