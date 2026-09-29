using Unity.Netcode;
using Unity.Collections;
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
    private Vector3 preparedAttackDirection = Vector3.right;
    private GameObject attackRangePreview;
    private Mesh attackRangeMesh;
    private Material attackRangeFillMaterial;
    private Material attackRangeOutlineMaterial;
    private Coroutine weaponSwingCoroutine;
    private GameObject activeWeaponVisual;
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
        if (gameMenu != null) {
            gameMenu.OnSelectedIndexChanged -= HandleSelectedIndexChange;
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

    private void OnGUI() {
        if (!IsSpawned) {
            return;
        }

        WorldHealthBarGUI.Draw(
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

        SubmitMovementServerRpc(moveInput.x, moveInput.y, runPressed, jumpPressed);

        if (!GameState.isPlaying || PauseMenu.isOpen || BagMenu.IsOpen || !IsAlive) {
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
        if (!TryGetMouseAimDirection(out Vector3 direction)) {
            return;
        }

        preparedAttackDirection = direction;
        UpdateAttackRangePreviewRotation();
    }

    private bool TryGetMouseAimDirection(out Vector3 direction) {
        direction = Vector3.zero;
        Camera mainCamera = Camera.main;
        if (mainCamera == null || Mouse.current == null) {
            return false;
        }

        Ray mouseRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane playerGroundPlane = new Plane(Vector3.up, transform.position);
        if (!playerGroundPlane.Raycast(mouseRay, out float distance)) {
            return false;
        }

        direction = mouseRay.GetPoint(distance) - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) {
            return false;
        }

        direction.Normalize();
        return true;
    }

    private void UpdateAttackRangePreviewRotation() {
        if (attackRangePreview == null) {
            return;
        }

        Vector3 localDirection = transform.InverseTransformDirection(preparedAttackDirection);
        localDirection.y = 0f;
        if (localDirection.sqrMagnitude < 0.0001f) {
            return;
        }

        float yaw = -Mathf.Atan2(localDirection.z, localDirection.x) * Mathf.Rad2Deg;
        attackRangePreview.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void ShowAttackRangePreview() {
        HideAttackRangePreview();
        if (currentAttackData == null) {
            return;
        }

        attackRangePreview = new GameObject("AttackRangePreview");
        attackRangePreview.transform.SetParent(transform, false);
        attackRangePreview.transform.localPosition = new Vector3(
            currentAttackData.centerOffset.x,
            0.05f,
            currentAttackData.centerOffset.z
        );

        float innerRadius = GetCurrentAttackInnerRadius();
        float outerRadius = Mathf.Max(innerRadius, currentAttackData.radius);
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
            // Draw the two circles separately. Combining them into one loop creates
            // a visible line where the 0-degree and 360-degree points meet.
            outline.positionCount = segmentCount;
            for (int i = 0; i < segmentCount; i++) {
                outline.SetPosition(i, vertices[i * 2 + 1] + Vector3.up * 0.01f);
            }

            if (innerRadius > 0f) {
                GameObject innerOutlineObject = new GameObject("InnerOutline");
                innerOutlineObject.transform.SetParent(attackRangePreview.transform, false);
                LineRenderer innerOutline = innerOutlineObject.AddComponent<LineRenderer>();
                innerOutline.useWorldSpace = false;
                innerOutline.loop = true;
                innerOutline.widthMultiplier = outline.widthMultiplier;
                innerOutline.positionCount = segmentCount;
                innerOutline.startColor = attackRangeOutlineColor;
                innerOutline.endColor = attackRangeOutlineColor;
                innerOutline.numCornerVertices = outline.numCornerVertices;
                innerOutline.sharedMaterial = attackRangeOutlineMaterial;

                for (int i = 0; i < segmentCount; i++) {
                    innerOutline.SetPosition(i, vertices[i * 2] + Vector3.up * 0.01f);
                }
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

        UpdateAttackRangePreviewRotation();
    }

    private float GetCurrentAttackInnerRadius() {
        // A sector starts at the player so its point and its hit area match.
        // Radial attacks keep their existing safe area around the character.
        if (currentAttackData != null && currentAttackData.attackShape == AttackShape.Sector) {
            return 0f;
        }

        return characterController != null ? characterController.radius * 2f : 0f;
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
    private void PlayPreparedUnarmedAttackServerRpc(float aimDirectionX, float aimDirectionZ) {
        if (currentHealth.Value <= 0 || currentAttackUsesWeapon || currentAttackData == null ||
            Time.time < nextServerAttackTime) {
            return;
        }

        Vector3 attackDirection = GetValidatedAttackDirection(aimDirectionX, aimDirectionZ);
        Vector3 attackCenter = transform.position + currentAttackData.centerOffset;
        Vector3 effectPosition = GetMissAttackEffectPosition(attackCenter, attackDirection);
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

        nextServerAttackTime = Time.time + currentAttackData.cooldown;

        Vector3 attackDirection = GetValidatedAttackDirection(aimDirectionX, aimDirectionZ);
        Vector3 attackCenter = transform.position + currentAttackData.centerOffset;
        Collider[] hits = Physics.OverlapSphere(
            attackCenter,
            currentAttackData.radius,
            currentAttackData.targetLayer,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits) {
            if (!IsInsideAttackArea(hit, attackCenter, attackDirection)) {
                continue;
            }

            PlayerController target = hit.GetComponentInParent<PlayerController>();
            if (target != null) {
                if (target == this || !target.IsSpawned) {
                    continue;
                }

                target.TakeDamage(currentAttackData.damage, OwnerClientId);
                PlayAttackVisualClientRpc(selectedItemId, attackDirection, currentAttackUsesWeapon);
                Debug.Log($"[PlayerController] Client {OwnerClientId} hit client {target.OwnerClientId} for {currentAttackData.damage}.", this);
                return;
            }

            EnemyHealthDrop enemy = hit.GetComponentInParent<EnemyHealthDrop>();
            if (enemy == null || !enemy.IsAlive) {
                continue;
            }

            enemy.TakeDamage(currentAttackData.damage, OwnerClientId);
            PlayAttackVisualClientRpc(selectedItemId, attackDirection, currentAttackUsesWeapon);
            Debug.Log($"[PlayerController] Client {OwnerClientId} hit enemy {enemy.name} for {currentAttackData.damage}.", this);
            return;
        }

        PlayAttackVisualClientRpc(selectedItemId, attackDirection, currentAttackUsesWeapon);
        Debug.Log(
            $"[Combat] Client {OwnerClientId} {currentAttackData.attackName} attack missed. " +
            $"Center={attackCenter}, radius={currentAttackData.radius}.",
            this
        );
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

    private Vector3 GetMissAttackEffectPosition(Vector3 attackCenter, Vector3 attackDirection) {
        return attackCenter + attackDirection * currentAttackData.effectForwardOffset;
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
        float characterBottom = characterController != null
            ? characterController.center.y - characterController.height * 0.5f
            : 0f;
        activeWeaponVisual.transform.localPosition = new Vector3(
            attackData.centerOffset.x,
            characterBottom + weaponGroundHeight,
            attackData.centerOffset.z
        );

        GameObject weaponObject = new GameObject("Weapon");
        weaponObject.transform.SetParent(activeWeaponVisual.transform, false);

        SpriteRenderer weaponRenderer = weaponObject.AddComponent<SpriteRenderer>();
        weaponRenderer.sprite = weaponItem.icon;
        weaponRenderer.flipX = weaponFlipX;
        weaponRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        weaponRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 10;

        Quaternion skyFacingRotation =
            Quaternion.AngleAxis(-90f, Vector3.right) *
            Quaternion.AngleAxis(weaponPlanarAngle, Vector3.forward);
        weaponObject.transform.localRotation = skyFacingRotation;

        // Every weapon texture uses the same handle-to-blade layout, so every weapon
        // shares one mirrored handle pivot and one blade-forward orientation.
        Bounds spriteBounds = weaponItem.icon.bounds;
        Vector2 renderedHandlePivot = new Vector2(
            weaponFlipX
                ? 1f - Mathf.Clamp01(weaponHandlePivot.x)
                : Mathf.Clamp01(weaponHandlePivot.x),
            Mathf.Clamp01(weaponHandlePivot.y)
        );
        Vector3 handlePoint = new Vector3(
            Mathf.Lerp(spriteBounds.min.x, spriteBounds.max.x, renderedHandlePivot.x),
            Mathf.Lerp(spriteBounds.min.y, spriteBounds.max.y, renderedHandlePivot.y),
            0f
        );
        float scale = Mathf.Max(0.1f, attackData.weaponVisualScale);
        if (attackData.attackShape == AttackShape.Sector) {
            Vector3 bladePoint = new Vector3(
                weaponFlipX ? spriteBounds.min.x : spriteBounds.max.x,
                spriteBounds.max.y,
                0f
            );
            float spriteLength = Vector3.Distance(handlePoint, bladePoint);
            float targetLength = Mathf.Max(0.1f, attackData.radius);
            scale = targetLength / Mathf.Max(0.01f, spriteLength);
        }

        weaponObject.transform.localScale = Vector3.one * scale;
        weaponObject.transform.localPosition = -(skyFacingRotation * (handlePoint * scale));

        float centerAngle = Mathf.Atan2(attackDirection.x, attackDirection.z) * Mathf.Rad2Deg;
        float halfSwingAngle = Mathf.Clamp(attackData.sectorAngle, 1f, 360f) * 0.5f;
        float startAngle = centerAngle - halfSwingAngle;
        float endAngle = centerAngle + halfSwingAngle;
        float duration = Mathf.Max(0.01f, attackData.cooldown);
        float elapsed = 0f;

        while (elapsed < duration && activeWeaponVisual != null) {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedTime = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            float angle = Mathf.Lerp(startAngle, endAngle, easedTime);

            // The attack pivot only rotates around Y. The child keeps its fixed
            // sky-facing tilt while its position follows the pivot around the player.
            activeWeaponVisual.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }

        if (activeWeaponVisual != null) {
            Destroy(activeWeaponVisual);
            activeWeaponVisual = null;
        }
        weaponSwingCoroutine = null;
    }

    private bool IsInsideAttackArea(Collider hit, Vector3 attackCenter, Vector3 attackDirection) {
        float innerRadius = GetCurrentAttackInnerRadius();
        Vector3 horizontalOffset = hit.ClosestPoint(attackCenter) - attackCenter;
        horizontalOffset.y = 0f;
        float distanceSquared = horizontalOffset.sqrMagnitude;
        if (distanceSquared < innerRadius * innerRadius ||
            distanceSquared > currentAttackData.radius * currentAttackData.radius) {
            return false;
        }

        if (currentAttackData.attackShape != AttackShape.Sector) {
            return true;
        }

        return Vector3.Angle(attackDirection, horizontalOffset) <= currentAttackData.sectorAngle * 0.5f;
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
        if (!IsOwner || !IsClient || !GameState.isPlaying || PauseMenu.isOpen) return;
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
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float BorderSize = 2f;
    private const int BaseFontSize = 16;
    private static GUIStyle healthTextStyle;
    private static GUIStyle healthTextShadowStyle;
    private static int currentFontSize;

    public static void Draw(
        Vector3 worldPosition,
        float currentHealth,
        float maxHealth,
        Color fillColor,
        float width = 100f,
        float height = 16f) {
        if (Event.current.type != EventType.Repaint || maxHealth <= 0f) {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null) {
            return;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f) {
            return;
        }

        screenPosition.y = Screen.height - screenPosition.y;
        if (screenPosition.x < -width || screenPosition.x > Screen.width + width ||
            screenPosition.y < -height || screenPosition.y > Screen.height + height) {
            return;
        }

        float uiScale = GetUIScale();
        float scaledWidth = width * uiScale;
        float scaledHeight = height * uiScale;
        float scaledBorderSize = BorderSize * uiScale;
        float healthRatio = Mathf.Clamp01(currentHealth / maxHealth);
        Rect backgroundRect = new Rect(
            screenPosition.x - scaledWidth * 0.5f,
            screenPosition.y - scaledHeight * 0.5f,
            scaledWidth,
            scaledHeight
        );
        Rect fillRect = new Rect(
            backgroundRect.x + scaledBorderSize,
            backgroundRect.y + scaledBorderSize,
            (backgroundRect.width - scaledBorderSize * 2f) * healthRatio,
            backgroundRect.height - scaledBorderSize * 2f
        );

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(backgroundRect, Texture2D.whiteTexture);

        GUI.color = fillColor;
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.white;
        string healthText = $"{Mathf.CeilToInt(Mathf.Max(0f, currentHealth))} / {Mathf.CeilToInt(maxHealth)}";
        Rect shadowRect = backgroundRect;
        float shadowOffset = Mathf.Max(1f, uiScale);
        shadowRect.x += shadowOffset;
        shadowRect.y += shadowOffset;
        GUI.Label(shadowRect, healthText, GetHealthTextShadowStyle(uiScale));
        GUI.Label(
            backgroundRect,
            healthText,
            GetHealthTextStyle(uiScale)
        );
        GUI.color = previousColor;
    }

    private static float GetUIScale() {
        float widthScale = Screen.width / ReferenceWidth;
        float heightScale = Screen.height / ReferenceHeight;
        return Mathf.Max(0.01f, Mathf.Min(widthScale, heightScale));
    }

    private static GUIStyle GetHealthTextStyle(float uiScale) {
        EnsureTextStyles(uiScale);
        return healthTextStyle;
    }

    private static GUIStyle GetHealthTextShadowStyle(float uiScale) {
        EnsureTextStyles(uiScale);
        return healthTextShadowStyle;
    }

    private static void EnsureTextStyles(float uiScale) {
        int fontSize = Mathf.Max(10, Mathf.RoundToInt(BaseFontSize * uiScale));
        if (healthTextStyle == null) {
            healthTextStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            healthTextStyle.normal.textColor = Color.white;

            healthTextShadowStyle = new GUIStyle(healthTextStyle);
            healthTextShadowStyle.normal.textColor = Color.black;
        }

        if (currentFontSize != fontSize) {
            currentFontSize = fontSize;
            healthTextStyle.fontSize = fontSize;
            healthTextShadowStyle.fontSize = fontSize;
        }
    }
}
