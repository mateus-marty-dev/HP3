using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class ZombieBehaviour : MonoBehaviour
{
    public enum MovementLevel { Schlendern = 1, Spazieren = 2, Rennen = 3 }

    [SerializeField] private MovementLevel movementLevel = MovementLevel.Schlendern;
    [Header("Random gait changes - only when departing")]
    [SerializeField] private Vector2 gaitChangeInterval = new Vector2(30f, 60f);
    [Header("Pause at each destination (seconds)")]
    [SerializeField] private Vector2 idleDuration = new Vector2(3f, 8f);
    [Header("Speed for Schlendern (metres per second)")]
    [Min(0f)] [SerializeField] private float walkSpeed = 0.17f;
    [Header("Destinations - automatically supplemented at Play start")]
    [SerializeField] private Transform[] destinations = new Transform[0];
    [Min(0.1f)] [SerializeField] private float arrivalDistance = 0.3f;
    [Min(0.1f)] [SerializeField] private float objectClearance = 0.7f;

    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int Gait = Animator.StringToHash("Gait");
    private static readonly int GaitSpeed = Animator.StringToHash("GaitSpeed");
    private static readonly HashSet<string> GardenDestinationNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "Pond", "Basket", "beehive", "Wooden_Shed", "Workbench",
        "Shovel", "Tomato_House", "CherryBlossom_4"
    };
    private readonly List<Transform> targets = new List<Transform>();
    private Animator animator;
    private NavMeshAgent agent;
    private GameObject navigationObject;
    private Transform currentTarget;
    private Transform previousTarget;
    private float remainingTime;
    private float stuckTime;
    private float previousDistance;
    private float nextGaitChangeTime;
    private bool travelling;
    private bool initialized;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        ScheduleGaitChange();
    }

    private void Start()
    {
        CollectTargets();
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        agent.radius = 0.25f;
        agent.height = 1.8f;
        agent.angularSpeed = 180f;
        agent.acceleration = 2f;
        agent.stoppingDistance = arrivalDistance;
        agent.autoBraking = true;

        // Reuse a baked surface if available, otherwise build from scene collision geometry.
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, 3f, NavMesh.AllAreas))
        {
            navigationObject = new GameObject("Zombie Navigation");
            NavMeshSurface surface = navigationObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = agent.agentTypeID;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }
        if (!NavMesh.SamplePosition(transform.position, out start, 3f, NavMesh.AllAreas))
        {
            Debug.LogWarning("Zombie cannot find walkable ground. Check the terrain colliders and navigation surface.", this);
            enabled = false;
            return;
        }

        transform.position = start.position;
        agent.enabled = true;
        initialized = agent.isOnNavMesh;
        if (!initialized)
        {
            Debug.LogWarning("Zombie is not on a compatible navigation surface.", this);
            enabled = false;
            return;
        }
        BeginIdle();
    }

    private void OnEnable()
    {
        if (initialized && agent.isOnNavMesh) BeginIdle();
    }

    [ContextMenu("Gartenziele suchen und ergaenzen")]
    private void CollectTargets()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObject(this, "Collect zombie destinations");
