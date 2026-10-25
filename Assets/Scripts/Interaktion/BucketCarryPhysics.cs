using System.Collections.Generic;
using UnityEngine;

// Optional transport support. Bodies stay dynamic; only the carrier's change
// of reference frame is removed from their motion.
public sealed class BucketCarryPhysics : MonoBehaviour
{
    private Rigidbody vessel;
    private Bounds cavity;
    private Vector3 scale;
    private readonly HashSet<Rigidbody> contents = new HashSet<Rigidbody>();
    private readonly List<Rigidbody> remove = new List<Rigidbody>();
    private readonly List<Visual> visuals = new List<Visual>();
    private readonly HashSet<Rigidbody> visualBodies = new HashSet<Rigidbody>();
    private bool ready;
    private BoxCollider floorOverride;
    private bool rectangular;
    private Quaternion cavityRotation = Quaternion.identity;
    public void ConfigureFloor(BoxCollider floor, bool rectangle)
    {
        floorOverride = floor;
        rectangular = rectangle;
    }
    [SerializeField] private int trackedContents;
    public IEnumerable<Rigidbody> Contents => contents;

    public bool Initialize()
    {
        if (ready) return true;
        vessel = GetComponent<Rigidbody>();
        scale = transform.lossyScale;
        BoxCollider floor = floorOverride;
        if (floor == null)
            foreach (var box in GetComponentsInChildren<BoxCollider>())
                if (box.attachedRigidbody == vessel && !box.isTrigger && box.name == "ColliderBottom") floor = box;
        if (floor == null) return false;
        if (floorOverride != null)
        {
            Vector3 size = Vector3.Scale(floor.size, floor.transform.lossyScale);
            int thin = Mathf.Abs(size.x) < Mathf.Abs(size.y) ? 0 : 1;
            if (Mathf.Abs(size.z) < Mathf.Abs(size[thin])) thin = 2;
            Vector3 normal = Vector3.zero; normal[thin] = 1f;
            Vector3 worldNormal = floor.transform.TransformDirection(normal);
            if (Vector3.Dot(worldNormal, Vector3.up) < 0f) worldNormal = -worldNormal;
            Vector3 localNormal = transform.InverseTransformDirection(worldNormal);
            cavityRotation = Quaternion.FromToRotation(Vector3.up, localNormal);
        }
        Bounds floorBounds = LocalBounds(floor);
        cavity = floorBounds;
        foreach (var box in GetComponentsInChildren<BoxCollider>())
            if (box.enabled && !box.isTrigger && box.attachedRigidbody == vessel) cavity.Encapsulate(LocalBounds(box));
        Vector3 min = cavity.min, max = cavity.max;
        min.y = floorBounds.max.y;
        // Exclude wall thickness; retain the wider opening of this tapered bucket.
        float inset = 0.02f / Mathf.Max(0.001f, Mathf.Abs(scale.x));
        min.x += inset; min.z += inset; max.x -= inset; max.z -= inset;
        cavity.SetMinMax(min, max);
        ready = cavity.size.y > 0 && cavity.size.x > 0 && cavity.size.z > 0;
        return ready;
    }

