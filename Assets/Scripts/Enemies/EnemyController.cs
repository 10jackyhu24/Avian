using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController), typeof(NetworkObject))]
public class EnemyController : NetworkBehaviour {
    [Header("Patrol")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float patrolRadius = 8f;
    [SerializeField] private float patrolPointReachedDistance = 0.75f;
    [SerializeField] private float patrolPauseDuration = 0.75f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float targetRefreshInterval = 0.25f;

    [Header("Movement")]
    [SerializeField] private float gravity = -20f;

    [Header("Hit Reaction")]
    [Min(0f)] [SerializeField] private float knockbackDistance = 0.8f;
    [Min(0.01f)] [SerializeField] private float knockbackDuration = 0.2f;
    [Range(0.05f, 0.95f)] [SerializeField] private float firstStageDistanceShare = 0.6f;
    [Range(0.05f, 0.95f)] [SerializeField] private float firstStageTimeShare = 0.35f;
    [Min(0f)] [SerializeField] private float firstStageLiftHeight;

    [Header("Camera Culling")]
    [Tooltip("Hide offscreen renderers without stopping movement, collisions or attacks.")]
    [SerializeField] private bool disableWhenOutOfView = true;
    [SerializeField] private Vector3 visibilityBoundsSize = new Vector3(2f, 3f, 2f);
    [SerializeField] private float visibilityCheckInterval = 0.2f;

    [Header("Enemy Visuals")]
    [SerializeField] private RuntimeAnimatorController raiderAnimatorController;
    [SerializeField] private RuntimeAnimatorController spitterAnimatorController;
    [SerializeField] private RuntimeAnimatorController miniBossAnimatorController;
    [SerializeField] private RuntimeAnimatorController bossAnimatorController;

    private CharacterController characterController;
    private EnemyAttackController attackController;
    private Camera mainCamera;
    private SpriteRenderer[] spriteRenderers;
    private bool[] initialSpriteFlipX;
    private bool localFacingLeft;
    private readonly NetworkVariable<bool> facingLeft = new NetworkVariable<bool>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<EnemyArchetype> networkArchetype = new NetworkVariable<EnemyArchetype>(
        EnemyArchetype.Grunt,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<bool> networkIsAttacking = new NetworkVariable<bool>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );
    private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving");
    private static readonly int IsAttackingParameter = Animator.StringToHash("IsAttacking");
    private static readonly int AttackSpeedParameter = Animator.StringToHash("AttackSpeed");
    private EnemyArchetype pendingArchetype = EnemyArchetype.Grunt;
    private Transform visualTransform;
    private Vector3 initialVisualScale;
    private SpriteRenderer visualSpriteRenderer;
    private bool visualSpriteFacesLeft;
    private bool usesMovementAnimations;
    private bool usesArchetypeAnimations;
    private Vector3 previousAnimationPosition;
    private float movingAnimationUntil;
    private Animator visualAnimator;
    private RuntimeAnimatorController initialAnimatorController;
    private CapsuleCollider bodyCollider;
    private float initialControllerHeight;
    private float initialControllerRadius;
    private Vector3 initialControllerCenter;
    private float initialColliderHeight;
    private float initialColliderRadius;
    private Vector3 initialColliderCenter;
    private Vector3 initialVisibilityBoundsSize;
    private Renderer[] cachedRenderers;
    private bool[] rendererEnabledStates;
    private PlayerController playerTarget;
    private Vector3 patrolCenter;
    private Vector3 currentPatrolPoint;
    private float verticalVelocity;
    private float patrolPauseTimer;
    private float nextTargetSearchTime;
    private float nextVisibilityCheckTime;
    private bool visualsHiddenByCamera;
    private Vector3 knockbackDirection;
    private int knockbackStage;
    private float stageRemainingDistance;
    private float stageRemainingTime;
    private float secondStageDistance;
    private float secondStageDuration;
    private float avoidanceSide = 1f;
    private float avoidanceUntil;

    public bool IsBossType => (IsSpawned ? networkArchetype.Value : pendingArchetype) >= EnemyArchetype.MiniBoss;
    public EnemyArchetype Archetype => IsSpawned ? networkArchetype.Value : pendingArchetype;
    public bool IsPursuingPlayer => IsValidTarget(playerTarget);

    private void Awake() {
        characterController = GetComponent<CharacterController>();
        attackController = GetComponent<EnemyAttackController>();
        mainCamera = Camera.main;
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        initialSpriteFlipX = new bool[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++) {
            initialSpriteFlipX[i] = spriteRenderers[i].flipX;
        }
        visualSpriteRenderer = spriteRenderers.Length > 0 ? spriteRenderers[0] : null;
        visualTransform = visualSpriteRenderer != null ? visualSpriteRenderer.transform : null;
        initialVisualScale = visualTransform != null ? visualTransform.localScale : Vector3.one;
        visualAnimator = visualTransform != null ? visualTransform.GetComponent<Animator>() : null;
        initialAnimatorController = visualAnimator != null ? visualAnimator.runtimeAnimatorController : null;
        previousAnimationPosition = transform.position;
        bodyCollider = GetComponent<CapsuleCollider>();
        initialControllerHeight = characterController.height;
        initialControllerRadius = characterController.radius;
        initialControllerCenter = characterController.center;
        if (bodyCollider != null) {
            initialColliderHeight = bodyCollider.height;
            initialColliderRadius = bodyCollider.radius;
            initialColliderCenter = bodyCollider.center;
        }
        initialVisibilityBoundsSize = visibilityBoundsSize;
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        rendererEnabledStates = new bool[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++) {
            rendererEnabledStates[i] = cachedRenderers[i].enabled;
        }

        patrolCenter = transform.position;
        currentPatrolPoint = patrolCenter;
        PickNextPatrolPoint();
    }

    public override void OnNetworkSpawn() {
        facingLeft.OnValueChanged += HandleFacingChanged;
        networkArchetype.OnValueChanged += HandleArchetypeChanged;
        if (IsServer) {
            facingLeft.Value = localFacingLeft;
            networkArchetype.Value = pendingArchetype;
            networkIsAttacking.Value = false;
        }
        previousAnimationPosition = transform.position;
        movingAnimationUntil = 0f;
        ApplyArchetype(networkArchetype.Value);
        ApplyFacing(facingLeft.Value);
    }

    public override void OnNetworkDespawn() {
        facingLeft.OnValueChanged -= HandleFacingChanged;
        networkArchetype.OnValueChanged -= HandleArchetypeChanged;
    }

    public void ConfigureArchetype(EnemyArchetype archetype) {
        if (IsSpawned && !IsServer) return;
        pendingArchetype = archetype;
        ApplyArchetype(archetype);
        if (IsSpawned) networkArchetype.Value = archetype;
    }

    private void HandleArchetypeChanged(EnemyArchetype previousValue, EnemyArchetype currentValue) {
        ApplyArchetype(currentValue);
    }

    private void ApplyArchetype(EnemyArchetype archetype) {
        EnemyArchetypeStats stats = EnemyArchetypes.Get(archetype);
        patrolSpeed = stats.PatrolSpeed;
        chaseSpeed = stats.ChaseSpeed;
        visibilityBoundsSize = initialVisibilityBoundsSize * stats.ColliderScale;

        if (visualTransform != null) {
            visualTransform.localScale = initialVisualScale * stats.VisualScale;
        }
        RuntimeAnimatorController controller = archetype switch {
            EnemyArchetype.Raider => raiderAnimatorController,
            EnemyArchetype.Spitter => spitterAnimatorController,
            EnemyArchetype.MiniBoss => miniBossAnimatorController,
            EnemyArchetype.Boss => bossAnimatorController,
            _ => initialAnimatorController
        };
        usesArchetypeAnimations = controller != null &&
            (archetype == EnemyArchetype.Raider || archetype == EnemyArchetype.Spitter || archetype == EnemyArchetype.MiniBoss);
        // Medusa has idle/move states but no attack animation parameters.
        usesMovementAnimations = controller != null &&
            (usesArchetypeAnimations || archetype == EnemyArchetype.Boss);
        if (controller == null) controller = initialAnimatorController;
        if (visualAnimator != null) {
            bool changed = !visualAnimator.enabled || visualAnimator.runtimeAnimatorController != controller;
            visualAnimator.runtimeAnimatorController = controller;
            visualAnimator.enabled = true;
            if (changed) {
                visualAnimator.Rebind();
                visualAnimator.Update(0f);
            }
            if (usesArchetypeAnimations) {
                // The attack clip is 0.6 seconds long. Match windup and the full charge.
                float duration = stats.Windup + (stats.Pattern == EnemyAttackPattern.Charge ? stats.ChargeDuration : 0f);
                visualAnimator.SetFloat(AttackSpeedParameter, 0.6f / Mathf.Max(0.05f, duration));
            }
        }
        visualSpriteFacesLeft = stats.SpriteFacesLeft;
        ApplyFacing(localFacingLeft);

        float heightIncrease = initialControllerHeight * (stats.ColliderScale - 1f);
        characterController.height = initialControllerHeight * stats.ColliderScale;
        characterController.radius = initialControllerRadius * stats.ColliderScale;
        characterController.center = initialControllerCenter + Vector3.up * (heightIncrease * 0.5f);
        if (bodyCollider != null) {
            bodyCollider.height = initialColliderHeight * stats.ColliderScale;
            bodyCollider.radius = initialColliderRadius * stats.ColliderScale;
            bodyCollider.center = initialColliderCenter + Vector3.up * (initialColliderHeight * (stats.ColliderScale - 1f) * 0.5f);
        }

        GetComponent<EnemyHealthDrop>()?.ApplyArchetype(stats);
        attackController?.ApplyArchetype(stats);
    }

    private void HandleFacingChanged(bool previousValue, bool currentValue) {
        ApplyFacing(currentValue);
    }

    private void ApplyFacing(bool isFacingLeft) {
        localFacingLeft = isFacingLeft;
        for (int i = 0; i < spriteRenderers.Length; i++) {
            if (spriteRenderers[i] != null) {
                spriteRenderers[i].flipX = spriteRenderers[i] == visualSpriteRenderer
                    ? isFacingLeft ^ visualSpriteFacesLeft
                    : initialSpriteFlipX[i] ^ isFacingLeft;
            }
        }
    }

    private void UpdateFacing(Vector3 direction) {
        // Keep the authored rotation and retain facing during vertical movement.
        if (Mathf.Abs(direction.x) <= 0.01f) {
            return;
        }

        bool isFacingLeft = direction.x < 0f;
        if (IsSpawned && IsServer) {
            facingLeft.Value = isFacingLeft;
        }
        ApplyFacing(isFacingLeft);
    }

    private void Update() {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer) {
            return;
        }

        if (!GameState.isPlaying) return;

        ResolvePlayerTarget();
        if (playerTarget != null) {
            UpdateFacing(playerTarget.transform.position - transform.position);
        }

        if (knockbackStage != 0 && Time.deltaTime > 0f) {
            UpdateKnockback(Time.deltaTime);
            return;
        }

        if (playerTarget != null) {
            UpdateChase();
        } else {
            UpdatePatrol();
        }
    }

