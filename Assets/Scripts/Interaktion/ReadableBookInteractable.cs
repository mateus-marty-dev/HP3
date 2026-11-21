
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ReadableBookInteractable : Interactable
{
    [Header("Book Animations")]
    [SerializeField] private GameObject animationRoot;
    [SerializeField] private AnimationClip openingClip;
    [SerializeField] private AnimationClip[] pageClips;

    [Min(0.1f)]
    [SerializeField] private float playbackSpeed = 1f;

    [Header("Interaction Collider")]
    [SerializeField] private BoxCollider interactionCollider;
    [SerializeField] private Bounds closedInteractionBounds;
    [SerializeField] private Bounds openInteractionBounds;

    [Header("Pickup")]
    [SerializeField] private PickupInteractable pickup;

    [Header("Reading Position")]
    [SerializeField] private Transform backCoverOutside;
    [SerializeField] private LayerMask readingSurfaceLayers = ~0;

    [Range(0.5f, 1f)]
    [SerializeField] private float minimumAlignment = 0.9f;

    [Min(0.01f)]
    [SerializeField] private float supportDistance = 0.15f;

    [Min(0f)]
    [SerializeField] private float maxReadingVelocity = 0.25f;

    private Rigidbody bookRigidbody;

    public bool IsOpen { get; private set; }
    public int TurnedPages { get; private set; }
    public bool IsAnimating => activeClip != null;

    private AnimationClip activeClip;
    private float clipTime;
    private bool playingBackwards;
    private bool nextOpen;
    private int nextPage;

    // Returns true only when the closed or open book
    // is resting on its back cover.
    public bool CanRead
    {
        get
        {
            if (pickup == null || backCoverOutside == null)
                return false;

            if (pickup.IsHeld)
                return false;

            // Do not start reading while the book is moving.
            if (bookRigidbody != null &&
                !bookRigidbody.isKinematic &&
                bookRigidbody.linearVelocity.sqrMagnitude >
                maxReadingVelocity * maxReadingVelocity)
            {
                return false;
            }

            // Back cover's local +Y must point outward.
            // When resting on the back cover, it points down.
            float alignment = Vector3.Dot(
                backCoverOutside.up,
                Vector3.down
            );

            if (alignment < minimumAlignment)
                return false;

            // Start slightly above the outside surface.
            Vector3 origin =
                backCoverOutside.position -
                backCoverOutside.up * 0.02f;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                supportDistance + 0.02f,
                readingSurfaceLayers,
                QueryTriggerInteraction.Ignore
            );

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                // Ignore the book's own colliders.
                if (hit.collider.transform.IsChildOf(pickup.transform))
                    continue;

                // Only accept reasonably upward-facing surfaces.
                if (Vector3.Dot(hit.normal, Vector3.up) < 0.6f)
                    continue;

                return true;
            }

            return false;
        }
    }

    private void Awake()
    {
        if (pickup == null)
            pickup = GetComponent<PickupInteractable>();

        bookRigidbody = GetComponent<Rigidbody>();

        if (pickup != null)
            pickup.HeldStateChanged += OnHeldStateChanged;

        if (animationRoot == null ||
            openingClip == null ||
            pageClips == null ||
            pageClips.Length == 0)
        {
            Debug.LogError(
                "Buchanimationen nicht vollstaendig zugewiesen.",
                this
            );

            enabled = false;
            return;
        }

        // Prevent imported animators from interfering
        // with manually sampled animation clips.
        foreach (Animator animator in
                 animationRoot.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
        }

        ResetToClosed();
    }

    private void OnDestroy()
    {
        if (pickup != null)
            pickup.HeldStateChanged -= OnHeldStateChanged;
    }

    private void OnHeldStateChanged(bool held)
    {
        if (held)
            ResetToClosed();
    }

    // Reset to the initial closed book pose.
    private void ResetToClosed()
    {
        activeClip = null;
        clipTime = 0f;
        playingBackwards = false;

        IsOpen = false;
        TurnedPages = 0;

        if (animationRoot != null)
        {
            // Reset every individual page to its initial pose.
            if (pageClips != null)
            {
                foreach (AnimationClip clip in pageClips)
                {
                    if (clip != null)
                        clip.SampleAnimation(animationRoot, 0f);
                }
            }

            // Reset covers.
            if (openingClip != null)
                openingClip.SampleAnimation(animationRoot, 0f);
        }

        UpdateInteractionBounds(false);
        UpdateInteractionText();
    }

    public override void Interact()
    {
        if (!CanRead || IsAnimating)
            return;

        if (!IsOpen)
        {
            Begin(
                openingClip,
                false,
                true,
                0
            );
        }
        else if (TurnedPages < pageClips.Length)
        {
            Begin(
                pageClips[TurnedPages],
                false,
                true,
                TurnedPages + 1
            );
        }
    }

    public void InteractReverse()
    {
        if (!CanRead || IsAnimating || !IsOpen)
            return;

        if (TurnedPages > 0)
        {
            Begin(
                pageClips[TurnedPages - 1],
                true,
                true,
                TurnedPages - 1
            );
        }
        else
        {
            Begin(
                openingClip,
                true,
                false,
                0
            );
        }
    }

    private void Begin(
        AnimationClip clip,
        bool backwards,
        bool openAfterwards,
        int pageAfterwards)
    {
        if (clip == null)
            return;

        activeClip = clip;

        playingBackwards = backwards;
        clipTime = backwards ? clip.length : 0f;

        nextOpen = openAfterwards;
        nextPage = pageAfterwards;

        UpdateInteractionBounds(true);

        activeClip.SampleAnimation(
            animationRoot,
            clipTime
        );

        interactionText = "Buch bewegt sich ...";
    }

    private void Update()
    {
        Advance(Time.deltaTime);
    }

    private void Advance(float seconds)
    {
        if (activeClip == null)
            return;

        float end =
            playingBackwards ? 0f : activeClip.length;

        clipTime = Mathf.MoveTowards(
            clipTime,
            end,
            Mathf.Max(0f, seconds) * playbackSpeed
        );

        activeClip.SampleAnimation(
            animationRoot,
            clipTime
        );

        if (Mathf.Approximately(clipTime, end))
            Finish();
    }

    private void Finish()
    {
        if (activeClip == null)
            return;

        activeClip.SampleAnimation(
            animationRoot,
            playingBackwards ? 0f : activeClip.length
        );

        activeClip = null;

        IsOpen = nextOpen;
        TurnedPages = nextPage;

        UpdateInteractionBounds(IsOpen);
        UpdateInteractionText();
    }

    private void OnDisable()
    {
        if (activeClip != null && animationRoot != null)
            Finish();
    }

    private void UpdateInteractionBounds(bool open)
    {
        if (interactionCollider == null)
            return;

        Bounds bounds =
            open ? openInteractionBounds : closedInteractionBounds;

        interactionCollider.center = bounds.center;
        interactionCollider.size = bounds.size;
    }

    private void UpdateInteractionText()
    {
        if (!IsOpen)
        {
            interactionText =
                "F / Linksklick - Buch oeffnen";
        }
        else if (TurnedPages == 0)
        {
            interactionText =
                "F / Linksklick - Blaettern   " +
                "Q / Rechtsklick - Schliessen";
        }
        else if (TurnedPages == pageClips.Length)
        {
            interactionText =
                "Q / Rechtsklick - Zurueckblaettern";
        }
        else
        {
            interactionText =
                "F / Linksklick - Weiter   " +
                "Q / Rechtsklick - Zurueck";
        }
    }
}
