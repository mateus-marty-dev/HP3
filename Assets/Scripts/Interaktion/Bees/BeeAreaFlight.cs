using UnityEngine;

// Attach to an empty parent of the animated bee. Its blue Z axis is forward.
public class BeeAreaFlight : MonoBehaviour
{
    [SerializeField] private BoxCollider flightArea;
    [SerializeField, Min(0.01f)] private float speed = 0.35f;
    [SerializeField, Min(0f)] private float turnSpeed = 180f;
    [Tooltip("Inset as a fraction of each box dimension; leaves room for the wings.")]
    [SerializeField, Range(0f, 0.45f)] private float edgeMargin = 0.1f;
    [SerializeField, Min(0f)] private float minimumPause = 0.15f;
    [SerializeField, Min(0f)] private float maximumPause = 0.7f;

    [Header("Rain shelter")]
    [SerializeField] private WeatherManager weatherManager;
    [Tooltip("Optional entrance. Otherwise the scene object beehive is used.")]
    [SerializeField] private Transform hiveEntrance;
    [Tooltip("Optional individual resting place inside the hive. Otherwise generated from the hive bounds.")]
    [SerializeField] private Transform hiveRestPoint;
    [SerializeField, Min(0.01f)] private float returnSpeed = 1.5f;
    [SerializeField, Min(0f)] private float shelterSearchSeconds = 2f;
    private float shelterSearchTimer;
    private Animator[] beeAnimators;
    private float[] animationSpeeds;
    private Transform hive;
    private Vector3 localRestPosition;
    private Vector3 localEntrancePosition;
    private bool automaticEntrance;
    private bool reachedEntrance;
    private Vector3 visualCenterLocal;
    private float beeRadius;
    private bool hasRestPosition;
    private AudioSource[] beeAudio;
    private bool[] audioStates;
    private bool sheltered;
    private bool returning;

    private Vector3 target;
    private float pause;
    private bool arrived;

    private void Start()
    {
        if (weatherManager == null)
            weatherManager = FindFirstObjectByType<WeatherManager>();
        if (hiveEntrance == null)
        {
            GameObject hiveObject = GameObject.Find("beehive");
            if (hiveObject != null)
            {
                hiveEntrance = hiveObject.transform;
                automaticEntrance = true;
            }
        }
        hive = hiveEntrance;
        beeAnimators = GetComponentsInChildren<Animator>(true);
        animationSpeeds = new float[beeAnimators.Length];
        beeAudio = GetComponentsInChildren<AudioSource>(true);
        audioStates = new bool[beeAudio.Length];
        if (hiveEntrance == null)
            Debug.LogWarning("Assign a Hive Entrance for rain shelter to " + name, this);
        if (flightArea == null)
        {
            Debug.LogWarning("Assign a Flight Area to " + name, this);
            enabled = false;
            return;
        }
        transform.position = ClampToArea(transform.position);
        CacheBeeSize();
        ConfigureRestPosition();
        PickTarget();
        if (IsRaining && hiveEntrance != null && hasRestPosition)
        {
            transform.position = RestPosition;
            SetSheltered(true);
            returning = true;
            reachedEntrance = true;
            shelterSearchTimer = shelterSearchSeconds;
        }
    }

    private Vector3 RestPosition => (hiveRestPoint != null ? hiveRestPoint.position :
        hive != null ? hive.TransformPoint(localRestPosition) : transform.position)
        - transform.TransformVector(visualCenterLocal);
    private Vector3 EntrancePosition => automaticEntrance ?
        hive.TransformPoint(localEntrancePosition) : hiveEntrance.position;