    private void LateUpdate() {
        RefreshVisibilityState();
        bool localAttack = GameState.isPlaying && attackController != null && attackController.IsAttacking;
        if (IsSpawned && IsServer && networkIsAttacking.Value != localAttack) {
            networkIsAttacking.Value = localAttack;
        }

        Vector3 displacement = transform.position - previousAnimationPosition;
        previousAnimationPosition = transform.position;
        if (!usesMovementAnimations || visualAnimator == null || !visualAnimator.enabled) return;

        displacement.y = 0f;
        float minimumStep = 0.1f * Time.deltaTime;
        if (Time.deltaTime > 0f && displacement.sqrMagnitude > minimumStep * minimumStep) {
            // A short hold prevents idle/move flicker between NetworkTransform updates.
            movingAnimationUntil = Time.time + 0.12f;
        }
        visualAnimator.SetBool(IsMovingParameter, GameState.isPlaying && Time.time < movingAnimationUntil);
        if (usesArchetypeAnimations) {
            bool attacking = IsSpawned ? networkIsAttacking.Value : localAttack;
            visualAnimator.SetBool(IsAttackingParameter, attacking);
        }
    }

    private void RefreshVisibilityState() {
        if (!disableWhenOutOfView) {
            if (visualsHiddenByCamera) {
                SetVisualsHidden(false);
            }
            return;
        }

        if (Time.time < nextVisibilityCheckTime) return;
        nextVisibilityCheckTime = Time.time + visibilityCheckInterval;

        if (mainCamera == null) {
            mainCamera = Camera.main;
            if (mainCamera == null) {
                return;
            }
        }

        Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        Bounds visibilityBounds = new Bounds(
            transform.position + Vector3.up * (visibilityBoundsSize.y * 0.5f),
            visibilityBoundsSize
        );
        bool isVisible = GeometryUtility.TestPlanesAABB(cameraPlanes, visibilityBounds);

        if (isVisible == visualsHiddenByCamera) {
            SetVisualsHidden(!isVisible);
        }
    }