    private Bounds LocalBounds(BoxCollider box)
    {
        Bounds result = new Bounds(Quaternion.Inverse(cavityRotation) * transform.InverseTransformPoint(box.transform.TransformPoint(box.center)), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    result.Encapsulate(Quaternion.Inverse(cavityRotation) * transform.InverseTransformPoint(box.transform.TransformPoint(
                        box.center + Vector3.Scale(box.size * 0.5f, new Vector3(x, y, z)))));
        return result;
    }

    private Vector3 LocalPoint(Vector3 point)
    {
        Vector3 p = Quaternion.Inverse(vessel.rotation) * (point - vessel.position);
        return Quaternion.Inverse(cavityRotation) * new Vector3(p.x / scale.x, p.y / scale.y, p.z / scale.z);
    }

    private bool Inside(Rigidbody body, bool retaining)
    {
        Vector3 p = LocalPoint(body.worldCenterOfMass);
        float margin = retaining ? 0.05f / Mathf.Max(0.001f, Mathf.Abs(scale.y)) : 0f;
        if (p.y < cavity.min.y - margin || p.y > cavity.max.y + margin) return false;
        float x = (p.x - cavity.center.x) / (cavity.extents.x + margin);
        float z = (p.z - cavity.center.z) / (cavity.extents.z + margin);
        return rectangular ? Mathf.Abs(x) <= 1f && Mathf.Abs(z) <= 1f : x * x + z * z <= 1f;
    }

    private bool Eligible(Rigidbody body)
    {
        if (body == null || body == vessel || body.isKinematic || !body.gameObject.activeInHierarchy) return false;
        var pickup = body.GetComponent<PickupInteractable>();
        return pickup != null && pickup.isActiveAndEnabled && !pickup.IsHeld;
    }

    public bool Move(Vector3 position, Quaternion rotation)
    {
        if (!Initialize()) return false;
        remove.Clear();
        foreach (var body in contents)
            if (!Eligible(body) || !Inside(body, true)) remove.Add(body);
        foreach (var body in remove) { contents.Remove(body); RemoveVisuals(body); }
        Vector3 center = vessel.position + vessel.rotation * Vector3.Scale(cavityRotation * cavity.center, scale);
        float radius = cavity.extents.magnitude * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        foreach (var col in Physics.OverlapSphere(center, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            var body = col.attachedRigidbody;
            if (!contents.Contains(body) && Eligible(body) && Inside(body, false)) contents.Add(body);
        }
        trackedContents = contents.Count;
        Quaternion delta = rotation * Quaternion.Inverse(vessel.rotation);
        Vector3 previous = vessel.position;
        foreach (var body in contents)
        {
            AddVisuals(body);
            body.position = position + delta * (body.position - previous);
            body.rotation = delta * body.rotation;
            body.linearVelocity = delta * body.linearVelocity;
            body.angularVelocity = delta * body.angularVelocity;
            body.WakeUp();
        }
        AddVisuals(vessel);
        vessel.position = position;
        vessel.rotation = rotation;
        return true;
    }

    // Render copies follow the current camera/hand pose each frame. No physical
    // transform is moved in LateUpdate, and there is no smoothing or trailing.
    public void RenderAt(Vector3 position, Quaternion rotation)
    {
        if (!ready) return;
        Quaternion delta = rotation * Quaternion.Inverse(vessel.rotation);
        foreach (var visual in visuals) visual.Render(position, vessel.position, delta);
    }

    private void AddVisuals(Rigidbody body)
    {
        if (!visualBodies.Add(body)) return;
        foreach (var renderer in body.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.GetComponentInParent<Rigidbody>() != body) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) visuals.Add(new Visual(body, renderer, filter));
        }
    }

    private void RemoveVisuals(Rigidbody body)
    {
        for (int i = visuals.Count - 1; i >= 0; i--)
            if (visuals[i].body == body) { visuals[i].Dispose(); visuals.RemoveAt(i); }
        visualBodies.Remove(body);
    }

    public void Release()
    {
        foreach (var visual in visuals) visual.Dispose();
        visuals.Clear(); visualBodies.Clear(); contents.Clear();
        trackedContents = 0;
    }
    private void OnDisable() => Release();

    private void OnDrawGizmosSelected()
    {
        if (!ready) return;
        Gizmos.matrix = Matrix4x4.TRS(vessel.position, vessel.rotation, scale) * Matrix4x4.Rotate(cavityRotation);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(cavity.center, cavity.size);
        Gizmos.matrix = Matrix4x4.identity;
    }

    private sealed class Visual
    {
        public readonly Rigidbody body;
        private readonly MeshRenderer source, copy;
        private readonly GameObject view;
        private readonly bool originalForceOff;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        public Visual(Rigidbody owner, MeshRenderer renderer, MeshFilter filter)
        {
            body = owner; source = renderer; originalForceOff = source.forceRenderingOff;
            view = new GameObject("Carry visual: " + renderer.name) { hideFlags = HideFlags.HideAndDontSave, layer = renderer.gameObject.layer };
            view.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            copy = view.AddComponent<MeshRenderer>();
            copy.sharedMaterials = source.sharedMaterials;
            copy.shadowCastingMode = source.shadowCastingMode; copy.receiveShadows = source.receiveShadows;
            copy.lightProbeUsage = source.lightProbeUsage; copy.reflectionProbeUsage = source.reflectionProbeUsage;
            copy.renderingLayerMask = source.renderingLayerMask;
            copy.enabled = false;
            source.forceRenderingOff = true;
        }
        public void Render(Vector3 target, Vector3 origin, Quaternion delta)
        {
            if (source == null || body == null) { copy.enabled = false; return; }
            copy.enabled = source.enabled && source.gameObject.activeInHierarchy && !originalForceOff;
            // Preserve interpolated movement relative to each fruit's physics pose.
            Vector3 local = Quaternion.Inverse(body.transform.rotation) * (source.transform.position - body.transform.position);
            Quaternion localRotation = Quaternion.Inverse(body.transform.rotation) * source.transform.rotation;
            view.transform.SetPositionAndRotation(target + delta * (body.position + body.rotation * local - origin), delta * body.rotation * localRotation);
            view.transform.localScale = source.transform.lossyScale;
            source.GetPropertyBlock(properties); copy.SetPropertyBlock(properties);
            for (int i = 0; i < source.sharedMaterials.Length; i++)
            {
                source.GetPropertyBlock(properties, i);
                copy.SetPropertyBlock(properties, i);
            }
        }
        public void Dispose()
        {
            if (source != null) source.forceRenderingOff = originalForceOff;
            if (view != null) { view.SetActive(false); Object.Destroy(view); }
        }
    }
}
