using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class EnemyController : MonoBehaviour {
    private enum EnemyState {
        Patrol,
        Chase,
        TelegraphAttack,
        AttackRecover,
        Cooldown
    }

    private enum EnemyControlMode {
        Navigation,
        Combat,
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

    [Header("Combat")]
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackTelegraphDuration = 0.7f;
    [SerializeField] private float attackRecoverDuration = 0.8f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private Color attackTelegraphColor = new Color(1f, 0.7f, 0.7f, 0.45f);

    [Header("Movement")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float gravity = -20f;

    [Header("Camera Culling")]
    [SerializeField] private bool disableWhenOutOfView = true;
    [SerializeField] private Vector3 visibilityBoundsSize = new Vector3(2f, 3f, 2f);
    [SerializeField] private float visibilityCheckInterval = 0.2f;

    private CharacterController characterController;
    private Camera mainCamera;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private bool[] rendererEnabledStates;
    private bool[] colliderEnabledStates;
    private Transform playerTarget;
    private EnemyState currentState = EnemyState.Patrol;
    private EnemyControlMode currentControlMode = EnemyControlMode.Navigation;
    private Vector3 patrolCenter;
    private Vector3 currentPatrolPoint;
    private float verticalVelocity;
    private float chaseTimer;
    private float cooldownTimer;
    private float patrolPauseTimer;
    private float nextTargetSearchTime;
    private float nextVisibilityCheckTime;
    private float attackStateTimer;
    private bool isSuspendedByCamera;
    private AttackTelegraphRenderer telegraphRenderer;

    public bool IsInCombatMode => currentControlMode == EnemyControlMode.Combat;

    private void Awake() {
        characterController = GetComponent<CharacterController>();
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

        telegraphRenderer = GetComponent<AttackTelegraphRenderer>();
        if (telegraphRenderer == null) {
            telegraphRenderer = gameObject.AddComponent<AttackTelegraphRenderer>();
        }
    }

    private void Update() {
        RefreshVisibilityState();

        if (isSuspendedByCamera) return;
        if (!GameState.isPlaying) return;

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

            case EnemyState.TelegraphAttack:
                UpdateAttackTelegraph();
                break;

            case EnemyState.AttackRecover:
                UpdateAttackRecover();
                break;

            case EnemyState.Cooldown:
                UpdateCooldown();
                UpdatePatrol();
                break;
        }
    }

    public void SetCombatMode(bool shouldEnterCombatMode) {
        if (shouldEnterCombatMode) {
            if (currentState == EnemyState.Patrol || currentState == EnemyState.Cooldown) {
                BeginChase();
            }

            return;
        }

        BeginCooldown();
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
        if (playerTarget != null) return;
        if (Time.time < nextTargetSearchTime) return;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null) {
            playerTarget = taggedPlayer.transform;
            return;
        }

        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null) {
            playerTarget = playerController.transform;
            return;
        }

        nextTargetSearchTime = Time.time + 0.5f;
    }

    private bool CanStartChase() {
        if (playerTarget == null) return false;
        return GetHorizontalDistanceTo(playerTarget.position) <= detectionRange;
    }

    private void BeginChase() {
        currentState = EnemyState.Chase;
        currentControlMode = EnemyControlMode.Combat;
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

        if (toPlayer.sqrMagnitude > 0.01f) {
            MoveCharacter(toPlayer.normalized, chaseSpeed);
        } else {
            MoveCharacter(Vector3.zero, 0f);
        }

        if (toPlayer.sqrMagnitude <= attackRange * attackRange) {
            BeginAttackTelegraph();
            return;
        }

        if (chaseTimer <= 0f) {
            BeginCooldown();
        }
    }

    private void BeginAttackTelegraph() {
        currentState = EnemyState.TelegraphAttack;
        currentControlMode = EnemyControlMode.Combat;
        attackStateTimer = attackTelegraphDuration;

        if (telegraphRenderer != null) {
            telegraphRenderer.ShowTelegraph(
                attackRange,
                attackTelegraphDuration,
                attackTelegraphColor
            );
        }
    }

    private void UpdateAttackTelegraph() {
        MoveCharacter(Vector3.zero, 0f);
        attackStateTimer -= Time.deltaTime;

        if (attackStateTimer > 0f) {
            return;
        }

        ResolveAttack();
        BeginAttackRecover();
    }

    private void BeginAttackRecover() {
        currentState = EnemyState.AttackRecover;
        currentControlMode = EnemyControlMode.Combat;
        attackStateTimer = attackRecoverDuration;

        if (telegraphRenderer != null) {
            telegraphRenderer.HideTelegraph();
        }
    }

    private void UpdateAttackRecover() {
        MoveCharacter(Vector3.zero, 0f);
        attackStateTimer -= Time.deltaTime;

        if (attackStateTimer > 0f) {
            return;
        }

        if (CanStartChase()) {
            BeginChase();
            return;
        }

        BeginCooldown();
    }

    private void ResolveAttack() {
        if (playerTarget == null) {
            return;
        }

        if (GetHorizontalDistanceTo(playerTarget.position) > attackRange) {
            return;
        }

        playerTarget.SendMessage(
            "OnEnemyAttackResolved",
            attackDamage,
            SendMessageOptions.DontRequireReceiver
        );

        if (telegraphRenderer != null) {
            telegraphRenderer.HideTelegraph();
        }
    }

    private void UpdateCooldown() {
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f) {
            currentState = EnemyState.Patrol;
            currentControlMode = EnemyControlMode.Navigation;
        }
    }

    private void BeginCooldown() {
        currentState = EnemyState.Cooldown;
        currentControlMode = EnemyControlMode.Cooldown;
        cooldownTimer = chaseCooldown;
        patrolPauseTimer = 0f;
        PickNextPatrolPoint();

        if (telegraphRenderer != null) {
            telegraphRenderer.HideTelegraph();
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

        Gizmos.color = new Color(1f, 0.7f, 0.7f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
}
