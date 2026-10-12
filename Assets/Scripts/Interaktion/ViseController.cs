using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Moves a vise jaw and screw together, rotating only the screw handle.</summary>
public class ViseController : MonoBehaviour
{
    public Transform movingAssembly;
    public Transform rotatingHandle;
    public Vector3 localTravelAxis = Vector3.right;
    public float closedOffset;
    public float openOffset = 0.2f;
    public float initialOffset;
    [Min(0.001f)] public float travelSpeed = 0.05f;
    [Min(0.001f)] public float travelPerTurn = 0.025f;
    [Min(0.1f)] public float interactionDistance = 3f;
    public Camera interactionCamera;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float offset;
    private float handleAngle;

    void Awake()
    {
        if (movingAssembly == null || rotatingHandle == null || localTravelAxis.sqrMagnitude < 0.0001f)
        {
            Debug.LogError("Schraubstock: Bewegliche Gruppe, Drehgriff und Bewegungsachse zuweisen.", this);
            enabled = false;
            return;
        }
        localTravelAxis.Normalize();
        offset = Mathf.Clamp(initialOffset, Mathf.Min(closedOffset, openOffset), Mathf.Max(closedOffset, openOffset));
        basePosition = movingAssembly.localPosition - localTravelAxis * initialOffset;
        baseRotation = rotatingHandle.localRotation;
    }

    void Update()
    {
        if (Time.timeScale == 0f || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
        var camera = interactionCamera != null ? interactionCamera : Camera.main;
        if (camera == null) return;
        var ray = new Ray(camera.transform.position, camera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            || hit.collider.GetComponentInParent<ViseController>() != this) return;

        bool close = Input.GetMouseButton(0);
        bool open = Input.GetMouseButton(1);
        if (close == open) return;
        Turn(close, Time.deltaTime);
    }

    public void Turn(bool close, float seconds)
    {
        if (movingAssembly == null || rotatingHandle == null || seconds <= 0f) return;
        float next = Mathf.MoveTowards(offset, close ? closedOffset : openOffset, travelSpeed * seconds);
        float delta = next - offset;
        if (Mathf.Abs(delta) < 0.000001f) return;
        offset = next;
        handleAngle = Mathf.Repeat(handleAngle + delta / travelPerTurn * 360f, 360f);
        movingAssembly.localPosition = basePosition + localTravelAxis * offset;

        // Convert the translation axis into the handle parent's coordinates.
        Vector3 worldAxis = movingAssembly.parent != null
            ? movingAssembly.parent.TransformDirection(localTravelAxis) : localTravelAxis;
        Vector3 rotationAxis = rotatingHandle.parent != null
            ? rotatingHandle.parent.InverseTransformDirection(worldAxis) : worldAxis;
        rotatingHandle.localRotation = Quaternion.AngleAxis(handleAngle, rotationAxis) * baseRotation;
    }
}
