using UnityEngine;

public class BookInteractable : Interactable
{
    [SerializeField] private float pullDistance = 0.12f;
    [SerializeField] private Vector3 pullDirection = Vector3.forward;

    private bool pulledOut = false;
    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    public override void Interact()
    {
        if (!pulledOut)
        {
            Vector3 worldDirection =
                transform.TransformDirection(pullDirection.normalized);

            transform.position =
                startPosition + worldDirection * pullDistance;

            pulledOut = true;
        }
        else
        {
            transform.position = startPosition;
            pulledOut = false;
        }
    }
}