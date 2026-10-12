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

    private Vector3 target;
    private float pause;
    private bool arrived;

    private void Start()
    {
        if (flightArea == null)
        {
            Debug.LogWarning("Assign a Flight Area to " + name, this);
            enabled = false;
            return;
        }
        transform.position = ClampToArea(transform.position);
        PickTarget();
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
