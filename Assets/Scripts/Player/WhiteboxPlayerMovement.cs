using UnityEngine;

public class WhiteboxPlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Mouse Look")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float mouseSensitivity = 2f;

    private CharacterController controller;
    private Vector3 velocity;

    private float verticalLookRotation = 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Maus im Game-Fenster sperren
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        Move();
        Look();
    }

    private void Move()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 move =
            transform.right * horizontal +
            transform.forward * vertical;

        move = Vector3.ClampMagnitude(move, 1f);

        controller.Move(move * moveSpeed * Time.deltaTime);

        // Gravity
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Links / Rechts:
        // Der ganze Player dreht sich.
        transform.Rotate(Vector3.up * mouseX);

        // Hoch / Runter:
        // Nur die Kamera dreht sich.
        verticalLookRotation -= mouseY;
        verticalLookRotation = Mathf.Clamp(
            verticalLookRotation,
            -85f,
            85f
        );

        playerCamera.localRotation =
            Quaternion.Euler(verticalLookRotation, 0f, 0f);
    }
}