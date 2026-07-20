using Unity.Netcode;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour {
    private const string BindingOverridesPrefsKey = "InputBindingOverrides";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float respawnDelaySeconds = 2f;

    [Header("Combat")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRadius = 2f;
    [SerializeField] private float attackCooldownSeconds = 0.6f;
    [SerializeField] private Vector3 attackCenterOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private LayerMask attackLayerMask = ~0;

    [Header("Debug Health Display")]
    [SerializeField] private Vector3 healthLabelOffset = new Vector3(0f, 2.2f, 0f);

    private CharacterController characterController;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private readonly NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    private float verticalVelocity;
    private float moveInputX;
    private float moveInputZ;
    private bool jumpRequested;
    private float nextServerAttackTime;
    private Vector3 spawnPosition;
    private Coroutine respawnCoroutine;
    private GUIStyle healthLabelStyle;
    private GameInput inputActions;

    private void Awake() {
        characterController = GetComponent<CharacterController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        inputActions = new GameInput();
        LoadBindingOverrides();
    }

    public override void OnNetworkSpawn() {
        if (IsServer) {
            spawnPosition = transform.position;
            currentHealth.Value = maxHealth;
        }

        currentHealth.OnValueChanged += OnHealthChanged;

        if (!IsOwner) {
            return;
        }

        CameraController cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController != null) {
            cameraController.SetTarget(transform);
        }

        inputActions.Player.Enable();
    }

    public override void OnNetworkDespawn() {
        currentHealth.OnValueChanged -= OnHealthChanged;
        inputActions.Player.Disable();
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

    private void OnGUI() {
        Camera mainCamera = Camera.main;
        if (mainCamera == null || !IsSpawned) {
            return;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(transform.position + healthLabelOffset);
        if (screenPosition.z <= 0f) {
            return;
        }

        if (healthLabelStyle == null) {
            healthLabelStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }

        healthLabelStyle.normal.textColor = currentHealth.Value <= 0 ? Color.red : Color.green;

        const float width = 120f;
        const float height = 28f;
        Rect labelRect = new Rect(
            screenPosition.x - width * 0.5f,
            Screen.height - screenPosition.y - height * 0.5f,
            width,
            height
        );

        GUI.Label(labelRect, $"{currentHealth.Value}/{maxHealth}", healthLabelStyle);
    }

    private void SendInputToServer() {
        Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool attackPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        SubmitMovementServerRpc(moveInput.x, moveInput.y, jumpPressed);

        if (attackPressed) {
            TryAttackServerRpc();
        }
    }

    [ServerRpc]
    private void SubmitMovementServerRpc(float horizontal, float vertical, bool jumpPressed) {
        if (currentHealth.Value <= 0) {
            moveInputX = 0f;
            moveInputZ = 0f;
            jumpRequested = false;
            return;
        }

        moveInputX = Mathf.Clamp(horizontal, -1f, 1f);
        moveInputZ = Mathf.Clamp(vertical, -1f, 1f);
        jumpRequested |= jumpPressed;
    }

    [ServerRpc]
    private void TryAttackServerRpc() {
        if (currentHealth.Value <= 0 || Time.time < nextServerAttackTime) {
            return;
        }

        nextServerAttackTime = Time.time + attackCooldownSeconds;

        Vector3 attackCenter = transform.position + attackCenterOffset;
        Collider[] hits = Physics.OverlapSphere(
            attackCenter,
            attackRadius,
            attackLayerMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits) {
            PlayerController target = hit.GetComponentInParent<PlayerController>();
            if (target == null || target == this || !target.IsSpawned) {
                continue;
            }

            target.TakeDamage(attackDamage, OwnerClientId);
            Debug.Log($"[PlayerController] Client {OwnerClientId} hit client {target.OwnerClientId} for {attackDamage}.", this);
            return;
        }
    }

    private void MovePlayer() {
        if (currentHealth.Value <= 0) {
            return;
        }

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

    private void TakeDamage(int amount, ulong attackerClientId) {
        if (!IsServer || currentHealth.Value <= 0) {
            return;
        }

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);

        if (currentHealth.Value > 0) {
            return;
        }

        Debug.Log($"[PlayerController] Client {OwnerClientId} was defeated by client {attackerClientId}.", this);

        if (respawnCoroutine != null) {
            StopCoroutine(respawnCoroutine);
        }

        respawnCoroutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay() {
        yield return new WaitForSeconds(respawnDelaySeconds);

        characterController.enabled = false;
        transform.position = spawnPosition;
        verticalVelocity = 0f;
        moveInputX = 0f;
        moveInputZ = 0f;
        jumpRequested = false;
        characterController.enabled = true;

        currentHealth.Value = maxHealth;
        respawnCoroutine = null;
    }

    private void OnHealthChanged(int previousValue, int newValue) {
        Debug.Log($"[PlayerController] Client {OwnerClientId} health changed: {previousValue} -> {newValue}", this);
    }

    private void LoadBindingOverrides() {
        string overridesJson = PlayerPrefs.GetString(BindingOverridesPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(overridesJson)) {
            inputActions.asset.LoadBindingOverridesFromJson(overridesJson);
        }
    }

    private void OnDestroy() {
        inputActions?.Disable();
        inputActions?.Dispose();
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + attackCenterOffset, attackRadius);
    }
}
