using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour {
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    private CharacterController characterController;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private float verticalVelocity;
    private float moveInputX;
    private float moveInputZ;
    private bool jumpRequested;

    private void Awake() {
        characterController = GetComponent<CharacterController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn() {
        if (!IsOwner) {
            return;
        }

        CameraController cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController != null) {
            cameraController.SetTarget(transform);
        }
    }

    private void Update() {
        if (IsOwner && IsClient) {
            SendInputToServer();
        }

        if (!IsServer) {
            return;
        }

        MovePlayer();
        jumpRequested = false;
    }

    private void SendInputToServer() {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        bool jumpPressed = Input.GetButtonDown("Jump");

        SubmitMovementServerRpc(horizontal, vertical, jumpPressed);
    }

    [ServerRpc]
    private void SubmitMovementServerRpc(float horizontal, float vertical, bool jumpPressed) {
        moveInputX = Mathf.Clamp(horizontal, -1f, 1f);
        moveInputZ = Mathf.Clamp(vertical, -1f, 1f);
        jumpRequested |= jumpPressed;
    }

    private void MovePlayer() {
        if (characterController.isGrounded && verticalVelocity < 0f) {
            verticalVelocity = -2f;
        }

        Vector3 moveDirection = new Vector3(moveInputX, 0f, moveInputZ).normalized;
        bool isMoving = moveDirection.sqrMagnitude > 0f;

        if (isMoving && Mathf.Abs(moveInputX) > 0f) {
            spriteRenderer.flipX = moveInputX < 0f;
        }

        if (animator != null) {
            animator.SetBool("isMoving", isMoving);
        }

        if (jumpRequested && characterController.isGrounded) {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }
}
