using UnityEngine;

/// <summary>Contact tuning only: never anchors contents or disables gravity.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public sealed class GentleItemPhysics : MonoBehaviour
{
    [SerializeField] private bool vessel;
    public bool IsVessel => vessel;
    private Rigidbody body;

    public static void Configure(Rigidbody target, bool isVessel)
    {
        var tuning = target.GetComponent<GentleItemPhysics>();
        if (tuning == null) tuning = target.gameObject.AddComponent<GentleItemPhysics>();
        tuning.vessel = isVessel;
        tuning.ApplySettings();
    }

    private void Awake() => ApplySettings();

    public void ApplySettings()
    {
        body = GetComponent<Rigidbody>();
        body.maxDepenetrationVelocity = 1f;
        body.linearDamping = vessel ? 0.1f : 0.05f;
        body.angularDamping = vessel ? 0.5f : 0.35f;
        body.solverIterations = 16;
        body.solverVelocityIterations = 8;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        UpdateCollisionMode();

        PhysicsMaterial material = Resources.Load<PhysicsMaterial>("Physics/GentleContents");
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col.attachedRigidbody != body || col.isTrigger) continue;
            if (material != null) col.sharedMaterial = material;
            col.contactOffset = 0.002f;
        }
    }

    public void UpdateCollisionMode()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        body.collisionDetectionMode = body.isKinematic
            ? CollisionDetectionMode.ContinuousSpeculative
            : CollisionDetectionMode.ContinuousDynamic;
    }
}
