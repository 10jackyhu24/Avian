using UnityEngine;
using Unity.Netcode; // for NGO

[RequireComponent(typeof(CharacterController))]

// Originally MonoBehaviour; switched to NetworkBehaviour to support Netcode for GameObjects (NGO)
public class PlayerController : NetworkBehaviour {
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float rotationSpeed = 12f;
    Animator animator;
    SpriteRenderer spriteRenderer;
    private CharacterController characterController;
    private float verticalVelocity;
    bool previousFlipX = false;
    private bool loggedFirstMovementInput;
    private float serverHorizontalInput;
    private float serverVerticalInput;
    private bool serverJumpRequested;

    private void Awake() {
        characterController = GetComponent<CharacterController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn() {
        Debug.Log($"[PlayerController] Spawned. OwnerClientId={OwnerClientId}, IsOwner={IsOwner}, IsLocalPlayer={IsLocalPlayer}", this);

        if (!IsOwner) {
            return;
        }

        CameraController cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController != null) {
            cameraController.SetTarget(transform);
            Debug.Log("[PlayerController] Local owner assigned camera target.", this);
        }
    }

    private void Update() {
        if (IsServer) {
            HandleMovement(serverHorizontalInput, serverVerticalInput, serverJumpRequested);
            serverJumpRequested = false;
        }

        if (IsOwner && IsClient) {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            bool jumpPressed = Input.GetButtonDown("Jump");

            if (!loggedFirstMovementInput && (Mathf.Abs(horizontal) > 0f || Mathf.Abs(vertical) > 0f || jumpPressed)) {
                loggedFirstMovementInput = true;
                Debug.Log($"[PlayerController] Sending movement input. h={horizontal}, v={vertical}, jump={jumpPressed}", this);
            }

            SubmitMovementServerRpc(horizontal, vertical, jumpPressed);
        }
    }

    [ServerRpc]
    private void SubmitMovementServerRpc(float horizontal, float vertical, bool jumpPressed) {
        serverHorizontalInput = Mathf.Clamp(horizontal, -1f, 1f);
        serverVerticalInput = Mathf.Clamp(vertical, -1f, 1f);
        serverJumpRequested |= jumpPressed;
    }

    private void HandleMovement(float horizontal, float vertical, bool jumpPressed) {
        if (characterController.isGrounded && verticalVelocity < 0f) {
            verticalVelocity = -2f;
        }

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (moveDirection.sqrMagnitude > 0f) {
            // Flip sprite based on horizontal input
            if (horizontal <= 0) {
                if (!previousFlipX) {
                    spriteRenderer.flipX = true;
                    previousFlipX = true;
                }
            }
            else {
                if (previousFlipX) {
                    spriteRenderer.flipX = false;
                    previousFlipX = false;
                }
            }
            animator.SetBool("isMoving", true);
        }
        else {
            animator.SetBool("isMoving", false);
        }

        // Jump input handling
        if (jumpPressed && characterController.isGrounded) {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        transform.rotation = Quaternion.identity;
        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }
}
