using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SoupCooking : MonoBehaviour
{
    [SerializeField] private WaterHeating heating;
    [SerializeField] private RainCollector water;
    [SerializeField, Min(0.1f)] private float cookingSeconds = 15f;
    [SerializeField]
    private Color soupColor =
        new Color(0.65f, 0.08f, 0.025f, 1f);

    private readonly HashSet<Collider> tomatoes = new();
    private const int RequiredTomatoes = 4;
    private readonly List<GameObject> availableTomatoes = new();
    private readonly HashSet<GameObject> uniqueTomatoes = new();
    private bool cooked;
    private MaterialPropertyBlock properties;
    private Renderer waterRenderer;
    private Color originalColor;
    private float progress;

    private static readonly int ColorId =
        Shader.PropertyToID("_SoupColor");

    private void Start()
    {
        if (heating == null)
            heating = GetComponentInParent<WaterHeating>();

        if (water == null)
            water = GetComponentInParent<RainCollector>();

        GetComponent<BoxCollider>().isTrigger = true;

        waterRenderer = water != null
            ? water.WaterSurfaceRenderer
            : null;

        if (heating == null || waterRenderer == null ||
            waterRenderer.sharedMaterial == null ||
            !waterRenderer.sharedMaterial.HasProperty(ColorId))
        {
            Debug.LogError(
                "SoupCooking: Wasser, WaterHeating oder _SoupColor fehlt.",
                this);
            enabled = false;
            return;
        }

        properties = new MaterialPropertyBlock();
        originalColor = waterRenderer.sharedMaterial.GetColor(ColorId);
    }

    private bool IsTomato(Collider other)
    {
        return other.CompareTag("Tomato") ||
            (other.attachedRigidbody != null &&
             other.attachedRigidbody.CompareTag("Tomato"));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsTomato(other))
            tomatoes.Add(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsTomato(other))
            tomatoes.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        tomatoes.Remove(other);
    }

    private void LateUpdate()
    {
        tomatoes.RemoveWhere(item =>
            item == null || !item.enabled ||
            !item.gameObject.activeInHierarchy);

        availableTomatoes.Clear();
        uniqueTomatoes.Clear();

        foreach (Collider tomato in tomatoes)
        {
            PickupInteractable pickup =
                tomato.GetComponentInParent<PickupInteractable>();

            if (pickup != null && pickup.IsHeld)
                continue;

            // Count a tomato once even when it has multiple colliders.
            GameObject ingredient = tomato.attachedRigidbody != null
                ? tomato.attachedRigidbody.gameObject
                : pickup != null ? pickup.gameObject : tomato.gameObject;
            if (uniqueTomatoes.Add(ingredient))
                availableTomatoes.Add(ingredient);
        }

        if (!water.HasWater)
        {
            progress = 0f;
            cooked = false;
        }
        else if (!cooked && heating.IsBoiling && availableTomatoes.Count >= RequiredTomatoes)
        {
            progress = Mathf.MoveTowards(
                progress, 1f,
                Time.deltaTime / Mathf.Max(0.1f, cookingSeconds));
            if (progress >= 1f)
            {
                // Consume one batch; extra tomatoes remain in the pot.
                cooked = true;
                for (int i = 0; i < RequiredTomatoes; i++)
                {
                    GameObject ingredient = availableTomatoes[i];
                    ingredient.SetActive(false);
                    Destroy(ingredient);
                }
            }
        }

        // Vorhandene Einstellungen f�r die Kochbewegung erhalten.
        waterRenderer.GetPropertyBlock(properties);
        properties.SetColor(
            ColorId,
            Color.Lerp(originalColor, soupColor, progress));
        waterRenderer.SetPropertyBlock(properties);
    }

    private void OnDisable()
    {
        tomatoes.Clear();
    }
}
