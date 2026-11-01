using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class AIObjects
{

    //declare our variables
    public string AIGroupName { get { return m_aiGroupname; } }
    public GameObject  objectPrefab { get { return m_prefab; } }
    public int maxAI { get { return m_maxAI; } }
    public int spawnRate { get { return m_spawnRate; } }
    public int spawnAmount { get { return m_maxSpawnAmount; } } 
    public bool RandomizeStats { get { return m_RandomizeStats; } }
    public bool enableSpawner { get { return m_enableSpawner; } }
    
    //>>

    //serialize the private variables

    [Header("AI Group Stats")]
    [SerializeField]
    private string m_aiGroupname;
    [SerializeField]
    private GameObject m_prefab;
    [SerializeField]
    [Range(0f, 20f)]
    private int m_maxAI;
    [SerializeField]
    [Range(0f, 20f)]
    private int m_spawnRate;
    [SerializeField]
    [Range(0f, 10f)]
    private int m_maxSpawnAmount;
    [SerializeField]
    private bool m_randomizeStats;

    // new variables
    [Header("Main Settings")]
    [SerializeField]
    private bool m_enableSpawner;
    [SerializeField]
    private bool m_RandomizeStats;





    public AIObjects(string Name, GameObject Prefab, int MaxAI, int SpawnRate, int SpawnAmount, bool RandomizeStats)
    {
        this.m_aiGroupname = Name;
        this.m_prefab = Prefab;
        this.m_maxAI = MaxAI;
        this.m_spawnRate = SpawnRate;
        this.m_maxSpawnAmount = SpawnAmount;
        this.m_RandomizeStats = RandomizeStats;
    }


    public void setValues(int MaxAI, int SpawnRate, int SpawnAmount)
    {
        this.m_maxAI = MaxAI;
        this.m_spawnRate = SpawnRate;
        this.m_maxSpawnAmount = SpawnAmount;
    }


}

public class AISpawner : MonoBehaviour
{
    public List<Transform> Waypoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float m_SpawnTimer = 5f;
    [SerializeField] private Color m_SpawnColor = new Color(0f, 0.7f, 1f, 0.3f);
    [SerializeField] private Vector3 m_SpawnArea = new Vector3(4f, 1f, 4f);
    public AIObjects[] AIObject = new AIObjects[0];
    public float spawnTimer => m_SpawnTimer;
    public Vector3 spawnArea => m_SpawnArea;
    private readonly Dictionary<AIObjects, Transform> groups = new Dictionary<AIObjects, Transform>();

    void Start()
    {
        foreach (var group in AIObject)
        {
            if (group == null) continue;
            if (group.RandomizeStats) group.setValues(Random.Range(1, 20), Random.Range(1, 20), Random.Range(1, 10));
            var root = new GameObject(group.AIGroupName);
            root.transform.SetParent(transform, false);
            groups[group] = root.transform;
        }
        InvokeRepeating(nameof(SpawnNPC), 0.5f, Mathf.Max(0.1f, m_SpawnTimer));
    }

    public bool TryGetWaypoint(Transform previous, out Transform next)
    {
        next = null;
        int count = 0;
        foreach (var point in Waypoints)
        {
            if (point == null || point == previous) continue;
            if (Random.Range(0, ++count) == 0) next = point;
        }
        return next != null;
    }

    public Vector3 RandomWaypoint()
    {
        return TryGetWaypoint(null, out var point) ? point.position : transform.position;
    }

    public Vector3 RandomPosition() => RandomWaypoint();

    void SpawnNPC()
    {
        foreach (var pair in groups)
        {
            var group = pair.Key;
            if (!group.enableSpawner || group.objectPrefab == null || !TryGetWaypoint(null, out var point)) continue;
            int amount = Mathf.Min(Mathf.Max(0, group.maxAI - pair.Value.childCount), Random.Range(0, Mathf.Max(0, group.spawnAmount) + 1));
            for (int i = 0; i < amount; i++)
            {
                var fish = Instantiate(group.objectPrefab, point.position, Quaternion.identity, pair.Value);
                var move = fish.GetComponent<AIMove>();
                if (move == null) move = fish.AddComponent<AIMove>();
                move.manager = this;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = m_SpawnColor;
        foreach (var point in Waypoints)
        {
            if (point == null) continue;
            Gizmos.DrawSphere(point.position, 0.08f);
            foreach (var other in Waypoints)
                if (other != null && other != point) Gizmos.DrawLine(point.position, other.position);
        }
    }
}