    private void ResolvePlayerTarget() {
        if (Time.time < nextTargetSearchTime && (playerTarget == null || IsValidTarget(playerTarget))) return;
        nextTargetSearchTime = Time.time + Mathf.Max(0.05f, targetRefreshInterval);

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        PlayerController closestTarget = null;
        float closestDistanceSquared = float.MaxValue;

        foreach (PlayerController player in players) {
            if (!IsValidTarget(player)) {
                continue;
            }

            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared >= closestDistanceSquared) {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closestTarget = player;
        }

        playerTarget = closestTarget;
    }

    private static bool IsValidTarget(PlayerController player) {
        return player != null && player.isActiveAndEnabled && player.IsSpawned && player.IsAlive;
    }

    private void UpdatePatrol() {
        if (patrolPauseTimer > 0f) {
            patrolPauseTimer -= Time.deltaTime;
            MoveCharacter(Vector3.zero, 0f);
            return;
        }

        Vector3 toPatrolPoint = currentPatrolPoint - transform.position;
        toPatrolPoint.y = 0f;

        if (toPatrolPoint.sqrMagnitude <= patrolPointReachedDistance * patrolPointReachedDistance) {
            patrolPauseTimer = patrolPauseDuration;
            PickNextPatrolPoint();
            MoveCharacter(Vector3.zero, 0f);
            return;
        }

        MoveCharacter(toPatrolPoint.normalized, patrolSpeed);
    }

