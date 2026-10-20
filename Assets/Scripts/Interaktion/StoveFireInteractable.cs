using UnityEngine;
using System.Collections.Generic;

public class StoveFireInteractable : Interactable
{
    [SerializeField] private ParticleSystem flame;
    [SerializeField] private Light fireLight;
    [Min(0.1f)] [SerializeField] private float growthDuration = 8f;
    [Min(1f)] [SerializeField] private float fullFlameMultiplier = 2.5f;
    [Min(0f)] [SerializeField] private float initialLightIntensity = 0.3f;
    [Min(0f)] [SerializeField] private float fullLightIntensity = 1.2f;
    [Header("Fire sound")]
    [SerializeField] private AudioSource fireAudio;
    [Range(0f, 1f)] [SerializeField] private float initialFireVolume = 0.15f;
    [Range(0f, 1f)] [SerializeField] private float fullFireVolume = 0.65f;
    [Header("Wood charring")]
    [SerializeField] private MeshRenderer[] woodRenderers;
    [Min(0f)] [SerializeField] private float charringDelay = 10f;
    [Min(0.1f)] [SerializeField] private float charringDuration = 30f;
    [SerializeField] private Color charredColor = new Color(0.188f, 0.173f, 0.161f, 1f);

    private readonly List<Material> woodMaterials = new List<Material>();
    private readonly List<Color> originalColors = new List<Color>();
    private readonly List<int> colorProperties = new List<int>();

    private Vector3 initialFlameScale;
    private bool burning;
    private float elapsed;
    public bool IsBurning => burning;

    private void Awake()
    {
        interactionText = "E - Ofen anzünden";
        if (flame != null)
        {
            initialFlameScale = flame.transform.localScale;
            flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (fireLight != null) fireLight.intensity = 0f;
        if (fireAudio == null) fireAudio = GetComponent<AudioSource>();
        if (fireAudio != null)
        {
            fireAudio.playOnAwake = false;
            fireAudio.loop = true;
            fireAudio.Stop();
            fireAudio.volume = 0f;
        }
        if (woodRenderers != null)
        {
            var seen = new HashSet<MeshRenderer>();
            foreach (MeshRenderer wood in woodRenderers)
            {
                if (wood == null || !seen.Add(wood)) continue;
                Material[] materials = wood.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    string property = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                    if (!source.HasProperty(property)) continue;
                    // Instance only the assigned wood slots; shared assets remain untouched.
                    Material copy = new Material(source);
                    materials[i] = copy;
                    woodMaterials.Add(copy);
                    originalColors.Add(copy.GetColor(property));
                    colorProperties.Add(Shader.PropertyToID(property));
                }
                wood.sharedMaterials = materials;
            }
        }
    }

    public override void Interact()
    {
        if (burning || flame == null) return;
        burning = true;
        elapsed = 0f;
        flame.transform.localScale = initialFlameScale;
        if (fireLight != null)
        {
            fireLight.enabled = true;
            fireLight.intensity = initialLightIntensity;
        }
        flame.Play(true);
        if (fireAudio != null && fireAudio.clip != null)
        {
            fireAudio.volume = initialFireVolume;
            fireAudio.Play();
        }
        interactionText = "Ofen brennt";
    }

    private void Update()
    {
        if (!burning) return;
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, growthDuration));
        float growth = Mathf.SmoothStep(0f, 1f, progress);
        if (flame != null)
            flame.transform.localScale = initialFlameScale
                * Mathf.Lerp(1f, fullFlameMultiplier, growth);
        if (fireLight != null)
            fireLight.intensity = Mathf.Lerp(initialLightIntensity, fullLightIntensity, growth);
        if (fireAudio != null)
            fireAudio.volume = Mathf.Lerp(initialFireVolume, fullFireVolume, growth);
        float charProgress = Mathf.Clamp01((elapsed - charringDelay)
            / Mathf.Max(0.1f, charringDuration));
        for (int i = 0; i < woodMaterials.Count; i++)
        {
            Color target = charredColor;
            target.a = originalColors[i].a;
            woodMaterials[i].SetColor(colorProperties[i],
                Color.Lerp(originalColors[i], target, charProgress));
        }
    }

    private void OnDisable()
    {
        if (fireAudio != null) fireAudio.Stop();
    }

    private void OnEnable()
    {
        if (burning && fireAudio != null && fireAudio.clip != null) fireAudio.Play();
    }

    private void OnDestroy()
    {
        foreach (Material material in woodMaterials)
            if (material != null) Destroy(material);
    }
}
