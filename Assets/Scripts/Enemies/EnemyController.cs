using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class EnemyController : MonoBehaviour {
    private enum EnemyState {
        Patrol,
        Chase,
        Cooldown
    }

    [Header("Patrol")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float patrolRadius = 8f;
    [SerializeField] private float patrolPointReachedDistance = 0.75f;
    [SerializeField] private float patrolPauseDuration = 0.75f;

    [Header("Chase")]
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float chaseDuration = 5f;
    [SerializeField] private float chaseCooldown = 3f;
    [SerializeField] private float targetRefreshInterval = 0.25f;

    [Header("Movement")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float gravity = -20f;

    [Header("Hit Reaction")]
    [Min(0f)] [SerializeField] private float knockbackDistance = 0.8f;
    [Min(0.01f)] [SerializeField] private float knockbackDuration = 0.2f;
    [Range(0.05f, 0.95f)] [SerializeField] private float firstStageDistanceShare = 0.6f;
    [Range(0.05f, 0.95f)] [SerializeField] private float firstStageTimeShare = 0.35f;
    [Min(0f)] [SerializeField] private float firstStageLiftHeight = 0.25f;

    [Header("Camera Culling")]
    [SerializeField] private bool disableWhenOutOfView = true;
    [SerializeField] private Vector3 visibilityBoundsSize = new Vector3(2f, 3f, 2f);
    [SerializeField] private float visibilityCheckInterval = 0.2f;

    private CharacterController characterController;
    private EnemyAttackController attackController;
    private Camera mainCamera;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private bool[] rendererEnabledStates;
    private bool[] colliderEnabledStates;
    private Transform playerTarget;
    private EnemyState currentState = EnemyState.Patrol;
    private Vector3 patrolCenter;
    private Vector3 currentPatrolPoint;
    private float verticalVelocity;
    private float chaseTimer;
    private float cooldownTimer;
    private float patrolPauseTimer;
    private float nextTargetSearchTime;
    private float nextVisibilityCheckTime;
    private bool isSuspendedByCamera;
    private Vector3 knockbackDirection;
    private int knockbackStage;
    private float stageRemainingDistance;
    private float stageRemainingTime;
    private float secondStageDistance;
    private float secondStageDuration;

    private void Awake() {
        characterController = GetComponent<CharacterController>();
        attackController = GetComponent<EnemyAttackController>();
        mainCamera = Camera.main;
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
        rendererEnabledStates = new bool[cachedRenderers.Length];
        colliderEnabledStates = new bool[cachedColliders.Length];

        for (int i = 0; i < cachedRenderers.Length; i++) {
            rendererEnabledStates[i] = cachedRenderers[i].enabled;
        }

        for (int i = 0; i < cachedColliders.Length; i++) {
            colliderEnabledStates[i] = cachedColliders[i].enabled;
        }

        patrolCenter = transform.position;
        currentPatrolPoint = patrolCenter;
        PickNextPatrolPoint();
    }

    private void Update() {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer) {
            return;
        }

        RefreshVisibilityState();

        if (isSuspendedByCamera) return;
        if (!GameState.isPlaying) return;

        if (knockbackStage != 0 && Time.deltaTime > 0f) {
            UpdateKnockback(Time.deltaTime);
            return;
        }

        ResolvePlayerTarget();

        switch (currentState) {
            case EnemyState.Patrol:
                if (CanStartChase()) {
                    BeginChase();
                    UpdateChase();
                    return;
                }

                UpdatePatrol();
                break;

            case EnemyState.Chase:
                UpdateChase();
                break;

            case EnemyState.Cooldown:
                UpdateCooldown();
                UpdatePatrol();
                break;
        }
    }

    private void RefreshVisibilityState() {
        /*if (!disableWhenOutOfView) {
            if (isSuspendedByCamera) {
                SetCameraSuspended(false);
            }
            return;
        }*/

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

        if (isVisible == isSuspendedByCamera) {
            SetCameraSuspended(!isVisible);
        }
    }

    private void ResolvePlayerTarget() {
        if (Time.time < nextTargetSearchTime) return;
        nextTargetSearchTime = Time.time + targetRefreshInterval;

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Transform closestTarget = null;
        float closestDistanceSquared = float.MaxValue;

        foreach (PlayerController player in players) {
            if (player == null || !player.IsSpawned || !player.IsAlive) {
                continue;
            }

            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared >= closestDistanceSquared) {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closestTarget = player.transform;
        }

        playerTarget = closestTarget;
    }

    private bool CanStartChase() {
        if (playerTarget == null) return false;
        return GetHorizontalDistanceTo(playerTarget.position) <= detectionRange;
    }

    private void BeginChase() {
        currentState = EnemyState.Chase;
        chaseTimer = chaseDuration;
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
        if (playerTarget == null) {
            BeginCooldown();
            return;
        }

        chaseTimer -= Time.deltaTime;

        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f;

        if (attackController != null && attackController.IsAttacking) {
            MoveCharacter(Vector3.zero, 0f);
            return;
        }

        PlayerController targetPlayer = playerTarget.GetComponent<PlayerController>();
        if (attackController != null && targetPlayer != null &&
            toPlayer.magnitude <= attackController.AttackRange) {
            MoveCharacter(Vector3.zero, 0f);
            attackController.TryAttack(targetPlayer);
            if (chaseTimer <= 0f) {
                BeginCooldown();
            }
            return;
        }

        if (toPlayer.sqrMagnitude > 0.01f) {
            MoveCharacter(toPlayer.normalized, chaseSpeed);
        } else {
            MoveCharacter(Vector3.zero, 0f);
        }

        if (chaseTimer <= 0f) {
            BeginCooldown();
        }
    }

    private void UpdateCooldown() {
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f) {
            currentState = EnemyState.Patrol;
        }
    }

    private void BeginCooldown() {
        currentState = EnemyState.Cooldown;
        cooldownTimer = chaseCooldown;
        patrolPauseTimer = 0f;
        PickNextPatrolPoint();
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

        float verticalDistance = verticalVelocity * deltaTime +
            0.5f * gravity * deltaTime * deltaTime;
        verticalVelocity += gravity * deltaTime;
        CollisionFlags collisions = characterController.Move(
            knockbackDirection * horizontalDistance + Vector3.up * verticalDistance
        );
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

    private void SetCameraSuspended(bool shouldSuspend) {
        isSuspendedByCamera = shouldSuspend;

        if (characterController != null) {
            characterController.enabled = !shouldSuspend;
        }

        for (int i = 0; i < cachedRenderers.Length; i++) {
            if (cachedRenderers[i] != null) {
                cachedRenderers[i].enabled = shouldSuspend ? false : rendererEnabledStates[i];
            }
        }

        for (int i = 0; i < cachedColliders.Length; i++) {
            Collider collider = cachedColliders[i];
            if (collider == null || collider == characterController) {
                continue;
            }

            collider.enabled = shouldSuspend ? false : colliderEnabledStates[i];
        }
    }

    private void MoveCharacter(Vector3 direction, float moveSpeed) {
        if (characterController.isGrounded && verticalVelocity < 0f) {
            verticalVelocity = -2f;
        }

        if (direction.sqrMagnitude > 0.001f) {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = direction * moveSpeed;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private float GetHorizontalDistanceTo(Vector3 targetPosition) {
        Vector3 offset = targetPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
}
