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
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float respawnDelaySeconds = 2f;

    [Header("Combat")]
    [SerializeField] private CombatController combatController;
    [SerializeField] private AttackData currentAttackData;
    [SerializeField] private AttackData unarmedAttackData;


    [Header("Debug Health Display")]
    [SerializeField] private Vector3 healthLabelOffset = new Vector3(0f, 2.2f, 0f);

    private CharacterController characterController;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private GameMenu gameMenu;
    private UI_BagManager bagManager;
    private readonly NetworkVariable<float> currentHealth = new NetworkVariable<float>(
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

    public bool IsAlive => currentHealth.Value > 0;

    private void Awake() {
        characterController = GetComponent<CharacterController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        inputActions = new GameInput();
        KeyBinding.LoadBindingOverrides(inputActions);

        // 1 2 3 testing
        gameMenu = FindFirstObjectByType<GameMenu>();
        inputActions.Player.Item.performed += OnItemPerformed;

        if (combatController == null) {
            combatController = GetComponent<CombatController>();
        }
        bagManager = FindFirstObjectByType<UI_BagManager>();
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
            if (currentAttackData == null) {
                Debug.LogWarning("[PlayerController] currentAttackData 未指定。", this);
                return;
            }
            
            if(combatController.IsCombating.Value){
                TryAttackServerRpc();
            }
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

        nextServerAttackTime = Time.time + currentAttackData.cooldown;

        Vector3 attackCenter = transform.position + currentAttackData.centerOffset;
        Collider[] hits = Physics.OverlapSphere(
            attackCenter,
            currentAttackData.radius,
            currentAttackData.targetLayer,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits) {
            if (!IsInsideAttackRing(hit, attackCenter)) {
                continue;
            }

            PlayerController target = hit.GetComponentInParent<PlayerController>();
            if (target != null) {
                if (target == this || !target.IsSpawned) {
                    continue;
                }

                target.TakeDamage(currentAttackData.damage, OwnerClientId);
                Debug.Log($"[PlayerController] Client {OwnerClientId} hit client {target.OwnerClientId} for {currentAttackData.damage}.", this);
                return;
            }

            EnemyHealthDrop enemy = hit.GetComponentInParent<EnemyHealthDrop>();
            if (enemy == null || !enemy.IsAlive) {
                continue;
            }

            enemy.TakeDamage(currentAttackData.damage, OwnerClientId);
            Debug.Log($"[PlayerController] Client {OwnerClientId} hit enemy {enemy.name} for {currentAttackData.damage}.", this);
            return;
        }
    }

    // The inner safe radius is the character's width (diameter), matching CombatController's unarmed ring.
    private bool IsInsideAttackRing(Collider hit, Vector3 attackCenter) {
        float innerRadius = characterController.radius * 2f;
        Vector3 horizontalOffset = hit.ClosestPoint(attackCenter) - attackCenter;
        horizontalOffset.y = 0f;
        float distanceSquared = horizontalOffset.sqrMagnitude;
        return distanceSquared >= innerRadius * innerRadius && distanceSquared <= currentAttackData.radius * currentAttackData.radius;
    }

    private void MovePlayer() {
        if (currentHealth.Value <= 0) {
            return;
        }

        if (characterController.isGrounded && verticalVelocity < 0f) {
            verticalVelocity = -2f;
        }

        //Vector3 moveDirection = new Vector3(moveInputX, 0f, moveInputZ).normalized;
        Vector3 moveDirection = Vector3.zero;
        if (characterController.isGrounded) {
            //Vector3的3個參數表示xyz軸，0f鎖死y軸的變化，normalized避免斜著走速度比較快
            moveDirection = new Vector3(moveInputX, 0f, moveInputZ).normalized;
        }

        //moveDirection = new Vector3(moveInputX, 0f, moveInputZ).normalized;
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

    private void TakeDamage(float amount, ulong attackerClientId) {
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

    private void OnHealthChanged(float previousValue, float newValue) {
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

    // 選取物品欄
    private void OnItemPerformed(InputAction.CallbackContext context) {
    Debug.Log($"[Item] performed={context.performed}, control={context.control?.path}, action={context.action?.name}", this);

    if (!context.performed || !IsOwner || !IsClient || !GameState.isPlaying || PauseMenu.isOpen) {
        return;
    }

    int slotIndex = GetHotbarIndex(context);
    Debug.Log($"[Item] resolved slotIndex={slotIndex}", this);

    if (slotIndex < 0) return;

    gameMenu?.SetSelectedIndex(slotIndex);    
    
    HandleSelectedIndexChange(slotIndex);
}

    private int GetHotbarIndex(InputAction.CallbackContext context) {
        var action = context.action;
        if (action == null || context.control == null) return -1;

        // Unity 官方 API：找出是哪個 binding 觸發
        int bindingIndex = action.GetBindingIndexForControl(context.control);
        if (bindingIndex < 0) return -1;

        // 只計算「非 composite」的離散 binding，壓縮成 0..N-1
        int slot = 0;
        for (int i = 0; i < action.bindings.Count; i++) {
            var b = action.bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;

            if (i == bindingIndex) return slot;
            slot++;
        }
        return -1;
    }

    private void HandleSelectedIndexChange(int slotIndex) {
    if (!IsOwner || !IsClient || !GameState.isPlaying || PauseMenu.isOpen) return;
    if (bagManager == null) return;
    if (slotIndex < 0 || slotIndex >= bagManager.bagDataList.Count) return;

    BagSlotData slot = bagManager.bagDataList[slotIndex];
    if (slot == null || slot.IsEmpty || slot.itemData.itemObject == null) {
        combatController.SetAttackData(unarmedAttackData);
        return;
    }

    ItemObject item = slot.itemData.itemObject;
    if (!item.IsWeapon) {
        combatController.ExitCombat();
        return;
    }

    // 是武器 -> 切換 AttackData + 進入戰鬥
    AttackData weaponAttackData = item.attackData;
    currentAttackData = weaponAttackData;

    if (combatController != null) {
        combatController.SetAttackData(weaponAttackData);
        combatController.EntryCombat();
    }

    Debug.Log($"[Combat] Equipped weapon={item.itemName}, attackData={(weaponAttackData ? weaponAttackData.attackName : "NULL")}", this);
}
    private void OnDestroy() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
        // 1 2 3 testing
        inputActions.Player.Item.performed -= OnItemPerformed;
        inputActions?.Disable();
        inputActions?.Dispose();

    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + currentAttackData.centerOffset, currentAttackData.radius);
    }
}
