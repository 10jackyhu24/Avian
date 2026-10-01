using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    [SerializeField] private AttackData unarmedAttackData;
    private AttackData currentAttackData;
    private FixedString64Bytes selectedItemId;
    private bool currentAttackUsesWeapon;

    [Header("Attack Range Preview")]
    [SerializeField] private Color attackRangeFillColor = new Color(1f, 0.25f, 0.05f, 0.2f);
    [SerializeField] private Color attackRangeOutlineColor = new Color(1f, 0.4f, 0.05f, 0.9f);
    [Range(8, 64)] [SerializeField] private int attackRangeSegments = 32;

    [Header("Shared Weapon Swing Visual")]
    [Min(0f)] [SerializeField] private float weaponGroundHeight = 0.08f;
    [Tooltip("Normalized handle position shared by every weapon sprite. (0, 0) is bottom-left.")]
    [SerializeField] private Vector2 weaponHandlePivot = new Vector2(0.15f, 0.15f);
    [SerializeField] private bool weaponFlipX = true;
    [Range(-180f, 180f)] [SerializeField] private float weaponPlanarAngle = 135f;
    [Min(0.05f)] [SerializeField] private float weaponHitHeight = 0.5f;
    [Range(1f, 15f)] [SerializeField] private float weaponSweepStepDegrees = 4f;


    [Header("Health Display")]
    [SerializeField] private Vector3 healthLabelOffset = new Vector3(0f, 1.5f, 0f);

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
    private float nextLocalAttackTime;
    private bool isPreparingAttack;
    private float attackMovementLockedUntil;
    private Vector3 preparedAttackDirection = Vector3.right;
    private GameObject attackRangePreview;
    private Mesh attackRangeMesh;
    private Material attackRangeFillMaterial;
    private Material attackRangeOutlineMaterial;
    private Coroutine weaponSwingCoroutine;
    private GameObject activeWeaponVisual;
    private readonly Collider[] weaponHitBuffer = new Collider[64];
    private Vector3 spawnPosition;
    private Coroutine respawnCoroutine;
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
        WorldHealthBarGUI.Remove(this);
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

        if (gameMenu == null) {
            gameMenu = FindFirstObjectByType<GameMenu>();
        }
        if (bagManager == null) {
            bagManager = FindFirstObjectByType<UI_BagManager>();
        }
        if (bagManager != null) {
            bagManager.InventoryDisplayChanged -= HandleInventoryDisplayChanged;
            bagManager.InventoryDisplayChanged += HandleInventoryDisplayChanged;
        }
        if (gameMenu != null) {
            gameMenu.OnSelectedIndexChanged -= HandleSelectedIndexChange;
            gameMenu.OnSelectedIndexChanged += HandleSelectedIndexChange;
        }

        CameraController cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController != null) {
            cameraController.SetTarget(transform);
        }

        inputActions.Player.Enable();

        if (gameMenu != null && gameMenu.SelectedIndex >= 0) {
            HandleSelectedIndexChange(gameMenu.SelectedIndex);
        }
    }

    public override void OnNetworkDespawn() {
        currentHealth.OnValueChanged -= OnHealthChanged;
        isMovingVisual.OnValueChanged -= OnIsMovingVisualChanged;
        isFacingLeftVisual.OnValueChanged -= OnIsFacingLeftVisualChanged;
        CancelPreparedAttack();
        WorldHealthBarGUI.Remove(this);
        if (gameMenu != null) {
            gameMenu.OnSelectedIndexChanged -= HandleSelectedIndexChange;
        }
        if (bagManager != null) {
            bagManager.InventoryDisplayChanged -= HandleInventoryDisplayChanged;
        }
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

    private void LateUpdate() {
        if (!IsSpawned) {
            WorldHealthBarGUI.Remove(this);
            return;
        }

        WorldHealthBarGUI.Draw(
            this,
            transform.position + healthLabelOffset,
            currentHealth.Value,
            maxHealth,
            new Color(0.15f, 0.8f, 0.25f),
            width: 140f,
            height: 22f
        );
    }

    private void SendInputToServer() {
        Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        bool runPressed = inputActions.Player.Run.IsPressed();
        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        InputAction attackAction = inputActions.Player.Attack;

        if (!GameState.isPlaying || PauseMenu.isOpen || BagMenu.IsOpen || !IsAlive) {
            SubmitMovementServerRpc(0f, 0f, false, false);
            CancelPreparedAttack();
            return;
        }

        if (attackAction.WasPressedThisFrame()) {
            BeginPreparingAttack();
        }

        if (isPreparingAttack) {
            UpdatePreparedAttackDirection();
        }

        if (attackAction.WasReleasedThisFrame()) {
            ReleasePreparedAttack();
        }

        bool isMovementLocked = Time.time < nextLocalAttackTime;
        SubmitMovementServerRpc(
            isMovementLocked ? 0f : moveInput.x,
            isMovementLocked ? 0f : moveInput.y,
            !isMovementLocked && runPressed,
            !isMovementLocked && jumpPressed
        );
    }

    private void BeginPreparingAttack() {
        if (currentAttackData == null || Time.time < nextLocalAttackTime) {
            return;
        }

        isPreparingAttack = true;
        preparedAttackDirection = isFacingLeftVisual.Value ? Vector3.left : Vector3.right;
        UpdatePreparedAttackDirection();
        ShowAttackRangePreview();

        if (!currentAttackUsesWeapon) {
            PlayPreparedUnarmedAttackServerRpc(preparedAttackDirection.x, preparedAttackDirection.z);
        }
    }

    private void ReleasePreparedAttack() {
        if (!isPreparingAttack) {
            return;
        }

        Vector3 attackDirection = preparedAttackDirection;
        isPreparingAttack = false;
        HideAttackRangePreview();

        if (currentAttackData == null) {
            return;
        }

        nextLocalAttackTime = Time.time + Mathf.Max(0f, currentAttackData.cooldown);
        TryAttackServerRpc(attackDirection.x, attackDirection.z);
    }

    private void CancelPreparedAttack() {
        if (!isPreparingAttack && attackRangePreview == null) {
            return;
        }

        isPreparingAttack = false;
        HideAttackRangePreview();
    }

    private void UpdatePreparedAttackDirection() {
        if (TryGetMouseAimDirection(out Vector3 direction)) {
            preparedAttackDirection = direction;
        }

        UpdateAttackRangePreviewTransform();
    }

    private bool TryGetMouseAimDirection(out Vector3 direction) {
        direction = Vector3.zero;
        Camera mainCamera = Camera.main;
        if (mainCamera == null || Mouse.current == null) {
            return false;
        }

        Vector3 attackOrigin = currentAttackData != null
            ? GetAttackVisualWorldPosition(currentAttackData)
            : transform.position;
        Ray mouseRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane playerGroundPlane = new Plane(Vector3.up, attackOrigin);
        if (!playerGroundPlane.Raycast(mouseRay, out float distance)) {
            return false;
        }

        direction = mouseRay.GetPoint(distance) - attackOrigin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) {
            return false;
        }

        direction.Normalize();
        return true;
    }

    private void UpdateAttackRangePreviewTransform() {
        if (attackRangePreview == null || currentAttackData == null) {
            return;
        }

        Vector3 attackDirection = preparedAttackDirection;
        attackDirection.y = 0f;
        if (attackDirection.sqrMagnitude < 0.0001f) {
            return;
        }

        attackDirection.Normalize();
        attackRangePreview.transform.position = GetAttackVisualWorldPosition(currentAttackData);

        float yaw = -Mathf.Atan2(attackDirection.z, attackDirection.x) * Mathf.Rad2Deg;
        attackRangePreview.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void ShowAttackRangePreview() {
        HideAttackRangePreview();
        if (currentAttackData == null) {
            return;
        }

        // This is a local world-space object. It has no NetworkObject and is only
        // created by the owning client, so other players never receive the preview.
        attackRangePreview = new GameObject("LocalAttackRangePreview") {
            hideFlags = HideFlags.DontSave
        };
        attackRangePreview.transform.localScale = Vector3.one;

        float outerRadius = Mathf.Max(0f, currentAttackData.radius);
        const float innerRadius = 0f;
        float arcAngle = currentAttackData.attackShape == AttackShape.Sector
            ? Mathf.Clamp(currentAttackData.sectorAngle, 1f, 360f)
            : 360f;
        const float centerAngle = 0f;
        int segmentCount = Mathf.Max(2, Mathf.CeilToInt(attackRangeSegments * arcAngle / 360f));

        Vector3[] vertices = new Vector3[(segmentCount + 1) * 2];
        int[] triangles = new int[segmentCount * 6];
        for (int i = 0; i <= segmentCount; i++) {
            float angle = centerAngle - arcAngle * 0.5f + arcAngle * i / segmentCount;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
            vertices[i * 2] = direction * innerRadius;
            vertices[i * 2 + 1] = direction * outerRadius;

            if (i == segmentCount) {
                continue;
            }

            int triangleIndex = i * 6;
            int vertexIndex = i * 2;
            // Wind triangles upward so the ground preview is visible from the gameplay camera.
            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 3;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        attackRangeMesh = new Mesh { name = "Attack Range Preview Mesh" };
        attackRangeMesh.vertices = vertices;
        attackRangeMesh.triangles = triangles;
        attackRangeMesh.RecalculateBounds();

        MeshFilter meshFilter = attackRangePreview.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = attackRangeMesh;
        MeshRenderer meshRenderer = attackRangePreview.AddComponent<MeshRenderer>();

        Shader previewShader = Shader.Find("Sprites/Default");
        attackRangeFillMaterial = new Material(previewShader) {
            color = attackRangeFillColor,
            renderQueue = 3000
        };
        attackRangeFillMaterial.mainTexture = Texture2D.whiteTexture;
        meshRenderer.sharedMaterial = attackRangeFillMaterial;

        GameObject outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(attackRangePreview.transform, false);
        LineRenderer outline = outlineObject.AddComponent<LineRenderer>();
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.widthMultiplier = 0.05f;
        outline.positionCount = (segmentCount + 1) * 2;
        outline.startColor = attackRangeOutlineColor;
        outline.endColor = attackRangeOutlineColor;
        outline.numCornerVertices = 2;

        attackRangeOutlineMaterial = new Material(previewShader) {
            color = Color.white,
            renderQueue = 3001
        };
        attackRangeOutlineMaterial.mainTexture = Texture2D.whiteTexture;
        outline.sharedMaterial = attackRangeOutlineMaterial;

        if (currentAttackData.attackShape == AttackShape.Radial) {
            // A radial preview is a filled disc with only its outer edge outlined.
            outline.positionCount = segmentCount;
            for (int i = 0; i < segmentCount; i++) {
                outline.SetPosition(i, vertices[i * 2 + 1] + Vector3.up * 0.01f);
            }
        } else {
            outline.positionCount = (segmentCount + 1) * 2;
            int outlineIndex = 0;
            for (int i = 0; i <= segmentCount; i++) {
                outline.SetPosition(outlineIndex++, vertices[i * 2 + 1] + Vector3.up * 0.01f);
            }
            for (int i = segmentCount; i >= 0; i--) {
                outline.SetPosition(outlineIndex++, vertices[i * 2] + Vector3.up * 0.01f);
            }
        }

        UpdateAttackRangePreviewTransform();
    }

    private void HideAttackRangePreview() {
        if (attackRangePreview != null) {
            Destroy(attackRangePreview);
            attackRangePreview = null;
        }

        if (attackRangeMesh != null) {
            Destroy(attackRangeMesh);
            attackRangeMesh = null;
        }

        if (attackRangeFillMaterial != null) {
            Destroy(attackRangeFillMaterial);
            attackRangeFillMaterial = null;
        }

        if (attackRangeOutlineMaterial != null) {
            Destroy(attackRangeOutlineMaterial);
            attackRangeOutlineMaterial = null;
        }
    }

    [ServerRpc]
    private void SubmitMovementServerRpc(float horizontal, float vertical, bool runPressed, bool jumpPressed) {
        if (currentHealth.Value <= 0 || IsAttackMovementLockedOnServer()) {
            StopMovementOnServer();
            return;
        }

        moveInputX = Mathf.Clamp(horizontal, -1f, 1f);
        moveInputZ = Mathf.Clamp(vertical, -1f, 1f);
        runRequested = runPressed;
        jumpRequested |= jumpPressed;
    }

    [ServerRpc]
    private void PlayPreparedUnarmedAttackServerRpc(float aimDirectionX, float aimDirectionZ) {
        if (currentHealth.Value <= 0 || currentAttackUsesWeapon || currentAttackData == null ||
            Time.time < nextServerAttackTime) {
            return;
        }

        Vector3 attackDirection = GetValidatedAttackDirection(aimDirectionX, aimDirectionZ);
        Vector3 effectPosition = GetUnarmedAttackEffectPosition(currentAttackData, attackDirection);
        PlayPreparedUnarmedAttackClientRpc(effectPosition, attackDirection.x < 0f);
    }

    [ClientRpc]
    private void PlayPreparedUnarmedAttackClientRpc(Vector3 effectPosition, bool facingLeft) {
        PlayUnarmedEffect(effectPosition, facingLeft);
    }

    [ServerRpc]
    private void TryAttackServerRpc(float aimDirectionX, float aimDirectionZ) {
        if (currentHealth.Value <= 0 || Time.time < nextServerAttackTime) {
            return;
        }

        if (currentAttackData == null) {
            Debug.LogWarning($"[Combat] Server rejected attack from client {OwnerClientId}: no supported weapon is equipped.", this);
            return;
        }

        AttackData attackData = currentAttackData;
        bool usesWeapon = currentAttackUsesWeapon;
        FixedString64Bytes attackItemId = selectedItemId;
        float attackDuration = Mathf.Max(0f, attackData.cooldown);
        nextServerAttackTime = Time.time + attackDuration;
        attackMovementLockedUntil = nextServerAttackTime;
        StopMovementOnServer();

        Vector3 attackDirection = GetValidatedAttackDirection(aimDirectionX, aimDirectionZ);
        PlayAttackVisualClientRpc(attackItemId, attackDirection, usesWeapon);

        if (usesWeapon) {
            if (ItemManager.TryGetWeaponById(
                    attackItemId.ToString(),
                    out ItemData weaponItem,
                    out AttackData weaponAttackData) &&
                weaponItem.itemObject != null &&
                weaponItem.itemObject.icon != null) {
                StartCoroutine(WeaponDamageSweepRoutine(
                    weaponItem.itemObject,
                    weaponAttackData,
                    attackDirection
                ));
            }
            else {
                Debug.LogWarning(
                    $"[Combat] Server could not start the weapon sweep for item '{attackItemId}'.",
                    this
                );
            }
            return;
        }

        ApplyAreaAttackDamage(attackData, attackDirection);
    }

    private void ApplyAreaAttackDamage(AttackData attackData, Vector3 attackDirection) {
        Vector3 attackCenter = transform.position + attackData.centerOffset;
        Collider[] hits = Physics.OverlapSphere(
            attackCenter,
            attackData.radius,
            attackData.targetLayer,
            QueryTriggerInteraction.Ignore
        );

        HashSet<PlayerController> playerTargets = new HashSet<PlayerController>();
        HashSet<EnemyHealthDrop> enemyTargets = new HashSet<EnemyHealthDrop>();
        int damagedTargetCount = 0;

        foreach (Collider hit in hits) {
            if (hit == null ||
                !IsInsideAttackArea(hit, attackCenter, attackDirection, attackData)) {
                continue;
            }

            damagedTargetCount += TryDamageTarget(
                hit,
                attackData,
                playerTargets,
                enemyTargets
            ) ? 1 : 0;
        }

        if (damagedTargetCount == 0) {
            Debug.Log(
                $"[Combat] Client {OwnerClientId} {attackData.attackName} attack missed. " +
                $"Center={attackCenter}, radius={attackData.radius}.",
                this
            );
        } else {
            Debug.Log(
                $"[Combat] Client {OwnerClientId} {attackData.attackName} hit {damagedTargetCount} target(s).",
                this
            );
        }
    }

    private bool TryDamageTarget(
        Collider hit,
        AttackData attackData,
        HashSet<PlayerController> damagedPlayers,
        HashSet<EnemyHealthDrop> damagedEnemies
    ) {
        PlayerController target = hit.GetComponentInParent<PlayerController>();
        if (target != null) {
            if (target == this || !target.IsSpawned || !target.IsAlive ||
                !damagedPlayers.Add(target)) {
                return false;
            }

            target.TakeDamage(attackData.damage, OwnerClientId);
            Debug.Log(
                $"[PlayerController] Client {OwnerClientId} hit client {target.OwnerClientId} for {attackData.damage}.",
                this
            );
            return true;
        }

        EnemyHealthDrop enemy = hit.GetComponentInParent<EnemyHealthDrop>();
        if (enemy == null || !enemy.IsAlive || !damagedEnemies.Add(enemy)) {
            return false;
        }

        string enemyName = enemy.name;
        enemy.TakeDamage(attackData.damage, OwnerClientId);
        Debug.Log(
            $"[PlayerController] Client {OwnerClientId} hit enemy {enemyName} for {attackData.damage}.",
            this
        );
        return true;
    }

    private Vector3 GetValidatedAttackDirection(float aimDirectionX, float aimDirectionZ) {
        Vector3 attackDirection = new Vector3(aimDirectionX, 0f, aimDirectionZ);
        if (float.IsNaN(attackDirection.x) || float.IsInfinity(attackDirection.x) ||
            float.IsNaN(attackDirection.z) || float.IsInfinity(attackDirection.z) ||
            attackDirection.sqrMagnitude < 0.0001f) {
            attackDirection = isFacingLeftVisual.Value ? Vector3.left : Vector3.right;
        } else {
            attackDirection.Normalize();
        }
        return attackDirection;
    }

    private Vector3 GetUnarmedAttackEffectPosition(
        AttackData attackData,
        Vector3 attackDirection
    ) {
        Vector3 previewCenter = GetAttackVisualWorldPosition(attackData);
        float forwardOffset = Mathf.Clamp(
            attackData.effectForwardOffset,
            0f,
            Mathf.Max(0f, attackData.radius)
        );
        return previewCenter + attackDirection * forwardOffset;
    }

    [ClientRpc]
    private void PlayAttackVisualClientRpc(
        FixedString64Bytes itemId,
        Vector3 attackDirection,
        bool usesWeapon
    ) {
        // Unarmed visuals start on button press. The release RPC only executes
        // damage and weapon swing visuals, preventing a second Punch animation.
        if (!usesWeapon) {
            return;
        }

        if (ItemManager.TryGetWeaponById(itemId.ToString(), out ItemData weaponItem, out AttackData attackData)) {
            StartWeaponSwingVisual(weaponItem.itemObject, attackData, attackDirection);
        }
    }

    private void PlayUnarmedEffect(Vector3 position, bool facingLeft) {
        // Punch is deliberately restricted to attacks whose selected item is not a weapon.
        if (unarmedAttackData == null || unarmedAttackData.attackEffect == null) {
            return;
        }

        GameObject effect = Instantiate(unarmedAttackData.attackEffect, position, Quaternion.identity);
        effect.transform.localScale *= Mathf.Max(0.01f, unarmedAttackData.effectScale);

        foreach (SpriteRenderer effectRenderer in effect.GetComponentsInChildren<SpriteRenderer>()) {
            effectRenderer.flipX = facingLeft;
        }

        Destroy(effect, Mathf.Max(0.01f, unarmedAttackData.effectLifetime));
    }

    private void StartWeaponSwingVisual(ItemObject weaponItem, AttackData attackData, Vector3 attackDirection) {
        if (weaponItem == null || weaponItem.icon == null || attackData == null) {
            return;
        }

        if (weaponSwingCoroutine != null) {
            StopCoroutine(weaponSwingCoroutine);
        }
        if (activeWeaponVisual != null) {
            Destroy(activeWeaponVisual);
        }

        weaponSwingCoroutine = StartCoroutine(WeaponSwingRoutine(weaponItem, attackData, attackDirection));
    }

    private IEnumerator WeaponSwingRoutine(ItemObject weaponItem, AttackData attackData, Vector3 attackDirection) {
        activeWeaponVisual = new GameObject($"{weaponItem.itemName}_AttackVisual");
        activeWeaponVisual.transform.SetParent(transform, false);
        Vector3 playerScale = transform.lossyScale;
        activeWeaponVisual.transform.localScale = new Vector3(
            GetSafeInverseScale(playerScale.x),
            GetSafeInverseScale(playerScale.y),
            GetSafeInverseScale(playerScale.z)
        );
        activeWeaponVisual.transform.position = GetAttackVisualWorldPosition(attackData);

        GameObject weaponObject = new GameObject("Weapon");
        weaponObject.transform.SetParent(activeWeaponVisual.transform, false);

        SpriteRenderer weaponRenderer = weaponObject.AddComponent<SpriteRenderer>();
        weaponRenderer.sprite = weaponItem.icon;
        weaponRenderer.flipX = weaponFlipX;
        weaponRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        weaponRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 10;

        GetWeaponGeometry(
            weaponItem,
            attackData,
            out _,
            out _,
            out Quaternion skyFacingRotation,
            out Vector3 handlePoint,
            out float scale
        );
        weaponObject.transform.localRotation = skyFacingRotation;
        weaponObject.transform.localScale = Vector3.one * scale;
        weaponObject.transform.localPosition = -(skyFacingRotation * (handlePoint * scale));

        float duration = Mathf.Max(0.01f, attackData.cooldown);
        float startedAt = Time.time;

        while (activeWeaponVisual != null) {
            float normalizedTime = Mathf.Clamp01((Time.time - startedAt) / duration);
            float angle = GetWeaponSwingAngle(attackData, attackDirection, normalizedTime);

            // Keep the pivot at the attack center in world space so player scale
            // cannot change either the weapon length or its swing radius.
            activeWeaponVisual.transform.position = GetAttackVisualWorldPosition(attackData);
            activeWeaponVisual.transform.rotation = Quaternion.Euler(0f, angle, 0f);

            if (normalizedTime >= 1f) {
                break;
            }
            yield return null;
        }

        if (activeWeaponVisual != null) {
            Destroy(activeWeaponVisual);
            activeWeaponVisual = null;
        }
        weaponSwingCoroutine = null;
    }

    private IEnumerator WeaponDamageSweepRoutine(
        ItemObject weaponItem,
        AttackData attackData,
        Vector3 attackDirection
    ) {
        GetWeaponGeometry(
            weaponItem,
            attackData,
            out Vector2[] weaponVertices,
            out ushort[] weaponTriangles,
            out Quaternion skyFacingRotation,
            out Vector3 handlePoint,
            out float scale
        );
        Vector2[] worldWeaponVertices = new Vector2[weaponVertices.Length];

        HashSet<PlayerController> damagedPlayers = new HashSet<PlayerController>();
        HashSet<EnemyHealthDrop> damagedEnemies = new HashSet<EnemyHealthDrop>();
        float duration = Mathf.Max(0.01f, attackData.cooldown);
        float startedAt = Time.time;
        float previousAngle = GetWeaponSwingAngle(attackData, attackDirection, 0f);
        int damagedTargetCount = DamageTargetsAtWeaponAngle(
            weaponVertices,
            weaponTriangles,
            worldWeaponVertices,
            skyFacingRotation,
            handlePoint,
            scale,
            attackData,
            previousAngle,
            damagedPlayers,
            damagedEnemies
        );

        while (IsServer && currentHealth.Value > 0f) {
            yield return null;

            float normalizedTime = Mathf.Clamp01((Time.time - startedAt) / duration);
            float currentAngle = GetWeaponSwingAngle(attackData, attackDirection, normalizedTime);
            int sweepSteps = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    Mathf.Abs(currentAngle - previousAngle) /
                    Mathf.Max(1f, weaponSweepStepDegrees)
                )
            );

            for (int step = 1; step <= sweepSteps; step++) {
                float angle = Mathf.Lerp(previousAngle, currentAngle, step / (float)sweepSteps);
                damagedTargetCount += DamageTargetsAtWeaponAngle(
                    weaponVertices,
                    weaponTriangles,
                    worldWeaponVertices,
                    skyFacingRotation,
                    handlePoint,
                    scale,
                    attackData,
                    angle,
                    damagedPlayers,
                    damagedEnemies
                );
            }

            previousAngle = currentAngle;
            if (normalizedTime >= 1f) {
                break;
            }
        }

        if (damagedTargetCount == 0) {
            Debug.Log(
                $"[Combat] Client {OwnerClientId} {attackData.attackName} weapon sweep missed.",
                this
            );
        } else {
            Debug.Log(
                $"[Combat] Client {OwnerClientId} {attackData.attackName} weapon sweep hit " +
                $"{damagedTargetCount} target(s).",
                this
            );
        }
    }

    private int DamageTargetsAtWeaponAngle(
        Vector2[] weaponVertices,
        ushort[] weaponTriangles,
        Vector2[] worldWeaponVertices,
        Quaternion skyFacingRotation,
        Vector3 handlePoint,
        float scale,
        AttackData attackData,
        float angle,
        HashSet<PlayerController> damagedPlayers,
        HashSet<EnemyHealthDrop> damagedEnemies
    ) {
        Quaternion pivotRotation = Quaternion.Euler(0f, angle, 0f);
        Vector3 pivotPosition = GetAttackVisualWorldPosition(attackData);
        for (int i = 0; i < weaponVertices.Length; i++) {
            Vector2 vertex = weaponVertices[i];
            Vector3 localFromHandle = new Vector3(
                vertex.x - handlePoint.x,
                vertex.y - handlePoint.y,
                0f
            ) * scale;
            Vector3 worldVertex =
                pivotPosition + pivotRotation * (skyFacingRotation * localFromHandle);
            worldWeaponVertices[i] = new Vector2(worldVertex.x, worldVertex.z);
        }

        float halfHitHeight = Mathf.Max(0.025f, weaponHitHeight * 0.5f);
        Vector3 queryCenter = pivotPosition;
        Vector3 queryHalfExtents = new Vector3(
            attackData.radius,
            halfHitHeight,
            attackData.radius
        );

        int hitCount = Physics.OverlapBoxNonAlloc(
            queryCenter,
            queryHalfExtents,
            weaponHitBuffer,
            Quaternion.identity,
            attackData.targetLayer,
            QueryTriggerInteraction.Ignore
        );

        int damagedTargetCount = 0;
        for (int i = 0; i < hitCount; i++) {
            Collider hit = weaponHitBuffer[i];
            if (hit == null) {
                continue;
            }

            Bounds targetBounds = hit.bounds;
            if (targetBounds.max.y < pivotPosition.y - halfHitHeight ||
                targetBounds.min.y > pivotPosition.y + halfHitHeight ||
                !DoesWeaponMeshOverlapTarget(
                    worldWeaponVertices,
                    weaponTriangles,
                    targetBounds
                )) {
                continue;
            }

            damagedTargetCount += TryDamageTarget(
                hit,
                attackData,
                damagedPlayers,
                damagedEnemies
            ) ? 1 : 0;
        }

        return damagedTargetCount;
    }

    private static bool DoesWeaponMeshOverlapTarget(
        Vector2[] weaponVertices,
        ushort[] weaponTriangles,
        Bounds targetBounds
    ) {
        if (weaponVertices == null || weaponTriangles == null) {
            return false;
        }

        Vector2 targetCenter = new Vector2(targetBounds.center.x, targetBounds.center.z);
        Vector2 targetExtents = new Vector2(targetBounds.extents.x, targetBounds.extents.z);
        for (int i = 0; i + 2 < weaponTriangles.Length; i += 3) {
            int indexA = weaponTriangles[i];
            int indexB = weaponTriangles[i + 1];
            int indexC = weaponTriangles[i + 2];
            if (indexA >= weaponVertices.Length ||
                indexB >= weaponVertices.Length ||
                indexC >= weaponVertices.Length) {
                continue;
            }

            if (TriangleOverlapsRectangle(
                    weaponVertices[indexA] - targetCenter,
                    weaponVertices[indexB] - targetCenter,
                    weaponVertices[indexC] - targetCenter,
                    targetExtents)) {
                return true;
            }
        }

        return false;
    }

    private static bool TriangleOverlapsRectangle(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 rectangleExtents
    ) {
        float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
        float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        if (minX > rectangleExtents.x || maxX < -rectangleExtents.x) {
            return false;
        }

        float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
        float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
        if (minY > rectangleExtents.y || maxY < -rectangleExtents.y) {
            return false;
        }

        return !IsSeparatedByTriangleEdge(a, b, c, b - a, rectangleExtents) &&
               !IsSeparatedByTriangleEdge(a, b, c, c - b, rectangleExtents) &&
               !IsSeparatedByTriangleEdge(a, b, c, a - c, rectangleExtents);
    }

    private static bool IsSeparatedByTriangleEdge(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 edge,
        Vector2 rectangleExtents
    ) {
        Vector2 axis = new Vector2(-edge.y, edge.x);
        if (axis.sqrMagnitude < 0.000001f) {
            return false;
        }

        float projectionA = Vector2.Dot(a, axis);
        float projectionB = Vector2.Dot(b, axis);
        float projectionC = Vector2.Dot(c, axis);
        float triangleMin = Mathf.Min(projectionA, Mathf.Min(projectionB, projectionC));
        float triangleMax = Mathf.Max(projectionA, Mathf.Max(projectionB, projectionC));
        float rectangleRadius =
            rectangleExtents.x * Mathf.Abs(axis.x) +
            rectangleExtents.y * Mathf.Abs(axis.y);

        return triangleMin > rectangleRadius || triangleMax < -rectangleRadius;
    }

    private void GetWeaponGeometry(
        ItemObject weaponItem,
        AttackData attackData,
        out Vector2[] renderedVertices,
        out ushort[] triangles,
        out Quaternion skyFacingRotation,
        out Vector3 handlePoint,
        out float scale
    ) {
        skyFacingRotation =
            Quaternion.AngleAxis(-90f, Vector3.right) *
            Quaternion.AngleAxis(weaponPlanarAngle, Vector3.forward);

        // Every weapon texture uses the same handle-to-blade layout, so the visual
        // and the server hit box can share exactly the same geometry.
        Bounds spriteBounds = weaponItem.icon.bounds;
        Vector2 renderedHandlePivot = new Vector2(
            weaponFlipX
                ? 1f - Mathf.Clamp01(weaponHandlePivot.x)
                : Mathf.Clamp01(weaponHandlePivot.x),
            Mathf.Clamp01(weaponHandlePivot.y)
        );
        handlePoint = new Vector3(
            Mathf.Lerp(spriteBounds.min.x, spriteBounds.max.x, renderedHandlePivot.x),
            Mathf.Lerp(spriteBounds.min.y, spriteBounds.max.y, renderedHandlePivot.y),
            0f
        );

        Vector2[] spriteVertices = weaponItem.icon.vertices;
        renderedVertices = new Vector2[spriteVertices.Length];
        float spriteLength = 0f;
        for (int i = 0; i < spriteVertices.Length; i++) {
            Vector2 vertex = spriteVertices[i];
            if (weaponFlipX) {
                vertex.x = spriteBounds.center.x * 2f - vertex.x;
            }

            renderedVertices[i] = vertex;
            spriteLength = Mathf.Max(
                spriteLength,
                Vector2.Distance(vertex, new Vector2(handlePoint.x, handlePoint.y))
            );
        }

        triangles = weaponItem.icon.triangles;
        scale = Mathf.Max(0.1f, attackData.radius) / Mathf.Max(0.01f, spriteLength);
    }

    private static float GetWeaponSwingAngle(
        AttackData attackData,
        Vector3 attackDirection,
        float normalizedTime
    ) {
        float centerAngle = Mathf.Atan2(attackDirection.x, attackDirection.z) * Mathf.Rad2Deg;
        float halfSwingAngle = Mathf.Clamp(attackData.sectorAngle, 1f, 360f) * 0.5f;
        float easedTime = Mathf.Clamp01(normalizedTime);
        easedTime = easedTime * easedTime * (3f - 2f * easedTime);
        return Mathf.Lerp(
            centerAngle - halfSwingAngle,
            centerAngle + halfSwingAngle,
            easedTime
        );
    }

    private Vector3 GetAttackVisualWorldPosition(AttackData attackData) {
        Vector3 attackCenter = transform.position + attackData.centerOffset;
        float characterFeetY = spriteRenderer != null && spriteRenderer.sprite != null
            ? spriteRenderer.bounds.min.y
            : characterController != null
                ? characterController.bounds.min.y
                : transform.position.y;

        return new Vector3(
            attackCenter.x,
            characterFeetY + weaponGroundHeight,
            attackCenter.z
        );
    }

    private static float GetSafeInverseScale(float scale) {
        return Mathf.Abs(scale) > 0.0001f ? 1f / scale : 1f;
    }

    private bool IsInsideAttackArea(
        Collider hit,
        Vector3 attackCenter,
        Vector3 attackDirection,
        AttackData attackData
    ) {
        Vector3 horizontalOffset = hit.ClosestPoint(attackCenter) - attackCenter;
        horizontalOffset.y = 0f;
        float distanceSquared = horizontalOffset.sqrMagnitude;
        if (distanceSquared > attackData.radius * attackData.radius) {
            return false;
        }

        if (attackData.attackShape != AttackShape.Sector) {
            return true;
        }

        return Vector3.Angle(attackDirection, horizontalOffset) <= attackData.sectorAngle * 0.5f;
    }

    private void MovePlayer() {
        if (currentHealth.Value <= 0) {
            return;
        }

        if (IsAttackMovementLockedOnServer()) {
            StopMovementOnServer();
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

    private bool IsAttackMovementLockedOnServer() {
        return Time.time < attackMovementLockedUntil;
    }

    private void StopMovementOnServer() {
        moveInputX = 0f;
        moveInputZ = 0f;
        runRequested = false;
        jumpRequested = false;

        if (IsServer) {
            isMovingVisual.Value = false;
        }
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

    private void HandleInventoryDisplayChanged() {
        if (!IsOwner || !IsClient || gameMenu == null || gameMenu.SelectedIndex < 0) {
            return;
        }

        HandleSelectedIndexChange(gameMenu.SelectedIndex);
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

    if (gameMenu != null) {
        gameMenu.SetSelectedIndex(slotIndex);
    } else {
        HandleSelectedIndexChange(slotIndex);
    }
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
        // Selection state must also initialize while the scene or inventory is still
        // loading. Input is gated elsewhere, so syncing it here is safe while paused.
        if (!IsOwner || !IsClient) return;
        if (bagManager == null) {
            bagManager = FindFirstObjectByType<UI_BagManager>();
        }
        if (bagManager == null || slotIndex < 0) return;

        CancelPreparedAttack();

        FixedString64Bytes newSelectedItemId = default;
        AttackData localAttackData = null;
        bool localUsesWeapon = false;
        string selectedItemName = "Empty";

        BagSlotData slot = slotIndex < bagManager.bagDataList.Count
            ? bagManager.bagDataList[slotIndex]
            : null;
        if (slot != null && !slot.IsEmpty && slot.itemData.itemObject != null) {
            ItemObject item = slot.itemData.itemObject;
            selectedItemName = item.itemName;

            if (!string.IsNullOrWhiteSpace(item.itemId)) {
                newSelectedItemId = new FixedString64Bytes(item.itemId);
            }

            if (item.IsWeapon && ItemManager.TryGetWeaponById(item.itemId, out _, out AttackData attackData)) {
                localAttackData = attackData;
                localUsesWeapon = true;
            }
            else if (!item.IsWeapon) {
                localAttackData = unarmedAttackData;
            }
        }

        selectedItemId = newSelectedItemId;
        currentAttackData = localAttackData;
        currentAttackUsesWeapon = localUsesWeapon;
        combatController?.SetAttackData(localAttackData);
        SetSelectedAttackServerRpc(newSelectedItemId);

        if (localAttackData != null) {
            string attackType = localUsesWeapon ? "weapon" : "unarmed";
            Debug.Log($"[Combat] Requested {selectedItemName} from slot {slotIndex + 1} ({attackType} attack).", this);
        }
        else {
            Debug.Log($"[Combat] Slot {slotIndex + 1} ({selectedItemName}) has no usable attack data.", this);
        }
    }

    [ServerRpc]
    private void SetSelectedAttackServerRpc(FixedString64Bytes newSelectedItemId) {
        string itemId = newSelectedItemId.ToString();
        if (ItemManager.TryGetItemById(itemId, out ItemData selectedItem) && selectedItem.itemObject != null) {
            if (selectedItem.IsWeapon && selectedItem.itemObject.attackData != null) {
                selectedItemId = newSelectedItemId;
                currentAttackData = selectedItem.itemObject.attackData;
                currentAttackUsesWeapon = true;
                combatController?.SetAttackData(currentAttackData);
                combatController?.EntryCombat();
                Debug.Log($"[Combat] Client {OwnerClientId} equipped {selectedItem.itemObject.itemName} with attackData={currentAttackData.attackName}.", this);
                return;
            }

            if (!selectedItem.IsWeapon && unarmedAttackData != null) {
                selectedItemId = newSelectedItemId;
                currentAttackData = unarmedAttackData;
                currentAttackUsesWeapon = false;
                combatController?.SetAttackData(unarmedAttackData);
                combatController?.EntryCombat();
                Debug.Log($"[Combat] Client {OwnerClientId} selected non-weapon {selectedItem.itemObject.itemName}; using unarmed attack.", this);
                return;
            }
        }

        selectedItemId = default;
        currentAttackData = null;
        currentAttackUsesWeapon = false;
        combatController?.SetAttackData(null);
        combatController?.ExitCombat();

        Debug.Log($"[Combat] Client {OwnerClientId} cleared its selected attack.", this);
    }

    public void UnequipDroppedWeaponOnServer(string droppedItemId) {
        if (!IsServer || currentAttackData == null ||
            !string.Equals(droppedItemId, "axe", System.StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        ClearEquippedWeapon();
        ClearEquippedWeaponClientRpc();
        Debug.Log($"[Combat] Client {OwnerClientId} dropped its equipped Axe; unequipping.", this);
    }

    [ClientRpc]
    private void ClearEquippedWeaponClientRpc() {
        if (IsOwner) {
            ClearEquippedWeapon();
        }
    }

    private void ClearEquippedWeapon() {
        currentAttackData = null;
        combatController?.SetAttackData(null);
        combatController?.ExitCombat();
    }

    private void OnDestroy() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
        if (gameMenu != null) {
            gameMenu.OnSelectedIndexChanged -= HandleSelectedIndexChange;
        }
        if (bagManager != null) {
            bagManager.InventoryDisplayChanged -= HandleInventoryDisplayChanged;
        }
        // 1 2 3 testing
        if (inputActions != null) {
            inputActions.Player.Item.performed -= OnItemPerformed;
            inputActions.Disable();
            inputActions.Dispose();
        }
        HideAttackRangePreview();
        if (weaponSwingCoroutine != null) {
            StopCoroutine(weaponSwingCoroutine);
            weaponSwingCoroutine = null;
        }
        if (activeWeaponVisual != null) {
            Destroy(activeWeaponVisual);
            activeWeaponVisual = null;
        }
        base.OnDestroy();
    }

    private void OnDrawGizmosSelected() {
        if (currentAttackData == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + currentAttackData.centerOffset, currentAttackData.radius);
    }
}

public static class WorldHealthBarGUI {
    private const float BorderSize = 2f;
    private const int BaseFontSize = 16;
    private const int HealthBarSortingOrder = -100;
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    private static readonly Dictionary<int, HealthBarEntry> Entries =
        new Dictionary<int, HealthBarEntry>();
    private static RectTransform canvasRect;
    private static Font healthBarFont;

    private sealed class HealthBarEntry {
        public RectTransform Root;
        public RectTransform Fill;
        public Image FillImage;
        public Text Label;
    }

    public static void Draw(
        UnityEngine.Object owner,
        Vector3 worldPosition,
        float currentHealth,
        float maxHealth,
        Color fillColor,
        float width = 100f,
        float height = 16f) {
        if (owner == null || maxHealth <= 0f) {
            return;
        }

        EnsureCanvas();
        HealthBarEntry entry = GetOrCreateEntry(owner);
        Camera mainCamera = Camera.main;
        if (mainCamera == null) {
            entry.Root.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        bool isVisible = screenPosition.z > 0f &&
                         screenPosition.x >= -width &&
                         screenPosition.x <= Screen.width + width &&
                         screenPosition.y >= -height &&
                         screenPosition.y <= Screen.height + height;
        entry.Root.gameObject.SetActive(isVisible);
        if (!isVisible) {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                null,
                out Vector2 canvasPosition)) {
            entry.Root.gameObject.SetActive(false);
            return;
        }

        float healthRatio = Mathf.Clamp01(currentHealth / maxHealth);
        entry.Root.anchoredPosition = canvasPosition;
        entry.Root.sizeDelta = new Vector2(width, height);
        entry.Fill.sizeDelta = new Vector2(
            Mathf.Max(0f, width - BorderSize * 2f) * healthRatio,
            -BorderSize * 2f
        );
        entry.FillImage.color = fillColor;
        entry.Label.fontSize = BaseFontSize;
        entry.Label.text =
            $"{Mathf.CeilToInt(Mathf.Max(0f, currentHealth))} / {Mathf.CeilToInt(maxHealth)}";
    }

    public static void Remove(UnityEngine.Object owner) {
        if (owner == null) {
            return;
        }

        int ownerId = owner.GetInstanceID();
        if (!Entries.TryGetValue(ownerId, out HealthBarEntry entry)) {
            return;
        }

        Entries.Remove(ownerId);
        if (entry?.Root != null) {
            UnityEngine.Object.Destroy(entry.Root.gameObject);
        }
    }

    private static void EnsureCanvas() {
        if (canvasRect != null) {
            return;
        }

        Entries.Clear();
        GameObject canvasObject = new GameObject(
            "World Health Bars Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)
        );
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = HealthBarSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = canvasObject.GetComponent<RectTransform>();
    }

    private static HealthBarEntry GetOrCreateEntry(UnityEngine.Object owner) {
        int ownerId = owner.GetInstanceID();
        if (Entries.TryGetValue(ownerId, out HealthBarEntry existing) &&
            existing?.Root != null) {
            return existing;
        }

        GameObject rootObject = new GameObject(
            $"Health Bar - {owner.name}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(canvasRect, false);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);

        Image background = rootObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.85f);
        background.raycastTarget = false;

        GameObject fillObject = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform fill = fillObject.GetComponent<RectTransform>();
        fill.SetParent(root, false);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = new Vector2(BorderSize, 0f);

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.raycastTarget = false;

        GameObject labelObject = new GameObject(
            "Health Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Shadow)
        );
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(root, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        if (healthBarFont == null) {
            healthBarFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        Text label = labelObject.GetComponent<Text>();
        label.font = healthBarFont;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        Shadow shadow = labelObject.GetComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;

        HealthBarEntry entry = new HealthBarEntry {
            Root = root,
            Fill = fill,
            FillImage = fillImage,
            Label = label
        };
        Entries[ownerId] = entry;
        return entry;
    }
}
