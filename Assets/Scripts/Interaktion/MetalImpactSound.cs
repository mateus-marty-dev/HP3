using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class MetalImpactSound : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField] private AudioSource impactSource;
    [SerializeField] private float minimumImpactSpeed = 0.25f;
    [SerializeField] private float fullVolumeSpeed = 6f;
    [SerializeField, Range(0f, 1f)] private float maximumVolume = 0.85f;
    [SerializeField] private float minimumInterval = 0.10f;
    [SerializeField] private float rollingAngle = 30f;

    private Rigidbody body;
    private PickupInteractable pickup;
    private Vector3 previousVelocity;
    private Vector3 previousAngularVelocity;
    private float nextSoundTime;
    private float rotationSinceSound;
    private int lastClip = -1;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        pickup = GetComponent<PickupInteractable>();
        if (impactSource == null)
        {
            impactSource = gameObject.AddComponent<AudioSource>();
            impactSource.playOnAwake = false;
            impactSource.spatialBlend = 1f;
            impactSource.minDistance = 1.5f;
            impactSource.maxDistance = 20f;
        }
        impactSource.loop = false;
    }

    private bool CanSound => body != null && !body.isKinematic &&
        !body.IsSleeping() && (pickup == null || !pickup.IsHeld);

    private void FixedUpdate()
    {
        previousVelocity = body.linearVelocity;
        previousAngularVelocity = body.angularVelocity;
        if (!CanSound)
        {
            rotationSinceSound = 0f;
            if (impactSource.isPlaying) impactSource.Stop();
            return;
        }
        rotationSinceSound = Mathf.Min(rollingAngle,
            rotationSinceSound + previousAngularVelocity.magnitude * Mathf.Rad2Deg * Time.fixedDeltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (CanSound) PlayImpact(ImpactSpeed(collision));
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!CanSound) return;
        float impact = ImpactSpeed(collision);
        // A rolling rim can remain in contact throughout a turn. Rotation gates
        // these quieter taps; constant gravity support alone never triggers them.
        float radius = 0f;
        for (int i = 0; i < collision.contactCount; i++)
            radius = Mathf.Max(radius, Vector3.Distance(collision.GetContact(i).point, body.worldCenterOfMass));
        float rimSpeed = previousAngularVelocity.magnitude * radius;
        bool rolling = rotationSinceSound >= rollingAngle &&
            previousAngularVelocity.magnitude > 0.8f && rimSpeed > minimumImpactSpeed;
        if (impact > minimumImpactSpeed || rolling)
            PlayImpact(Mathf.Max(impact, rolling ? rimSpeed * 0.35f : 0f));
    }

    private float ImpactSpeed(Collision collision)
    {
        float speed = 0f;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 pointVelocity = previousVelocity + Vector3.Cross(previousAngularVelocity,
                contact.point - body.worldCenterOfMass);
            if (collision.rigidbody != null)
                pointVelocity -= collision.rigidbody.GetPointVelocity(contact.point);
            speed = Mathf.Max(speed, -Vector3.Dot(pointVelocity, contact.normal));
            speed = Mathf.Max(speed, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal)));
        }
        return speed;
    }

    private void PlayImpact(float speed)
    {
        if (speed < minimumImpactSpeed || Time.time < nextSoundTime || clips == null || clips.Length == 0) return;
        int valid = 0;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && i != lastClip) valid++;
        int chosen = lastClip;
        if (valid > 0)
        {
            int selection = Random.Range(0, valid);
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && i != lastClip && selection-- == 0) { chosen = i; break; }
        }
        if (chosen < 0 || clips[chosen] == null) return;
        float strength = Mathf.InverseLerp(minimumImpactSpeed, Mathf.Max(minimumImpactSpeed + 0.01f, fullVolumeSpeed), speed);
        impactSource.PlayOneShot(clips[chosen], Mathf.Lerp(0.06f, maximumVolume, Mathf.Sqrt(strength)));
        lastClip = chosen;
        nextSoundTime = Time.time + minimumInterval;
        rotationSinceSound = 0f;
    }
}