    private void UpdateChase() {
        Vector3 toPlayer = playerTarget.transform.position - transform.position;
        toPlayer.y = 0f;

        if (attackController != null && attackController.IsAttacking) {
            MoveCharacter(Vector3.zero, 0f);
            return;
        }

        if (attackController != null && attackController.CanReachTarget(playerTarget)) {
            MoveCharacter(Vector3.zero, 0f);
            attackController.TryAttack(playerTarget);
            return;
        }

        if (toPlayer.sqrMagnitude > 0.01f) {
            MoveCharacter(toPlayer.normalized, chaseSpeed);
        } else {
            MoveCharacter(Vector3.zero, 0f);
        }
    }

    public void ApplyKnockback(Vector3 attackerPosition) {
        Vector3 direction = transform.position - attackerPosition;
        direction.y = 0f;
        knockbackDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : -transform.forward;
        knockbackDirection.y = 0f;
        knockbackDirection.Normalize();

        float totalDistance = Mathf.Max(0f, knockbackDistance);
        float totalDuration = Mathf.Max(0.01f, knockbackDuration);
        float distanceShare = Mathf.Clamp(firstStageDistanceShare, 0.05f, 0.95f);
        float timeShare = Mathf.Clamp(firstStageTimeShare, 0.05f, 0.95f);
        knockbackStage = 1;
        stageRemainingDistance = totalDistance * distanceShare;
        stageRemainingTime = totalDuration * timeShare;
        secondStageDistance = totalDistance - stageRemainingDistance;
        secondStageDuration = totalDuration - stageRemainingTime;

        if (firstStageLiftHeight > 0f) {
            verticalVelocity = Mathf.Sqrt(
                2f * Mathf.Max(0.01f, -gravity) * firstStageLiftHeight
            );
        }
    }

