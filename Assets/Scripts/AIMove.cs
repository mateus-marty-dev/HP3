using UnityEngine;

[DisallowMultipleComponent]
public class AIMove : MonoBehaviour
{
    public AISpawner manager;
    [Min(0.01f)] public float minSpeed = 0.35f;
    [Min(0.01f)] public float maxSpeed = 0.65f;
    [Min(1f)] public float turnSpeed = 90f;
    public Transform visual;
    public Vector3 modelRotationOffset;
    private Transform target;
    private float speed;
    private Animator animator;

    void Start()
    {
        if (manager == null) manager = GetComponentInParent<AISpawner>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        if (manager == null)
        {
            Debug.LogWarning(name + ": AISpawner fehlt.", this);
            enabled = false;
        }
    }

    void Update() { Step(Time.deltaTime); }

    // Also used by the editor validation with a fixed simulation step.
    public void Step(float deltaTime)
    {
        if (manager == null) return;
        if (target == null || Vector3.Distance(transform.position, target.position) < 0.03f)
        {
            if (!manager.TryGetWaypoint(target, out target)) return;
            speed = Random.Range(Mathf.Max(0.01f, minSpeed), Mathf.Max(minSpeed, maxSpeed));
            if (animator != null) animator.speed = speed / Mathf.Max(0.01f, minSpeed);
        }
        Vector3 direction = target.position - transform.position;
        if (direction.sqrMagnitude < 0.000001f) return;
        // Straight segments stay within the convex route area; the visual turns smoothly.
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * deltaTime);
        Transform facing = visual != null ? visual : transform;
        Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(modelRotationOffset);
        facing.rotation = Quaternion.RotateTowards(facing.rotation, rotation, turnSpeed * deltaTime);
    }
}