    private void ConfigureRestPosition()
    {
        if (hive == null) return;
        GameObject hiveObject = GameObject.Find("beehive");
        Transform geometryRoot = hiveObject != null ? hiveObject.transform : hive;
        MeshFilter stock = null;
        foreach (MeshFilter mesh in geometryRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null) continue;
            if (stock == null) stock = mesh;
            if (mesh.name == "Torus") { stock = mesh; break; }
        }
        if (stock == null || !stock.sharedMesh.isReadable)
        {
            Debug.LogWarning("Bee shelter needs the readable Torus mesh of beehive.", this);
            return;
        }
        Vector3[] vertices = stock.sharedMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = stock.transform.TransformPoint(vertices[i]);
        int[] triangles = stock.sharedMesh.triangles;
        Bounds bounds = new Bounds(vertices[0], Vector3.zero);
        foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex);
        if (!hasRestPosition)
            ReserveRestPlaces(vertices, triangles, bounds);
        if (!hasRestPosition)
            Debug.LogWarning("No free resting place fits this bee inside the hive. Assign Hive Rest Point or check bee size.", this);
        Vector3 towardArea = Vector3.ProjectOnPlane(
            flightArea.transform.TransformPoint(flightArea.center) - bounds.center, Vector3.up).normalized;
        if (towardArea.sqrMagnitude < 0.001f) towardArea = Vector3.forward;
        Vector3 entrance = bounds.center + Vector3.Scale(towardArea, bounds.extents);
        localEntrancePosition = hive.InverseTransformPoint(entrance);
    }

    private void ReserveRestPlaces(Vector3[] vertices, int[] triangles, Bounds bounds)
    {
        var bees = new System.Collections.Generic.List<BeeAreaFlight>();
        foreach (BeeAreaFlight bee in FindObjectsByType<BeeAreaFlight>(FindObjectsSortMode.InstanceID))
        {
            if (bee.flightArea != flightArea) continue;
            bee.CacheBeeSize();
            if (bee.hive == null) bee.hive = hive;
            bees.Add(bee);
        }
        // Allocate together: move an earlier reservation if it blocks another bee.
        bees.Sort((a, b) => b.beeRadius.CompareTo(a.beeRadius));
        var candidates = new System.Collections.Generic.List<Vector3>[bees.Count];
        for (int i = 0; i < bees.Count; i++)
            candidates[i] = new System.Collections.Generic.List<Vector3>();
        // A fixed grid covers the stock independently of luck in random attempts.
        const int divisions = 12;
        for (int x = 1; x < divisions; x++)
        for (int y = 1; y < divisions; y++)
        for (int z = 1; z < divisions; z++)
        {
            Vector3 point = bounds.min + Vector3.Scale(bounds.size,
                new Vector3((float)x / divisions, (float)y / divisions, (float)z / divisions));
            for (int i = 0; i < bees.Count; i++)
                if (bees[i].hiveRestPoint == null &&
                    InsideStock(point, bees[i].beeRadius, vertices, triangles, bounds))
                    candidates[i].Add(point);
        }
        for (int i = 0; i < bees.Count; i++)
        {
            if (bees[i].hiveRestPoint != null) candidates[i].Add(bees[i].hiveRestPoint.position);
            // Random selection, but with all checked alternatives available.
            for (int j = candidates[i].Count - 1; j > 0; j--)
            {
                int other = Random.Range(0, j + 1);
                Vector3 swap = candidates[i][j];
                candidates[i][j] = candidates[i][other];
                candidates[i][other] = swap;
            }
        }
        Vector3[] chosen = new Vector3[bees.Count];
        if (!AssignPlaces(0, bees, candidates, chosen)) return;
        for (int i = 0; i < bees.Count; i++)
        {
            bees[i].localRestPosition = bees[i].hive.InverseTransformPoint(chosen[i]);
            bees[i].hasRestPosition = true;
        }
    }

    private static bool AssignPlaces(int index, System.Collections.Generic.List<BeeAreaFlight> bees,
        System.Collections.Generic.List<Vector3>[] candidates, Vector3[] chosen)
    {
        if (index == bees.Count) return true;
        foreach (Vector3 candidate in candidates[index])
        {
            bool free = true;
            for (int earlier = 0; earlier < index; earlier++)
                if (Vector3.Distance(candidate, chosen[earlier]) < bees[index].beeRadius + bees[earlier].beeRadius)
                { free = false; break; }
            if (!free) continue;
            chosen[index] = candidate;
            if (AssignPlaces(index + 1, bees, candidates, chosen)) return true;
        }
        return false;
    }

    private bool FlyToShelterPoint(Vector3 center)
    {
        // Rotate around the visible bee, not around the imported root pivot.
        // Preserve its world center BEFORE turning so rotation cannot undo progress.
        Vector3 previousCenter = transform.TransformPoint(visualCenterLocal);
        Vector3 heading = Vector3.ProjectOnPlane(center - previousCenter, Vector3.up);
        if (heading.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(heading, Vector3.up), turnSpeed * Time.deltaTime);
        Vector3 nextCenter = Vector3.MoveTowards(previousCenter, center, returnSpeed * Time.deltaTime);
        transform.position = nextCenter - transform.TransformVector(visualCenterLocal);
        if ((nextCenter - center).sqrMagnitude > 0.0001f) return false;
        transform.position = center - transform.TransformVector(visualCenterLocal);
        return true;
    }

    private void CacheBeeSize()
    {
        Renderer[] meshes = GetComponentsInChildren<Renderer>();
        if (meshes.Length == 0) { beeRadius = 0.01f; return; }
        Bounds bounds = meshes[0].bounds;
        foreach (Renderer mesh in meshes) bounds.Encapsulate(mesh.bounds);
        visualCenterLocal = transform.InverseTransformPoint(bounds.center);
        beeRadius = bounds.extents.magnitude + 0.005f;
    }

    // Check the actual mesh envelope from six sides, then leave a full bee-sized
    // sphere clear of every triangle. This also works for a hollow stock with a door.
    private static bool InsideStock(Vector3 point, float radius, Vector3[] vertices, int[] triangles, Bounds bounds)
    {
        float reach = bounds.size.magnitude * 2f;
        foreach (Vector3 axis in new[] { Vector3.right, Vector3.left, Vector3.up,
            Vector3.down, Vector3.forward, Vector3.back })
        {
            Vector3 origin = point + axis * reach;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                float distance = RayTriangle(origin, -axis, vertices[triangles[i]],
                    vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                nearest = Mathf.Min(nearest, distance);
            }
            if (nearest >= reach - radius) return false;
        }
        for (int i = 0; i < triangles.Length; i += 3)
            if (PointTriangleDistance(point, vertices[triangles[i]], vertices[triangles[i + 1]],
                vertices[triangles[i + 2]]) < radius) return false;
        return true;
    }

    private static float RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a;
        Vector3 p = Vector3.Cross(direction, ac);
        float det = Vector3.Dot(ab, p);
        if (Mathf.Abs(det) < 1e-10f) return float.PositiveInfinity;
        Vector3 offset = origin - a;
        float u = Vector3.Dot(offset, p) / det;
        if (u < 0f || u > 1f) return float.PositiveInfinity;
        Vector3 q = Vector3.Cross(offset, ab);
        float v = Vector3.Dot(direction, q) / det;
        if (v < 0f || u + v > 1f) return float.PositiveInfinity;
        float distance = Vector3.Dot(ac, q) / det;
        return distance >= 0f ? distance : float.PositiveInfinity;
    }

    private static float PointTriangleDistance(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = point - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return ap.magnitude;
        Vector3 bp = point - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return bp.magnitude;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            return Vector3.Distance(point, a + ab * (d1 / (d1 - d3)));
        Vector3 cp = point - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return cp.magnitude;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            return Vector3.Distance(point, a + ac * (d2 / (d2 - d6)));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            return Vector3.Distance(point, b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6))));
        Vector3 normal = Vector3.Cross(ab, ac);
        return normal.sqrMagnitude > 1e-20f ? Mathf.Abs(Vector3.Dot(ap, normal.normalized)) : ap.magnitude;
    }

    private bool IsRaining => weatherManager != null &&
        (weatherManager.currentWeather == WeatherManager.WeatherType.Rainy ||
         weatherManager.currentWeather == WeatherManager.WeatherType.Stormy);

    private void SetSheltered(bool value)
    {
        if (sheltered == value) return;
        sheltered = value;
        for (int i = 0; i < beeAnimators.Length; i++)
        {
            if (beeAnimators[i] == null) continue;
            if (value) animationSpeeds[i] = beeAnimators[i].speed;
            beeAnimators[i].speed = value ? 0f : animationSpeeds[i];
        }
        for (int i = 0; i < beeAudio.Length; i++)
        {
            if (beeAudio[i] == null) continue;
            if (value)
            {
                audioStates[i] = beeAudio[i].isPlaying;
                beeAudio[i].Pause();
            }
            else if (audioStates[i]) beeAudio[i].UnPause();
        }
    }

    private void FlyTowards(Vector3 destination, float flightSpeed)
    {
        Vector3 heading = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
        if (heading.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(heading, Vector3.up), turnSpeed * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, destination, flightSpeed * Time.deltaTime);
    }

    private Vector3 HalfSize => flightArea.size * (0.5f - edgeMargin);

    private Vector3 ClampToArea(Vector3 position)
    {
        Vector3 p = flightArea.transform.InverseTransformPoint(position) - flightArea.center;
        Vector3 h = HalfSize;
        p.x = Mathf.Clamp(p.x, -h.x, h.x);
        p.y = Mathf.Clamp(p.y, -h.y, h.y);
        p.z = Mathf.Clamp(p.z, -h.z, h.z);
        return flightArea.transform.TransformPoint(flightArea.center + p);
    }

    private void PickTarget()
    {
        Vector3 h = HalfSize;
        Vector3 local = flightArea.center + new Vector3(
            Random.Range(-h.x, h.x), Random.Range(-h.y, h.y), Random.Range(-h.z, h.z));
        target = flightArea.transform.TransformPoint(local);
        arrived = false;
    }

    private void Update()
    {
        if (flightArea == null) return;
        if (!IsRaining) shelterSearchTimer = 0f;
        else shelterSearchTimer += Time.deltaTime;
        if (IsRaining && hiveEntrance != null && shelterSearchTimer >= shelterSearchSeconds)
        {
            returning = true;
            if (!hasRestPosition)
            {
                // Invalid geometry/size is reported, but never resume fair-weather roaming.
                FlyToShelterPoint(EntrancePosition);
                return;
            }
            if (!sheltered)
            {
                Vector3 destination = reachedEntrance ?
                    (hiveRestPoint != null ? hiveRestPoint.position : hive.TransformPoint(localRestPosition)) : EntrancePosition;
                if (FlyToShelterPoint(destination))
                {
                    if (!reachedEntrance) reachedEntrance = true;
                    else SetSheltered(true);
                }
            }
            else transform.position = RestPosition;
            return;
        }
        SetSheltered(false);
        if (returning)
        {
            if (reachedEntrance)
            {
                if (!FlyToShelterPoint(EntrancePosition)) return;
                reachedEntrance = false;
            }
            // Fly back into the area without snapping from the hive to its boundary.
            Vector3 entry = ClampToArea(transform.position);
            FlyTowards(entry, speed);
            if ((entry - transform.position).sqrMagnitude > 0.000001f) return;
            returning = false;
            PickTarget();
        }
        transform.position = ClampToArea(transform.position);
        target = ClampToArea(target);
        if (arrived)
        {
            pause -= Time.deltaTime;
            if (pause > 0f) return;
            PickTarget();
        }
        Vector3 direction = target - transform.position;
        // Keep the bee upright while it changes altitude.
        Vector3 heading = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (heading.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(heading, Vector3.up), turnSpeed * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((target - transform.position).sqrMagnitude < 0.000001f)
        {
            arrived = true;
            pause = Random.Range(minimumPause, Mathf.Max(minimumPause, maximumPause));
        }
    }
}
