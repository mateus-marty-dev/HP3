using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    public string interactionText = "E - Interact";

    public abstract void Interact();
}