    private void UpdateKnockback(float deltaTime) {
        float remainingFrameTime = deltaTime;
        float horizontalDistance = 0f;

        while (knockbackStage != 0 && remainingFrameTime > 0f) {
            float stepTime = Mathf.Min(remainingFrameTime, stageRemainingTime);
            float stepDistance = stageRemainingDistance * stepTime / stageRemainingTime;
            horizontalDistance += stepDistance;
            stageRemainingDistance -= stepDistance;
            stageRemainingTime -= stepTime;
            remainingFrameTime -= stepTime;

            if (stageRemainingTime > 0.00001f) {
                break;
            }

            if (knockbackStage == 1) {
                knockbackStage = 2;
                stageRemainingDistance = secondStageDistance;
                stageRemainingTime = secondStageDuration;
            } else {
                knockbackStage = 0;
            }
        }

        horizontalDistance = Mathf.Min(horizontalDistance, GetObstacleDistance(knockbackDirection, horizontalDistance));
        if (horizontalDistance <= 0.001f) knockbackStage = 0;
        if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        float verticalDistance = verticalVelocity * deltaTime +
            0.5f * gravity * deltaTime * deltaTime;
        verticalVelocity += gravity * deltaTime;
        float previousStepOffset = characterController.stepOffset;
        characterController.stepOffset = 0f;
        CollisionFlags collisions = characterController.Move(
            knockbackDirection * horizontalDistance + Vector3.up * verticalDistance
        );
        characterController.stepOffset = previousStepOffset;
        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f) {
            verticalVelocity = -2f;
        } else if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) {
            verticalVelocity = 0f;
        }
    }

    private void PickNextPatrolPoint() {
        Vector2 randomOffset = Random.insideUnitCircle * patrolRadius;
        currentPatrolPoint = patrolCenter + new Vector3(randomOffset.x, 0f, randomOffset.y);
    }

    private void SetVisualsHidden(bool shouldHide) {
        visualsHiddenByCamera = shouldHide;

        for (int i = 0; i < cachedRenderers.Length; i++) {
            if (cachedRenderers[i] != null) {
                cachedRenderers[i].enabled = shouldHide ? false : rendererEnabledStates[i];
            }
        }
    }

    private void MoveCharacter(Vector3 direction, float moveSpeed) {
        if (characterController.isGrounded && verticalVelocity < 0f) {
            verticalVelocity = -2f;
        }

        direction = AvoidObstacles(direction, moveSpeed);
        if (playerTarget == null) UpdateFacing(direction);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = direction * moveSpeed;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private Vector3 AvoidObstacles(Vector3 direction, float speed) {
        if (speed <= 0f || direction.sqrMagnitude < 0.0001f) return direction;
        float lookAhead = Mathf.Max(0.6f, speed * 0.3f);
        if (GetObstacleDistance(direction, lookAhead) >= lookAhead) return direction;

        if (Time.time >= avoidanceUntil) {
            Vector3 left = Quaternion.AngleAxis(-65f, Vector3.up) * direction;
            Vector3 right = Quaternion.AngleAxis(65f, Vector3.up) * direction;
            avoidanceSide = GetObstacleDistance(left, lookAhead) > GetObstacleDistance(right, lookAhead) ? -1f : 1f;
            avoidanceUntil = Time.time + 0.8f;
        }
        for (int angle = 45; angle <= 135; angle += 30) {
            Vector3 candidate = Quaternion.AngleAxis(angle * avoidanceSide, Vector3.up) * direction;
            if (GetObstacleDistance(candidate, lookAhead) >= lookAhead) return candidate;
        }
        return Vector3.zero;
    }

    private float GetObstacleDistance(Vector3 direction, float distance) {
        if (distance <= 0f) return 0f;
        Vector3 scale = transform.lossyScale;
        float radius = characterController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float halfHeight = Mathf.Max(radius, characterController.height * Mathf.Abs(scale.y) * 0.5f);
        Vector3 center = transform.TransformPoint(characterController.center);
        Vector3 capOffset = Vector3.up * (halfHeight - radius);
        float skin = Mathf.Max(0.02f, characterController.skinWidth);
        float nearest = distance;
        foreach (RaycastHit hit in Physics.CapsuleCastAll(center + capOffset, center - capOffset,
                     Mathf.Max(0.01f, radius - skin), direction, distance + skin, ~0, QueryTriggerInteraction.Ignore)) {
            if (!GroundPlacement.IsEnvironment(hit.collider) || hit.normal.y > 0.9f) continue;
            nearest = Mathf.Min(nearest, Mathf.Max(0f, hit.distance - skin));
        }
        return nearest;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
}
