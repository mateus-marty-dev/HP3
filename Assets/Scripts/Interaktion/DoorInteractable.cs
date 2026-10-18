using UnityEngine;

public class DoorInteractable : Interactable
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;

    [Header("Door sound")]
    [SerializeField] private AudioClip doorSound;
    [SerializeField] private AudioClip closeSound;
    [Tooltip("Time within each clip at which the door reaches its end position.")]
    [SerializeField, Min(0.01f)] private float openArrivalTime = 0.85f;
    [SerializeField, Min(0.01f)] private float closeArrivalTime = 0.45f;
    [SerializeField] private AudioSource doorAudio;
    [SerializeField, Range(0f, 1f)] private float doorVolume = 0.75f;

    private void Awake()
    {
        if (doorAudio == null)
        {
            doorAudio = gameObject.AddComponent<AudioSource>();
            doorAudio.spatialBlend = 1f;
            doorAudio.minDistance = 1.5f;
            doorAudio.maxDistance = 20f;
        }
        doorAudio.playOnAwake = false;
        doorAudio.loop = false;
    }

    private void OnDisable()
    {
        if (doorAudio != null) doorAudio.Stop();
        moving = false;
    }

    private bool isOpen = false;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;
    private Quaternion movementStart;
    private bool moving;
    private float elapsed;
    private float duration;
    private bool followAudio;

    private void Start()
    {
        closedRotation = transform.localRotation;

        // Falls deine Tür um Z dreht:
        openRotation = closedRotation * Quaternion.Euler(0f, 0f, openAngle);

        targetRotation = closedRotation;
    }

    private void Update()
    {
        if (!moving) return;
        elapsed += Time.deltaTime;
        // Use the audio playback clock, including a delayed audio start.
        float clock = followAudio && doorAudio.isPlaying ? doorAudio.time : elapsed;
        float progress = Mathf.Clamp01(clock / duration);
        transform.localRotation = Quaternion.Slerp(movementStart, targetRotation,
            Mathf.SmoothStep(0f, 1f, progress));
        if (progress >= 1f) moving = false;
    }

    public override void Interact()
    {
        // Finish the current movement before accepting another command.
        if (moving) return;
        isOpen = !isOpen;

        targetRotation = isOpen
            ? openRotation
            : closedRotation;

        movementStart = transform.localRotation;
        elapsed = 0f;
        moving = true;
        AudioClip clip = isOpen ? doorSound : closeSound;
        followAudio = doorAudio != null && clip != null;
        duration = followAudio
            ? Mathf.Clamp(isOpen ? openArrivalTime : closeArrivalTime, 0.01f, clip.length)
            : 1f / Mathf.Max(0.01f, openSpeed);
        if (followAudio)
        {
            doorAudio.Stop();
            doorAudio.clip = clip;
            doorAudio.pitch = 1f;
            doorAudio.volume = doorVolume;
            doorAudio.Play();
        }
    }
}
