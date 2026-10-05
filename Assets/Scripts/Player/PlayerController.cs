using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(100)]
public class PlayerController : NetworkBehaviour {
    private const string ArrowProjectileItemId = "arrow";
    private const float ServerAttackCooldownGrace = 0.075f;
    private const double ClientAttackCooldownTolerance = 0.02d;
    private static PlayerController activeAttackPreviewOwner;

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
    private ulong selectedItemInstanceId;
    private bool currentAttackUsesWeapon;

    [Header("Attack Range Preview")]
    [SerializeField] private Color attackRangeFillColor = new Color(1f, 0.25f, 0.05f, 0.2f);
    [Range(8, 64)] [SerializeField] private int attackRangeSegments = 32;
    [Min(0.01f)] [SerializeField] private float attackPreviewHeight = 0.12f;

    [Header("Shared Weapon Swing Visual")]
    [Min(0f)] [SerializeField] private float weaponGroundHeight = 0.08f;
    [Tooltip("Normalized handle position shared by every weapon sprite. (0, 0) is bottom-left.")]
    [SerializeField] private Vector2 weaponHandlePivot = new Vector2(0.15f, 0.15f);
    [SerializeField] private bool weaponFlipX = true;
    [Range(-180f, 180f)] [SerializeField] private float weaponPlanarAngle = 135f;
    [Min(0.05f)] [SerializeField] private float weaponHitHeight = 0.5f;
    [Range(1f, 15f)] [SerializeField] private float weaponSweepStepDegrees = 4f;

    [Header("Ranged Weapon Visual")]
    [Min(0f)] [SerializeField] private float bowSideOffset = 0.55f;
    [Min(0f)] [SerializeField] private float bowForwardOffset = 0.25f;
    [SerializeField] private float bowHeightOffset;
    [Range(-180f, 180f)] [SerializeField] private float bowPlanarAngle = -135f;
    [SerializeField] private Color arrowTrailColor = new Color(1f, 0.75f, 0.15f, 0.95f);
    [Min(0.01f)] [SerializeField] private float minimumArrowVisibleDuration = 0.2f;


    [Header("Health Display")]
    [SerializeField] private Vector3 healthLabelOffset = new Vector3(0f, 1.5f, 0f);

    private CharacterController characterController;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private PlayerInventory playerInventory;
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
    private double nextAcceptedClientRangedAttackTime = double.NegativeInfinity;
    private bool isPreparingAttack;
    private float attackMovementLockedUntil;
    private Vector3 preparedAttackDirection = Vector3.right;
    private float preparedAttackPreviewYOffset;
    private float preparedRangedAimYOffset;
    private GameObject attackRangePreview;
    private Mesh attackRangeMesh;
    private Material attackRangeFillMaterial;
    private Material arrowProjectileMaterial;
    private Material arrowTrailMaterial;
    private Coroutine weaponSwingCoroutine;
    private GameObject activeWeaponVisual;
    private readonly Collider[] weaponHitBuffer = new Collider[64];
    private readonly RaycastHit[] projectileHitBuffer = new RaycastHit[32];
    private readonly Dictionary<ulong, GameObject> activeProjectileVisuals =
        new Dictionary<ulong, GameObject>();
    private readonly Dictionary<ulong, Coroutine> projectileVisualCoroutines =
        new Dictionary<ulong, Coroutine>();
    private readonly Dictionary<ulong, float> projectileVisualSpawnTimes =
        new Dictionary<ulong, float>();
    private readonly Dictionary<ulong, float> projectileVisualConfirmedTimes =
        new Dictionary<ulong, float>();
    private readonly Dictionary<ulong, int> projectileVisualSpawnFrames =
        new Dictionary<ulong, int>();
    private ulong nextLocalProjectileId;
    private Vector3 spawnPosition;
    private Coroutine respawnCoroutine;
    private GameInput inputActions;

    public bool IsAlive => currentHealth.Value > 0;

    private void Awake() {
        characterController = GetComponent<CharacterController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        playerInventory = GetComponent<PlayerInventory>();
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
        ClearProjectileVisuals();
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
        // CameraController also follows in LateUpdate. This component has a later
        // execution order, so aiming is calculated from the camera that will render
        // the current frame instead of alternating with the previous camera pose.
        if (IsOwner && IsClient && isPreparingAttack) {
            UpdatePreparedAttackDirection();
        }

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
        if (IsCurrentAttackRanged() && !HasArrowAmmo()) {
            Debug.Log("[Combat] Bow attack ignored because there are no arrows in the inventory.", this);
            return;
        }

        isPreparingAttack = true;
        preparedAttackPreviewYOffset =
            GetAttackPreviewWorldPosition(currentAttackData).y - transform.position.y;
        preparedRangedAimYOffset = GetRangedAimOrigin().y - transform.position.y;
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

        // Release happens in Update, before the LateUpdate aiming pass. Sample the
        // pointer once here so the released shot uses the newest available position.
        UpdatePreparedAttackDirection();
        Vector3 attackDirection = preparedAttackDirection;
        isPreparingAttack = false;
        HideAttackRangePreview();

        if (currentAttackData == null) {
            return;
        }
        if (IsCurrentAttackRanged() && !HasArrowAmmo()) {
            return;
        }

        ExecuteCurrentAttack(attackDirection);
    }

