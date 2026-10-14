using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RainCollector))]
public class WaterHeating : MonoBehaviour
{
    [Min(0.1f)] [SerializeField] private float secondsToBoil = 20f;
    [Min(0.1f)] [SerializeField] private float secondsToCool = 30f;
    [SerializeField] private ParticleSystem steam;
    [SerializeField] private ParticleSystem bubbles;
    [Range(0f, 1f)] [SerializeField] private float heat;
    [Tooltip("Maximum wave strength at full boil. The script increases toward this value automatically.")]
    [Min(0f)] [SerializeField] private float boilingWaveStrength = 0.1f;

    private RainCollector water;
    private PickupInteractable pickup;
    private Renderer waterRenderer;
    private MaterialPropertyBlock waterProperties;
    private float waterAnimationTime;
    private static readonly int WaveStrengthId = Shader.PropertyToID("_WaveStrength");
    private static readonly int WaterTimeId = Shader.PropertyToID("_WaterTime");
    [Header("Runtime diagnostics")]
    [Tooltip("Actual strength sent to the water shader. Updated automatically during Play Mode.")]
    [SerializeField] private float currentWaveStrength;
    [Tooltip("Animated shader time. Must increase while the water is hot.")]
    [SerializeField] private float currentWaterTime;
    private bool checkedWaterShader;
    private readonly HashSet<StoveHeatZone> sources = new HashSet<StoveHeatZone>();
    public bool IsBoiling => water != null && water.HasWater && heat >= 1f;

    private void Awake()
    {
        water = GetComponent<RainCollector>();
        pickup = GetComponent<PickupInteractable>();
        waterRenderer = water.WaterSurfaceRenderer;
        waterProperties = new MaterialPropertyBlock();
        UpdateWaterShader(0f);
        DisableAutomaticPlayback(steam);
        DisableAutomaticPlayback(bubbles);
        SetEffect(steam, false);
        SetEffect(bubbles, false);
    }

    public void AddHeatSource(StoveHeatZone source) => sources.Add(source);
    public void RemoveHeatSource(StoveHeatZone source) => sources.Remove(source);

    private void Update()
    {
        bool heating = false;
        if (pickup == null || !pickup.IsHeld)
            foreach (StoveHeatZone source in sources)
                if (source != null && source.IsHeating) { heating = true; break; }

        if (!water.HasWater) heat = 0f;
        else heat = Mathf.MoveTowards(heat, heating ? 1f : 0f,
            Time.deltaTime / Mathf.Max(0.1f, heating ? secondsToBoil : secondsToCool));
        float motion = water.HasWater ? Mathf.SmoothStep(0f, 1f, heat) : 0f;
        waterAnimationTime += Time.deltaTime * motion;
        UpdateWaterShader(motion);
        // Start the particles only when the waves have reached their maximum.
        SetEffect(steam, IsBoiling);
        SetEffect(bubbles, IsBoiling);
    }

    private void UpdateWaterShader(float motion)
    {
        currentWaveStrength = boilingWaveStrength * motion;
        currentWaterTime = waterAnimationTime;
        if (waterRenderer == null) waterRenderer = water.WaterSurfaceRenderer;
        if (waterRenderer == null || waterProperties == null) return;
        if (!checkedWaterShader)
        {
            checkedWaterShader = true;
            Material material = waterRenderer.sharedMaterial;
            if (material == null || !material.HasProperty(WaveStrengthId) || !material.HasProperty(WaterTimeId))
                Debug.LogWarning("WaterHeating: assign M_Water_Boiling to the Water mesh. Its shader must expose _WaveStrength and _WaterTime.", waterRenderer);
        }
        waterRenderer.GetPropertyBlock(waterProperties);
        waterProperties.SetFloat(WaveStrengthId, currentWaveStrength);
        waterProperties.SetFloat(WaterTimeId, waterAnimationTime);
        waterRenderer.SetPropertyBlock(waterProperties);
    }

    private static void DisableAutomaticPlayback(ParticleSystem effect)
    {
        if (effect == null) return;
        var main = effect.main;
        main.playOnAwake = false;
    }

    private static void SetEffect(ParticleSystem effect, bool active)
    {
        if (effect == null) return;
        if (active && !effect.isPlaying) effect.Play(false);
        else if (!active && !effect.isStopped)
            effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        SetEffect(steam, false);
        SetEffect(bubbles, false);
        UpdateWaterShader(0f);
    }
}
