using Unity.Netcode;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour {
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float runSpeedMultiplier = 1.75f;
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

    private readonly NetworkVariable<bool> isMovingVisual = new NetworkVariable<bool>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> isFacingLeftVisual = new NetworkVariable<bool>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    private float verticalVelocity;
    private float moveInputX;
    private float moveInputZ;
    private bool runRequested;
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
        KeyBinding.LoadBindingOverrides(inputActions);
    }

    private void OnEnable() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
        OptionsMenu.BindingOverridesChanged += HandleBindingOverridesChanged;
    }

    private void OnDisable() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
    }

    public override void OnNetworkSpawn() {
        if (IsServer) {
            spawnPosition = transform.position;
            currentHealth.Value = maxHealth;
        }

        currentHealth.OnValueChanged += OnHealthChanged;
        isMovingVisual.OnValueChanged += OnIsMovingVisualChanged;
        isFacingLeftVisual.OnValueChanged += OnIsFacingLeftVisualChanged;
        ApplyVisualState(isMovingVisual.Value, isFacingLeftVisual.Value);

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
        isMovingVisual.OnValueChanged -= OnIsMovingVisualChanged;
        isFacingLeftVisual.OnValueChanged -= OnIsFacingLeftVisualChanged;
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
        bool runPressed = inputActions.Player.Run.IsPressed();
        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool attackPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        SubmitMovementServerRpc(moveInput.x, moveInput.y, runPressed, jumpPressed);

        if (attackPressed) {
            TryAttackServerRpc();
        }
    }

    [ServerRpc]
    private void SubmitMovementServerRpc(float horizontal, float vertical, bool runPressed, bool jumpPressed) {
        if (currentHealth.Value <= 0) {
            moveInputX = 0f;
            moveInputZ = 0f;
            runRequested = false;
            jumpRequested = false;
            return;
        }

        moveInputX = Mathf.Clamp(horizontal, -1f, 1f);
        moveInputZ = Mathf.Clamp(vertical, -1f, 1f);
        runRequested = runPressed;
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
            if (target != null) {
                if (target == this || !target.IsSpawned) {
                    continue;
                }

                target.TakeDamage(attackDamage, OwnerClientId);
                Debug.Log($"[PlayerController] Client {OwnerClientId} hit client {target.OwnerClientId} for {attackDamage}.", this);
                return;
            }

            EnemyHealthDrop enemy = hit.GetComponentInParent<EnemyHealthDrop>();
            if (enemy == null || !enemy.IsAlive) {
                continue;
            }

            enemy.TakeDamage(attackDamage, OwnerClientId);
            Debug.Log($"[PlayerController] Client {OwnerClientId} hit enemy {enemy.name} for {attackDamage}.", this);
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
        bool isRunning = isMoving && runRequested;

        isMovingVisual.Value = isMoving;
        if (isMoving && Mathf.Abs(moveInputX) > 0f) {
            isFacingLeftVisual.Value = moveInputX < 0f;
        }

        if (jumpRequested && characterController.isGrounded) {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        float currentMoveSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;
        Vector3 velocity = moveDirection * currentMoveSpeed;
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
        runRequested = false;
        jumpRequested = false;
        characterController.enabled = true;

        currentHealth.Value = maxHealth;
        respawnCoroutine = null;
    }

    private void OnHealthChanged(int previousValue, int newValue) {
        Debug.Log($"[PlayerController] Client {OwnerClientId} health changed: {previousValue} -> {newValue}", this);
    }

    private void OnIsMovingVisualChanged(bool previousValue, bool newValue) {
        ApplyVisualState(newValue, isFacingLeftVisual.Value);
    }

    private void OnIsFacingLeftVisualChanged(bool previousValue, bool newValue) {
        ApplyVisualState(isMovingVisual.Value, newValue);
    }

    private void ApplyVisualState(bool isMoving, bool isFacingLeft) {
        if (spriteRenderer != null) {
            spriteRenderer.flipX = isFacingLeft;
        }

        if (animator != null) {
            animator.SetBool("isMoving", isMoving);
        }
    }

    private void HandleBindingOverridesChanged() {
        KeyBinding.LoadBindingOverrides(inputActions);
    }

    private void OnDestroy() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
        inputActions?.Disable();
        inputActions?.Dispose();
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + attackCenterOffset, attackRadius);
    }
}
