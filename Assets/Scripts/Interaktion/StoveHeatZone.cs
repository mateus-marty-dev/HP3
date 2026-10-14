using System.Collections.Generic;
using UnityEngine;

public class StoveHeatZone : MonoBehaviour
{
    [SerializeField] private StoveFireInteractable stoveFire;
    [SerializeField] private BoxCollider heatTrigger;
    private readonly HashSet<WaterHeating> occupants = new HashSet<WaterHeating>();

    public bool IsHeating => isActiveAndEnabled && stoveFire != null && stoveFire.IsBurning;

    private void FixedUpdate()
    {
        var current = new HashSet<WaterHeating>();
        if (heatTrigger != null && heatTrigger.enabled && heatTrigger.isTrigger)
        {
            Transform box = heatTrigger.transform;
            Vector3 scale = box.lossyScale;
            Vector3 halfSize = Vector3.Scale(heatTrigger.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            // Query the assigned trigger explicitly: multiple colliders on the
            // grate must not cause duplicate heat or ambiguous exit events.
            foreach (Collider hit in Physics.OverlapBox(box.TransformPoint(heatTrigger.center),
                         halfSize, box.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                WaterHeating water = hit.GetComponentInParent<WaterHeating>();
                if (water != null && water.isActiveAndEnabled) current.Add(water);
            }
        }
        foreach (WaterHeating water in occupants)
            if (water != null && !current.Contains(water)) water.RemoveHeatSource(this);
        foreach (WaterHeating water in current) water.AddHeatSource(this);
        occupants.Clear();
        occupants.UnionWith(current);
    }

    private void OnDisable()
    {
        foreach (WaterHeating water in occupants)
            if (water != null) water.RemoveHeatSource(this);
        occupants.Clear();
    }
}
