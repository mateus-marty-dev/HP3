using System.Collections.Generic;
using UnityEngine;

/// <summary>Stabilizes loose pickups inside a carried bucket, releasing them on drop or tilt.</summary>
public class BucketContents : MonoBehaviour
{
    private struct CarriedItem
    {
        public Rigidbody body;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public bool useGravity;
        public CollisionDetectionMode collisionMode;
        public Collider[] colliders;
        public bool[] ignoredPlayer;
    }

    private readonly List<CarriedItem> items = new List<CarriedItem>();
    private Vector3 bottom, axis;
    private float height, bottomRadius, topRadius, releaseAngle;
    private Collider player;
    private bool configured;

    public void Configure(Transform bottomSurface, Transform rimSurface, float tiltAngle)
    {
        if (bottomSurface == null || rimSurface == null) return;
        MeshFilter lower = bottomSurface.GetComponent<MeshFilter>();
        MeshFilter upper = rimSurface.GetComponent<MeshFilter>();
        if (lower == null || upper == null || lower.sharedMesh == null || upper.sharedMesh == null) return;
        bottom = transform.InverseTransformPoint(lower.transform.TransformPoint(lower.sharedMesh.bounds.center));
        Vector3 top = transform.InverseTransformPoint(upper.transform.TransformPoint(upper.sharedMesh.bounds.center));
        height = Vector3.Distance(bottom, top);
        if (height <= Mathf.Epsilon) return;
        axis = (top - bottom) / height;
        bottomRadius = SurfaceRadius(lower);
        topRadius = SurfaceRadius(upper);
        releaseAngle = tiltAngle;
        configured = true;
    }

    private float SurfaceRadius(MeshFilter mesh)
    {
        Bounds bounds = mesh.sharedMesh.bounds;
        Vector3 center = transform.InverseTransformPoint(mesh.transform.TransformPoint(bounds.center));
        float radius = 0f;
        // Axis extrema give the radius of the circular water surface, not its bounding-box diagonal.
        foreach (Vector3 direction in new[] { Vector3.right, Vector3.up, Vector3.forward })
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, direction);
            Vector3 offset = transform.InverseTransformPoint(mesh.transform.TransformPoint(point)) - center;
            radius = Mathf.Max(radius, Vector3.ProjectOnPlane(offset, axis).magnitude);
        }
        return radius;
    }

    public void BeginCarry(Collider playerCollider)
    {
        player = playerCollider;
        CaptureContents();
    }

    private bool Upright(Quaternion rotation)
    {
        return Vector3.Angle(rotation * axis, Vector3.up) < releaseAngle;
    }

    private void CaptureContents()
    {
        if (!configured || !Upright(transform.rotation)) return;
        Vector3 scale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 center = transform.TransformPoint(bottom + axis * height * 0.5f);
        float radius = Mathf.Sqrt(height * height * 0.25f + topRadius * topRadius) * maxScale;
        foreach (Collider candidate in Physics.OverlapSphere(center, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            Rigidbody body = candidate.attachedRigidbody;
            if (body == null || body.isKinematic || body.transform == transform) continue;
            PickupInteractable pickup = body.GetComponent<PickupInteractable>();
            if (pickup == null || pickup.IsHeld || !pickup.isActiveAndEnabled) continue;
            Vector3 offset = transform.InverseTransformPoint(candidate.bounds.center) - bottom;
            float level = Vector3.Dot(offset, axis);
            if (level < 0f || level > height) continue;
            float insideRadius = Mathf.Lerp(bottomRadius, topRadius, level / height);
            if (Vector3.ProjectOnPlane(offset, axis).sqrMagnitude > insideRadius * insideRadius) continue;

            Collider[] colliders = body.GetComponentsInChildren<Collider>();
            bool[] ignored = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
                if (player != null)
                {
                    ignored[i] = Physics.GetIgnoreCollision(colliders[i], player);
                    Physics.IgnoreCollision(colliders[i], player, true);
                }
            items.Add(new CarriedItem
            {
                body = body, localPosition = transform.InverseTransformPoint(body.position),
                localRotation = Quaternion.Inverse(transform.rotation) * body.rotation,
                useGravity = body.useGravity, collisionMode = body.collisionDetectionMode,
                colliders = colliders, ignoredPlayer = ignored
            });
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    public void MoveContents(Vector3 position, Quaternion rotation)
    {
        if (!configured) return;
        if (!Upright(rotation)) { ReleaseContents(); return; }
        CaptureContents();
        foreach (CarriedItem item in items)
        {
            if (item.body == null) continue;
            item.body.MovePosition(position + rotation * Vector3.Scale(item.localPosition, transform.lossyScale));
            item.body.MoveRotation(rotation * item.localRotation);
        }
    }

    public void ReleaseContents()
    {
        foreach (CarriedItem item in items)
        {
            if (item.body == null) continue;
            item.body.isKinematic = false;
            item.body.useGravity = item.useGravity;
            item.body.collisionDetectionMode = item.collisionMode;
            item.body.linearVelocity = Vector3.zero;
            item.body.angularVelocity = Vector3.zero;
            for (int i = 0; i < item.colliders.Length; i++)
                if (player != null && item.colliders[i] != null)
                    Physics.IgnoreCollision(item.colliders[i], player, item.ignoredPlayer[i]);
            item.body.WakeUp();
        }
        items.Clear();
    }

    private void OnDisable() { ReleaseContents(); }
}