    private void ExecuteCurrentAttack(Vector3 attackDirection) {
        if (currentAttackData == null || (IsCurrentAttackRanged() && !HasArrowAmmo())) {
            return;
        }

        nextLocalAttackTime = Time.time + Mathf.Max(0f, currentAttackData.cooldown);
        if (IsCurrentAttackRanged() &&
            ItemManager.TryGetWeaponById(
                selectedItemId.ToString(),
                out ItemData rangedWeapon,
                out AttackData rangedAttackData)) {
            // Start the owner's bow immediately so its lifetime uses exactly the
            // same clock as nextLocalAttackTime. The server RPC only starts this
            // visual for observing clients.
            StartWeaponSwingVisual(
                rangedWeapon.itemObject,
                rangedAttackData,
                attackDirection
            );
        }

        ulong predictedProjectileId = IsCurrentAttackRanged()
            ? SpawnPredictedArrowProjectile(currentAttackData, attackDirection)
            : 0;
        TryAttackServerRpc(
            attackDirection.x,
            attackDirection.z,
            predictedProjectileId,
            (double)Time.time
        );
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

        Vector3 attackOrigin = IsCurrentAttackRanged()
            ? new Vector3(
                transform.position.x,
                transform.position.y + preparedRangedAimYOffset,
                transform.position.z
            )
            : currentAttackData != null
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
        Vector3 previewPosition = transform.position + currentAttackData.centerOffset;
        previewPosition.y = transform.position.y + preparedAttackPreviewYOffset;
        attackRangePreview.transform.position = previewPosition;

        float yaw = -Mathf.Atan2(attackDirection.z, attackDirection.x) * Mathf.Rad2Deg;
        attackRangePreview.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void ShowAttackRangePreview() {
        HideAttackRangePreview();
        if (activeAttackPreviewOwner != null && activeAttackPreviewOwner != this) {
            activeAttackPreviewOwner.CancelPreparedAttack();
        }
        activeAttackPreviewOwner = this;
        if (currentAttackData == null) {
            activeAttackPreviewOwner = null;
            return;
        }

        // This is a local world-space object. It has no NetworkObject and is only
        // created by the owning client, so other players never receive the preview.
        attackRangePreview = new GameObject("LocalAttackRangePreview") {
            hideFlags = HideFlags.DontSave
        };
        attackRangePreview.transform.localScale = Vector3.one;

        Vector3[] vertices;
        int[] triangles;
        if (currentAttackData.attackmode == AttackMode.WeaponRanged) {
            // A projectile has a fixed collision width. A sector would incorrectly
            // become wider the farther it travels, so ranged attacks use a corridor.
            float range = Mathf.Max(0.1f, currentAttackData.projectileMaxDistance);
            float halfWidth = Mathf.Max(0.08f, currentAttackData.projectileRadius);
            vertices = new[] {
                new Vector3(0f, 0f, -halfWidth),
                new Vector3(0f, 0f, halfWidth),
                new Vector3(range, 0f, -halfWidth),
                new Vector3(range, 0f, halfWidth)
            };
            triangles = new[] {
                0, 1, 3,
                0, 3, 2
            };
        }
        else {
            float outerRadius = Mathf.Max(0f, currentAttackData.radius);
            float arcAngle = currentAttackData.attackShape == AttackShape.Sector
                ? Mathf.Clamp(currentAttackData.sectorAngle, 1f, 360f)
                : 360f;
            const float centerAngle = 0f;
            int segmentCount = Mathf.Max(
                2,
                Mathf.CeilToInt(attackRangeSegments * arcAngle / 360f)
            );

            vertices = new Vector3[segmentCount + 2];
            triangles = new int[segmentCount * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i <= segmentCount; i++) {
                float angle = centerAngle - arcAngle * 0.5f + arcAngle * i / segmentCount;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                vertices[i + 1] = direction * outerRadius;

                if (i == segmentCount) {
                    continue;
                }

                int triangleIndex = i * 3;
                // Wind triangles upward so the ground preview is visible from the gameplay camera.
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 2;
                triangles[triangleIndex + 2] = i + 1;
            }
        }

        attackRangeMesh = new Mesh { name = "Attack Range Preview Mesh" };
        attackRangeMesh.vertices = vertices;
        attackRangeMesh.triangles = triangles;
        Color[] vertexColors = new Color[vertices.Length];
        for (int i = 0; i < vertexColors.Length; i++) {
            vertexColors[i] = Color.white;
        }
        attackRangeMesh.colors = vertexColors;
        attackRangeMesh.RecalculateBounds();

        MeshFilter meshFilter = attackRangePreview.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = attackRangeMesh;
        MeshRenderer meshRenderer = attackRangePreview.AddComponent<MeshRenderer>();
        meshRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        meshRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 9;

        Shader previewShader = Resources.Load<Shader>("AttackPreview");
        if (previewShader == null) {
            previewShader = Shader.Find("Avian/AttackPreview");
        }
        if (previewShader == null) {
            previewShader = Shader.Find("Sprites/Default");
        }
        attackRangeFillMaterial = new Material(previewShader) {
            color = attackRangeFillColor,
            renderQueue = 3000
        };
        attackRangeFillMaterial.mainTexture = Texture2D.whiteTexture;
        meshRenderer.sharedMaterial = attackRangeFillMaterial;

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

        if (activeAttackPreviewOwner == this) {
            activeAttackPreviewOwner = null;
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
    private void TryAttackServerRpc(
        float aimDirectionX,
        float aimDirectionZ,
        ulong predictedProjectileId,
        double clientAttackTime
    ) {
        if (currentHealth.Value <= 0) {
            RejectAttackClientRpc(predictedProjectileId);
            return;
        }

        if (currentAttackData == null) {
            Debug.LogWarning($"[Combat] Server rejected attack from client {OwnerClientId}: no supported weapon is equipped.", this);
            RejectAttackClientRpc(predictedProjectileId);
            return;
        }

        AttackData attackData = currentAttackData;
        bool usesWeapon = currentAttackUsesWeapon;
        FixedString64Bytes attackItemId = selectedItemId;
        ItemData weaponItem = ItemData.Empty;
        AttackData weaponAttackData = null;
        if (usesWeapon &&
            !ItemManager.TryGetWeaponById(
                attackItemId.ToString(),
                out weaponItem,
                out weaponAttackData)) {
            Debug.LogWarning(
                $"[Combat] Server could not resolve equipped weapon '{attackItemId}'.",
                this
            );
            RejectAttackClientRpc(predictedProjectileId);
            return;
        }
        if (usesWeapon) {
            attackData = weaponAttackData;
        }

        bool isRangedAttack = usesWeapon && attackData.attackmode == AttackMode.WeaponRanged;
        if (isRangedAttack) {
            // Server RPCs can arrive closer together than the actual client shots
            // because of network jitter. Validate the owner's release timestamps so
            // a legitimate arrow is not intermittently rejected and removed.
            if (!double.IsFinite(clientAttackTime) ||
                clientAttackTime + ClientAttackCooldownTolerance <
                nextAcceptedClientRangedAttackTime) {
                RejectAttackClientRpc(predictedProjectileId);
                return;
            }
        }
        else if (Time.time + ServerAttackCooldownGrace < nextServerAttackTime) {
            RejectAttackClientRpc(predictedProjectileId);
            return;
        }

        if (usesWeapon && attackData.attackmode == AttackMode.WeaponRanged &&
            (playerInventory == null ||
             !playerInventory.TryConsumeItemOnServer(ArrowProjectileItemId, 1))) {
            Debug.Log(
                $"[Combat] Server rejected bow attack from client {OwnerClientId}: no arrows available.",
                this
            );
            RejectAttackClientRpc(predictedProjectileId);
            return;
        }

        float attackDuration = Mathf.Max(0f, attackData.cooldown);
        if (isRangedAttack) {
            nextAcceptedClientRangedAttackTime = clientAttackTime + attackDuration;
        }
        nextServerAttackTime = Time.time + attackDuration;
        attackMovementLockedUntil = nextServerAttackTime;
        StopMovementOnServer();

        Vector3 attackDirection = GetValidatedAttackDirection(aimDirectionX, aimDirectionZ);
        PlayAttackVisualClientRpc(attackItemId, attackDirection, usesWeapon);

        if (usesWeapon) {
            if (weaponAttackData.attackmode == AttackMode.WeaponRanged) {
                StartCoroutine(RangedProjectileRoutine(
                    weaponAttackData,
                    attackDirection,
                    predictedProjectileId
                ));
            }
            else if (weaponItem.itemObject.icon != null) {
                StartCoroutine(WeaponDamageSweepRoutine(
                    weaponItem.itemObject,
                    weaponAttackData,
                    attackDirection
                ));
            }
            else {
                Debug.LogWarning(
                    $"[Combat] Weapon '{attackItemId}' has no icon for its melee sweep.",
                    this
                );
            }
            return;
        }

        ApplyAreaAttackDamage(attackData, attackDirection);
    }

    [ClientRpc]
    private void RejectAttackClientRpc(ulong predictedProjectileId) {
        if (IsOwner) {
            nextLocalAttackTime = Time.time;
            if (predictedProjectileId != 0) {
                RemoveProjectileVisual(predictedProjectileId, stopCoroutine: true);
                StopActiveWeaponVisual();
            }
        }
    }

    private bool IsCurrentAttackRanged() {
        return currentAttackUsesWeapon &&
            currentAttackData != null &&
            currentAttackData.attackmode == AttackMode.WeaponRanged;
    }

    private bool HasArrowAmmo() {
        return playerInventory != null && playerInventory.HasItem(ArrowProjectileItemId, 1);
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
            if (IsOwner && attackData.attackmode == AttackMode.WeaponRanged) {
                return;
            }
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
        if (weaponItem == null || attackData == null) {
            return;
        }

        StopActiveWeaponVisual();

        if (attackData.attackmode == AttackMode.WeaponRanged) {
            if (weaponItem.icon != null) {
                weaponSwingCoroutine = StartCoroutine(BowVisualRoutine(
                    weaponItem,
                    attackData,
                    attackDirection
                ));
            }
            return;
        }

        if (weaponItem.icon == null) {
            return;
        }

        weaponSwingCoroutine = StartCoroutine(WeaponSwingRoutine(weaponItem, attackData, attackDirection));
    }

    private void StopActiveWeaponVisual() {
        if (weaponSwingCoroutine != null) {
            StopCoroutine(weaponSwingCoroutine);
            weaponSwingCoroutine = null;
        }
        if (activeWeaponVisual != null) {
            Destroy(activeWeaponVisual);
            activeWeaponVisual = null;
        }
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

    private IEnumerator BowVisualRoutine(
        ItemObject bowItem,
        AttackData attackData,
        Vector3 attackDirection
    ) {
        activeWeaponVisual = new GameObject("Bow_AttackVisual") {
            hideFlags = HideFlags.DontSave
        };
        activeWeaponVisual.transform.SetParent(transform, false);
        Vector3 playerScale = transform.lossyScale;
        activeWeaponVisual.transform.localScale = new Vector3(
            GetSafeInverseScale(playerScale.x),
            GetSafeInverseScale(playerScale.y),
            GetSafeInverseScale(playerScale.z)
        );

        GameObject bowObject = new GameObject("Bow");
        bowObject.transform.SetParent(activeWeaponVisual.transform, false);
        bowObject.transform.localRotation =
            Quaternion.AngleAxis(-90f, Vector3.right) *
            Quaternion.AngleAxis(bowPlanarAngle, Vector3.forward);

        SpriteRenderer bowRenderer = bowObject.AddComponent<SpriteRenderer>();
        bowRenderer.sprite = bowItem.icon;
        bowRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        bowRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 2 : 11;

        Vector2 bowSize = bowItem.icon.bounds.size;
        float longestSide = Mathf.Max(0.01f, Mathf.Max(bowSize.x, bowSize.y));
        bowObject.transform.localScale = Vector3.one *
            (Mathf.Max(0.1f, attackData.weaponVisualScale) / longestSide);

        float duration = Mathf.Max(0.01f, attackData.cooldown);
        float startedAt = Time.time;
        while (activeWeaponVisual != null && Time.time - startedAt < duration) {
            activeWeaponVisual.transform.position = GetBowWorldPosition(attackDirection);
            activeWeaponVisual.transform.rotation = Quaternion.LookRotation(attackDirection, Vector3.up);
            yield return null;
        }

        if (activeWeaponVisual != null) {
            Destroy(activeWeaponVisual);
            activeWeaponVisual = null;
        }
        weaponSwingCoroutine = null;
    }

    private Vector3 GetBowWorldPosition(Vector3 attackDirection) {
        Vector3 direction = attackDirection;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 position = GetRangedAimOrigin();
        position += right * bowSideOffset + direction * bowForwardOffset;
        return position;
    }

    private Vector3 GetRangedAimOrigin() {
        return new Vector3(
            transform.position.x,
            GetConfiguredCharacterCenterY() + bowHeightOffset,
            transform.position.z
        );
    }

    private Vector3 GetArrowSpawnPosition() {
        // The bow itself is displayed beside the player, but the arrow's path
        // begins at the player. A forward offset shifts the start down-screen
        // whenever the player aims toward the camera.
        return GetRangedAimOrigin();
    }

    private ulong SpawnPredictedArrowProjectile(
        AttackData attackData,
        Vector3 attackDirection
    ) {
        ulong projectileId = ++nextLocalProjectileId;
        if (projectileId == 0) {
            projectileId = ++nextLocalProjectileId;
        }

        Vector3 direction = attackDirection;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.forward;
        Vector3 startPosition = GetArrowSpawnPosition();

        CreateArrowProjectileVisual(
            projectileId,
            startPosition,
            direction,
            Mathf.Max(0.1f, attackData.projectileSpeed),
            Mathf.Max(0.1f, attackData.projectileMaxDistance),
            Mathf.Max(0.1f, attackData.projectileVisualLength),
            Mathf.Max(0.01f, attackData.projectileTrailWidth),
            Mathf.Max(0.01f, attackData.projectileTrailDuration)
        );
        return projectileId;
    }

    private IEnumerator RangedProjectileRoutine(
        AttackData attackData,
        Vector3 attackDirection,
        ulong predictedProjectileId
    ) {
        Vector3 direction = attackDirection;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        float speed = Mathf.Max(0.1f, attackData.projectileSpeed);
        float maxDistance = Mathf.Max(0.1f, attackData.projectileMaxDistance);
        float radius = Mathf.Max(0.01f, attackData.projectileRadius);
        // Keep the projectile on the same center-to-cursor line used for aiming.
        // The bow may remain beside the player, but it must not offset the shot path.
        Vector3 position = GetArrowSpawnPosition();
        ulong projectileId = predictedProjectileId;
        if (projectileId == 0) {
            projectileId = ++nextLocalProjectileId;
            if (projectileId == 0) {
                projectileId = ++nextLocalProjectileId;
            }
        }

        SpawnArrowProjectileClientRpc(
            projectileId,
            direction,
            speed,
            maxDistance,
            Mathf.Max(0.1f, attackData.projectileVisualLength),
            Mathf.Max(0.01f, attackData.projectileTrailWidth),
            Mathf.Max(0.01f, attackData.projectileTrailDuration)
        );

        float distanceTravelled = 0f;
        while (IsServer && distanceTravelled < maxDistance) {
            yield return new WaitForFixedUpdate();

            float stepDistance = Mathf.Min(
                speed * Time.fixedDeltaTime,
                maxDistance - distanceTravelled
            );
            if (TryGetClosestProjectileHit(
                    position,
                    radius,
                    direction,
                    stepDistance,
                    out RaycastHit closestHit)) {
                bool hitDamageTarget = IsProjectileDamageTarget(closestHit.collider) &&
                    IsLayerInMask(closestHit.collider.gameObject.layer, attackData.targetLayer);
                if (hitDamageTarget) {
                    HashSet<PlayerController> damagedPlayers = new HashSet<PlayerController>();
                    HashSet<EnemyHealthDrop> damagedEnemies = new HashSet<EnemyHealthDrop>();
                    TryDamageTarget(
                        closestHit.collider,
                        attackData,
                        damagedPlayers,
                        damagedEnemies
                    );
                }

                Vector3 hitPosition = position + direction * closestHit.distance;
                FinishArrowProjectileClientRpc(
                    projectileId,
                    hitPosition,
                    ensureMinimumVisibleTime: true
                );
                yield break;
            }

            position += direction * stepDistance;
            distanceTravelled += stepDistance;
        }

        FinishArrowProjectileClientRpc(
            projectileId,
            position,
            ensureMinimumVisibleTime: false
        );
    }

    private bool IsProjectileDamageTarget(Collider hit) {
        PlayerController targetPlayer = hit.GetComponentInParent<PlayerController>();
        if (targetPlayer != null) {
            return targetPlayer != this && targetPlayer.IsSpawned && targetPlayer.IsAlive;
        }

        EnemyHealthDrop targetEnemy = hit.GetComponentInParent<EnemyHealthDrop>();
        return targetEnemy != null && targetEnemy.IsAlive;
    }

    private bool TryGetClosestProjectileHit(
        Vector3 position,
        float radius,
        Vector3 direction,
        float distance,
        out RaycastHit closestHit
    ) {
        int hitCount = Physics.SphereCastNonAlloc(
            position,
            radius,
            direction,
            projectileHitBuffer,
            distance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );

        bool foundHit = false;
        closestHit = default;
        for (int i = 0; i < hitCount; i++) {
            Collider hitCollider = projectileHitBuffer[i].collider;
            if (hitCollider == null ||
                hitCollider.GetComponentInParent<PlayerController>() == this) {
                continue;
            }

            if (!foundHit || projectileHitBuffer[i].distance < closestHit.distance) {
                foundHit = true;
                closestHit = projectileHitBuffer[i];
            }
        }

        return foundHit;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask) {
        return (mask.value & (1 << layer)) != 0;
    }

    [ClientRpc]
    private void SpawnArrowProjectileClientRpc(
        ulong projectileId,
        Vector3 direction,
        float speed,
        float maxDistance,
        float visualLength,
        float trailWidth,
        float trailDuration
    ) {
        // Keep the owner's still-visible predicted arrow; restore it if it is
        // missing. Other clients create their visual from this server message.
        if (IsOwner && activeProjectileVisuals.TryGetValue(projectileId, out GameObject existingVisual) &&
            existingVisual != null) {
            projectileVisualConfirmedTimes[projectileId] = Time.time;
            return;
        }

        CreateArrowProjectileVisual(
            projectileId,
            GetArrowSpawnPosition(),
            direction,
            speed,
            maxDistance,
            visualLength,
            trailWidth,
            trailDuration
        );
        if (activeProjectileVisuals.ContainsKey(projectileId)) {
            projectileVisualConfirmedTimes[projectileId] = Time.time;
        }
    }

    private void CreateArrowProjectileVisual(
        ulong projectileId,
        Vector3 startPosition,
        Vector3 direction,
        float speed,
        float maxDistance,
        float visualLength,
        float trailWidth,
        float trailDuration
    ) {
        RemoveProjectileVisual(projectileId, stopCoroutine: true);

        ItemObject arrowObject = null;
        if (ItemManager.TryGetItemById(ArrowProjectileItemId, out ItemData arrowItem)) {
            arrowObject = arrowItem.itemObject;
        }
        if (arrowObject == null || arrowObject.icon == null) {
            // This also covers the first shot during scene startup, before the item
            // registry has finished building on a client.
            arrowObject = Resources.Load<ItemObject>("Items/Arrow");
        }
        if (arrowObject == null || arrowObject.icon == null) {
            Debug.LogWarning("[Combat] Arrow visual could not be created because its sprite is unavailable.", this);
            return;
        }

        GameObject projectileVisual = new GameObject($"Arrow_Projectile_{projectileId}") {
            hideFlags = HideFlags.DontSave
        };
        projectileVisual.transform.position = startPosition;
        projectileVisual.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        // Keep the launch point attached until its first rendered frame. On a
        // host, player movement can still run later in the same Update.
        projectileVisual.transform.SetParent(transform, true);

        GameObject spriteObject = new GameObject("Sprite");
        spriteObject.transform.SetParent(projectileVisual.transform, false);
        spriteObject.transform.localRotation =
            Quaternion.AngleAxis(-90f, Vector3.right) *
            Quaternion.AngleAxis(-135f, Vector3.forward);

        SpriteRenderer arrowRenderer = spriteObject.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = arrowObject.icon;
        arrowRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        arrowRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 3 : 11;
        Material projectileMaterial = GetArrowProjectileMaterial(arrowObject.icon.texture);
        if (projectileMaterial != null) {
            arrowRenderer.sharedMaterial = projectileMaterial;
        }

        Vector2 spriteSize = arrowObject.icon.bounds.size;
        float spriteLongestSide = Mathf.Max(0.01f, Mathf.Max(spriteSize.x, spriteSize.y));
        spriteObject.transform.localScale = Vector3.one * (visualLength / spriteLongestSide);

        TrailRenderer trail = projectileVisual.AddComponent<TrailRenderer>();
        trail.time = trailDuration;
        trail.startWidth = trailWidth;
        trail.endWidth = 0f;
        trail.startColor = arrowTrailColor;
        trail.endColor = new Color(
            arrowTrailColor.r,
            arrowTrailColor.g,
            arrowTrailColor.b,
            0f
        );
        trail.minVertexDistance = Mathf.Max(0.01f, trailWidth * 0.25f);
        trail.textureMode = LineTextureMode.Stretch;
        trail.alignment = LineAlignment.View;
        trail.sortingLayerID = arrowRenderer.sortingLayerID;
        trail.sortingOrder = arrowRenderer.sortingOrder - 1;
        Material trailMaterial = GetArrowTrailMaterial();
        trail.sharedMaterial = trailMaterial != null
            ? trailMaterial
            : arrowRenderer.sharedMaterial;

        activeProjectileVisuals[projectileId] = projectileVisual;
        projectileVisualSpawnTimes[projectileId] = Time.time;
        projectileVisualSpawnFrames[projectileId] = Time.frameCount;
        Coroutine routine = StartCoroutine(ArrowProjectileVisualRoutine(
            projectileId,
            projectileVisual,
            direction.normalized,
            speed,
            maxDistance
        ));
        projectileVisualCoroutines[projectileId] = routine;
    }

    private IEnumerator ArrowProjectileVisualRoutine(
        ulong projectileId,
        GameObject projectileVisual,
        Vector3 direction,
        float speed,
        float maxDistance
    ) {
        // StartCoroutine runs up to its first yield immediately. Draw one frame
        // at the player's position before detaching and advancing the arrow.
        yield return null;
        if (projectileVisual != null) {
            projectileVisual.transform.SetParent(null, true);
        }

        float distanceTravelled = 0f;
        while (projectileVisual != null && distanceTravelled < maxDistance) {
            float stepDistance = Mathf.Min(
                Mathf.Max(0f, speed) * Time.deltaTime,
                maxDistance - distanceTravelled
            );
            projectileVisual.transform.position += direction * stepDistance;
            distanceTravelled += stepDistance;
            yield return null;
        }

        // The server alone decides when the arrow hits a target or obstacle.
        // Wait for its finish message even after the visual reaches max distance;
        // a client-side collision or timer can otherwise hide a successful shot.
        float timeout = Time.time + 2f;
        while (projectileVisual != null && Time.time < timeout) {
            yield return null;
        }

        RemoveProjectileVisual(projectileId, stopCoroutine: false);
    }

    private Material GetArrowProjectileMaterial(Texture arrowTexture) {
        if (arrowProjectileMaterial != null) {
            if (arrowProjectileMaterial.mainTexture != arrowTexture) {
                arrowProjectileMaterial.mainTexture = arrowTexture;
            }
            return arrowProjectileMaterial;
        }

        Shader shader = Resources.Load<Shader>("AttackPreview");
        if (shader == null) {
            shader = Shader.Find("Avian/AttackPreview");
        }
        if (shader == null) {
            return null;
        }

        arrowProjectileMaterial = new Material(shader) {
            color = Color.white,
            mainTexture = arrowTexture,
            renderQueue = 3100
        };
        return arrowProjectileMaterial;
    }

    private Material GetArrowTrailMaterial() {
        if (arrowTrailMaterial != null) {
            return arrowTrailMaterial;
        }

        Shader shader = Resources.Load<Shader>("AttackPreview");
        if (shader == null) {
            shader = Shader.Find("Avian/AttackPreview");
        }
        if (shader == null) {
            return null;
        }

        arrowTrailMaterial = new Material(shader) {
            color = Color.white,
            mainTexture = Texture2D.whiteTexture,
            renderQueue = 3099
        };
        return arrowTrailMaterial;
    }

    [ClientRpc]
    private void FinishArrowProjectileClientRpc(
        ulong projectileId,
        Vector3 finalPosition,
        bool ensureMinimumVisibleTime
    ) {
        if (!activeProjectileVisuals.TryGetValue(projectileId, out GameObject visual) ||
            visual == null) {
            RemoveProjectileVisual(projectileId, stopCoroutine: true);
            return;
        }

        if (projectileVisualCoroutines.TryGetValue(projectileId, out Coroutine movementRoutine) &&
            movementRoutine != null) {
            StopCoroutine(movementRoutine);
        }

        if (projectileVisualSpawnFrames.TryGetValue(projectileId, out int spawnFrame) &&
            Time.frameCount == spawnFrame) {
            // A nearby hit can be reported before this client renders the spawn.
            // Preserve the first frame at the player, then show the impact.
            projectileVisualCoroutines[projectileId] = StartCoroutine(
                FinishArrowProjectileAfterFirstFrame(
                    projectileId,
                    finalPosition,
                    ensureMinimumVisibleTime
                )
            );
            return;
        }

        CompleteArrowProjectileVisual(projectileId, finalPosition, ensureMinimumVisibleTime);
    }

    private IEnumerator FinishArrowProjectileAfterFirstFrame(
        ulong projectileId,
        Vector3 finalPosition,
        bool ensureMinimumVisibleTime
    ) {
        yield return null;
        CompleteArrowProjectileVisual(projectileId, finalPosition, ensureMinimumVisibleTime);
    }

    private void CompleteArrowProjectileVisual(
        ulong projectileId,
        Vector3 finalPosition,
        bool ensureMinimumVisibleTime
    ) {
        if (!activeProjectileVisuals.TryGetValue(projectileId, out GameObject visual) ||
            visual == null) {
            RemoveProjectileVisual(projectileId, stopCoroutine: false);
            return;
        }

        visual.transform.SetParent(null, true);
        visual.transform.position = finalPosition;

        float remainingVisibleTime = 0f;
        if (ensureMinimumVisibleTime) {
            float visibleSince = projectileVisualConfirmedTimes.TryGetValue(
                projectileId,
                out float confirmedAt
            )
                ? confirmedAt
                : projectileVisualSpawnTimes.TryGetValue(projectileId, out float spawnedAt)
                    ? spawnedAt
                    : Time.time;
            remainingVisibleTime = Mathf.Max(
                0f,
                minimumArrowVisibleDuration - (Time.time - visibleSince)
            );
        }

        if (remainingVisibleTime <= 0f) {
            RemoveProjectileVisual(projectileId, stopCoroutine: false);
            return;
        }

        projectileVisualCoroutines[projectileId] = StartCoroutine(
            RemoveProjectileVisualAfterDelay(projectileId, remainingVisibleTime)
        );
    }

    private IEnumerator RemoveProjectileVisualAfterDelay(ulong projectileId, float delay) {
        yield return new WaitForSeconds(delay);
        RemoveProjectileVisual(projectileId, stopCoroutine: false);
    }

    private void RemoveProjectileVisual(ulong projectileId, bool stopCoroutine) {
        if (stopCoroutine &&
            projectileVisualCoroutines.TryGetValue(projectileId, out Coroutine routine) &&
            routine != null) {
            StopCoroutine(routine);
        }

        if (activeProjectileVisuals.TryGetValue(projectileId, out GameObject visual) &&
            visual != null) {
            Destroy(visual);
        }
        activeProjectileVisuals.Remove(projectileId);
        projectileVisualCoroutines.Remove(projectileId);
        projectileVisualSpawnTimes.Remove(projectileId);
        projectileVisualConfirmedTimes.Remove(projectileId);
        projectileVisualSpawnFrames.Remove(projectileId);
    }

    private void ClearProjectileVisuals() {
        foreach (Coroutine routine in projectileVisualCoroutines.Values) {
            if (routine != null) {
                StopCoroutine(routine);
            }
        }
        foreach (GameObject visual in activeProjectileVisuals.Values) {
            if (visual != null) {
                Destroy(visual);
            }
        }
        projectileVisualCoroutines.Clear();
        activeProjectileVisuals.Clear();
        projectileVisualSpawnTimes.Clear();
        projectileVisualConfirmedTimes.Clear();
        projectileVisualSpawnFrames.Clear();
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

        return new Vector3(
            attackCenter.x,
            GetConfiguredCharacterFeetY() + weaponGroundHeight,
            attackCenter.z
        );
    }

    private float GetConfiguredCharacterCenterY() {
        if (characterController == null) {
            return transform.position.y;
        }

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        return transform.position.y + characterController.center.y * scaleY;
    }

    private float GetConfiguredCharacterFeetY() {
        if (characterController == null) {
            return transform.position.y;
        }

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float configuredHeight = Mathf.Max(
            characterController.height,
            characterController.radius * 2f
        ) * scaleY;
        return GetConfiguredCharacterCenterY() - configuredHeight * 0.5f;
    }

    private Vector3 GetAttackPreviewWorldPosition(AttackData attackData) {
        // Keep the preview slightly above both the floor and weapon sweep plane so
        // its transparent mesh does not flicker from depth fighting with either one.
        return GetAttackVisualWorldPosition(attackData) + Vector3.up * attackPreviewHeight;
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

        // Arrow consumption refreshes the bag after every shot. Re-selecting the
        // unchanged bow would call CancelPreparedAttack and could cancel the next
        // shot depending on when the inventory update arrived.
        if (IsSelectedInventorySlotUnchanged(gameMenu.SelectedIndex)) {
            return;
        }

        HandleSelectedIndexChange(gameMenu.SelectedIndex);
    }

    private bool IsSelectedInventorySlotUnchanged(int slotIndex) {
        if (bagManager == null || slotIndex < 0 || slotIndex >= bagManager.bagDataList.Count) {
            return selectedItemInstanceId == 0 && selectedItemId.Length == 0;
        }

        BagSlotData slot = bagManager.bagDataList[slotIndex];
        if (slot == null || slot.IsEmpty || slot.itemData.itemObject == null) {
            return selectedItemInstanceId == 0 && selectedItemId.Length == 0;
        }

        if (slot.instanceId != 0 || selectedItemInstanceId != 0) {
            return slot.instanceId == selectedItemInstanceId;
        }

        return string.Equals(
            slot.itemData.itemObject.itemId,
            selectedItemId.ToString(),
            System.StringComparison.Ordinal
        );
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
        ulong newSelectedItemInstanceId = 0;
        AttackData localAttackData = null;
        bool localUsesWeapon = false;
        string selectedItemName = "Empty";

        BagSlotData slot = slotIndex < bagManager.bagDataList.Count
            ? bagManager.bagDataList[slotIndex]
            : null;
        if (slot != null && !slot.IsEmpty && slot.itemData.itemObject != null) {
            ItemObject item = slot.itemData.itemObject;
            newSelectedItemInstanceId = slot.instanceId;
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
        selectedItemInstanceId = newSelectedItemInstanceId;
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
        selectedItemId = default;
        selectedItemInstanceId = 0;
        currentAttackUsesWeapon = false;
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
        ClearProjectileVisuals();
        HideAttackRangePreview();
        if (weaponSwingCoroutine != null) {
            StopCoroutine(weaponSwingCoroutine);
            weaponSwingCoroutine = null;
        }
        if (activeWeaponVisual != null) {
            Destroy(activeWeaponVisual);
            activeWeaponVisual = null;
        }
        if (arrowProjectileMaterial != null) {
            Destroy(arrowProjectileMaterial);
            arrowProjectileMaterial = null;
        }
        if (arrowTrailMaterial != null) {
            Destroy(arrowTrailMaterial);
            arrowTrailMaterial = null;
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