#endif
        targets.Clear();
        foreach (Transform target in destinations)
            AddTarget(target);

        foreach (PlantGrowth plant in FindObjectsByType<PlantGrowth>(FindObjectsSortMode.None))
        {
            string name = plant.name.ToLowerInvariant();
            if (name.Contains("tomato") || name.Contains("potato"))
                AddTarget(plant.transform);
        }
        foreach (BucketContents bucket in FindObjectsByType<BucketContents>(FindObjectsSortMode.None))
            AddTarget(bucket.transform);
        foreach (DoorInteractable door in FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None))
            AddTarget(door.transform);
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            // Unity appends suffixes such as " (1)" when objects are duplicated.
            string objectName = candidate.name;
            int suffix = objectName.IndexOf(" (", System.StringComparison.Ordinal);
            if (suffix >= 0) objectName = objectName.Substring(0, suffix);
            if (GardenDestinationNames.Contains(objectName))
                AddTarget(candidate);
        }
        destinations = targets.ToArray();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        if (targets.Count == 0)
            Debug.LogWarning("Zombie has no destinations. Assign scene objects in Destinations.", this);
    }

    private void AddTarget(Transform target)
    {
        if (target != null && target != transform && !target.IsChildOf(transform) && !targets.Contains(target))
            targets.Add(target);
    }

    private void Update()
    {
        if (!initialized || !agent.isOnNavMesh) return;
        int level = Mathf.Clamp((int)movementLevel, 1, 3);
        float multiplier = level == 3 ? 4.6170213f : level == 2 ? 1.6489362f : 1f;
        agent.speed = Mathf.Max(0f, walkSpeed) * multiplier;
        animator.SetFloat(Gait, level);
        float referenceSpeed = 0.4548387f * transform.TransformVector(Vector3.forward).magnitude * multiplier;
        float actualSpeed = agent.velocity.magnitude;
        animator.SetBool(IsWalking, travelling && actualSpeed > 0.01f);
        animator.SetFloat(GaitSpeed, actualSpeed / Mathf.Max(0.0001f, referenceSpeed));

        if (!travelling)
        {
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f) ChooseDestination();
            return;
        }
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            BeginIdle();
            return;
        }
        if (agent.pathPending) return;
        if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete ||
            agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            BeginIdle();
            return;
        }

        // A blocked route must not leave the zombie walking forever.
        if (agent.remainingDistance < previousDistance - 0.02f)
        {
            previousDistance = agent.remainingDistance;
            stuckTime = 0f;
        }
        else
        {
            stuckTime += Time.deltaTime;
            if (stuckTime >= 10f) BeginIdle();
        }
    }

    private void ChooseDestination()
    {
        int count = targets.Count;
        int first = Random.Range(0, Mathf.Max(1, count));
        for (int i = 0; i < count; i++)
        {
            Transform target = targets[(first + i) % count];
            if (target == null || !target.gameObject.activeInHierarchy ||
                (count > 1 && target == previousTarget))
                continue;
            if (!TryApproach(target, out NavMeshPath path)) continue;
            agent.isStopped = false;
            if (!agent.SetPath(path)) continue;
            if (Time.time >= nextGaitChangeTime)
            {
                int currentLevel = Mathf.Clamp((int)movementLevel, 1, 3);
                // Either of the other two gaits, with equal probability.
                movementLevel = (MovementLevel)(1 + (currentLevel - 1 + Random.Range(1, 3)) % 3);
                ScheduleGaitChange();
            }
            currentTarget = target;
            travelling = true;
            previousDistance = float.PositiveInfinity;
            stuckTime = 0f;
            return;
        }
        BeginIdle();
    }

    private void ScheduleGaitChange()
    {
        float minimum = Mathf.Max(1f, Mathf.Min(gaitChangeInterval.x, gaitChangeInterval.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(gaitChangeInterval.x, gaitChangeInterval.y));
        nextGaitChangeTime = Time.time + Random.Range(minimum, maximum);
    }

    private bool TryApproach(Transform target, out NavMeshPath bestPath)
    {
        bestPath = null;
        Bounds bounds = new Bounds(target.position, Vector3.zero);
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            bounds.Encapsulate(renderer.bounds);
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            Vector3 point = bounds.center + new Vector3(
                Mathf.Sin(angle) * (bounds.extents.x + objectClearance), 0f,
                Mathf.Cos(angle) * (bounds.extents.z + objectClearance));
            point.y = bounds.min.y;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, agent.areaMask)) continue;
            NavMeshPath path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;
            float length = 0f;
            for (int corner = 1; corner < path.corners.Length; corner++)
                length += Vector3.Distance(path.corners[corner - 1], path.corners[corner]);
            if (length <= arrivalDistance || length >= bestDistance) continue;
            bestDistance = length;
            bestPath = path;
        }
        return bestPath != null;
    }

    private void BeginIdle()
    {
        if (currentTarget != null) previousTarget = currentTarget;
        currentTarget = null;
        travelling = false;
        agent.isStopped = true;
        agent.ResetPath();
        animator.SetBool(IsWalking, false);
        float minimum = Mathf.Max(0.1f, Mathf.Min(idleDuration.x, idleDuration.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(idleDuration.x, idleDuration.y));
        remainingTime = Random.Range(minimum, maximum);
    }

    private void OnDisable()
    {
        if (animator != null) animator.SetBool(IsWalking, false);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void OnDestroy()
    {
        if (navigationObject != null) Destroy(navigationObject);
    }
}
