using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>Server-authoritative attacks for unarmed melee, weapon melee, and weapon ranged modes.</summary>
public class CombatController : NetworkBehaviour{
    public enum AttackMode { UnarmedMelee, WeaponMelee, WeaponRanged }

    [Header("Attack Mode")]
    [SerializeField] private AttackMode attackMode = AttackMode.UnarmedMelee;
    [SerializeField] private bool requireCombatState;

    [Header("Shared Attack Settings")]
    [FormerlySerializedAs("CurrentAttack")]
    [SerializeField] private AttackData currentAttack;
    [FormerlySerializedAs("OriginPoint")]
    [SerializeField] private Transform originPoint;
    [FormerlySerializedAs("TargetLayer")]
    [SerializeField] private LayerMask targetLayer;

    [Header("Unarmed Melee")]
    // 【無武器攻擊圓圈半徑設定】半徑 = 角色寬度 × 此倍率。
    [Tooltip("Unarmed radius = character width x this value.")]
    [SerializeField] private float unarmedRadiusWidthMultiplier = 1.8f;
    // 【無武器安全內圈】半徑 = 角色自身寬度 × 此倍率。此區域不會生成拳頭，也不會造成傷害。
    [Tooltip("Unarmed inner safe radius = character width x this value.")]
    [SerializeField] private float unarmedInnerRadiusWidthMultiplier = 1f;
    // 找不到 CharacterController / CapsuleCollider 時使用的固定半徑。
    [SerializeField] private float unarmedRadiusFallback = 1.4f;

    [Header("Unarmed Melee Visual Test")]
    [SerializeField] private bool visualTestOnly = true;
    [SerializeField] private GameObject punchEffectPrefab;
    [SerializeField] private float punchEffectLifetime = 0.3f;

    [Header("Weapon Melee (Reserved)")]
    [SerializeField] private Transform weaponPivot;
    [Range(40f, 70f)] [SerializeField] private float weaponSwingAngle = 55f;
    [SerializeField] private float weaponMeleeRadius = 2.5f;

    [Header("Weapon Ranged (Reserved)")]
    [SerializeField] private Transform projectileSpawnPoint;

    public NetworkVariable<bool> IsCombating = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsAttacking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private GameInput inputActions;
    private float nextAttackTime;

    private void Awake() {
        inputActions = new GameInput();
        KeyBinding.LoadBindingOverrides(inputActions);
        if (originPoint == null) originPoint = transform;
    }

    public override void OnNetworkSpawn() {
        if (IsOwner) inputActions.Player.Enable();
    }

    public override void OnNetworkDespawn() {
        inputActions.Player.Disable();
    }

    private void Update() {
        // Uses the Attack Input Action, so OptionsMenu rebinding is respected.
        if (!IsOwner || !IsClient) return;
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null && !controller.isGrounded) return;
        if (inputActions.Player.Attack.WasPressedThisFrame()) RequestAttack();
    }

    public void EntryCombat() {
        if (IsServer) IsCombating.Value = true;
    }
    public void ExitCombat() {
        if (IsServer) IsCombating.Value = false;
    }
    public void RequestAttack() {
        if (IsOwner) RequestAttackServerRpc();
    }

    // Compatibility for existing UnityEvent/script references using the old spelling.
    public void RequestAttact() => RequestAttack();

    [ServerRpc]
    private void RequestAttackServerRpc() {
        if ((requireCombatState && !IsCombating.Value) || IsAttacking.Value || Time.time < nextAttackTime) return;
        if (currentAttack == null) {
            Debug.LogWarning("CombatController: Current Attack is not assigned.", this);
            return;
        }

        switch (attackMode) {
            case AttackMode.UnarmedMelee: StartCoroutine(UnarmedMeleeAttackRoutine()); break;
            case AttackMode.WeaponMelee: StartWeaponMeleeAttack(); break;
            case AttackMode.WeaponRanged: StartWeaponRangedAttack(); break;
        }
    }

    private IEnumerator UnarmedMeleeAttackRoutine() {
        IsAttacking.Value = true;
        nextAttackTime = Time.time + currentAttack.cooldown;
        float outerRadius = GetUnarmedRadius();
        float innerRadius = GetUnarmedInnerRadius(outerRadius);

        // Send a local offset so the effect remains at the same relative position while the player moves.
        SpawnPunchEffectClientRpc(GetRandomPointInAttackRing(outerRadius, innerRadius));
        ShowWarningClientRpc(originPoint.position, outerRadius);
        yield return new WaitForSeconds(currentAttack.warningDuration);

        if (!visualTestOnly) {
            ApplyDamage(
                Physics.OverlapSphere(originPoint.position, outerRadius, targetLayer, QueryTriggerInteraction.Ignore),
                innerRadius,
                outerRadius
            );
        }
        HideWarningClientRpc();
        IsAttacking.Value = false;
    }

    private Vector3 GetRandomPointInAttackRing(float outerRadius, float innerRadius) {
        // sqrt keeps points evenly distributed across the area of the ring instead of clustering at the inner edge.
        float distance = Mathf.Sqrt(Random.Range(innerRadius * innerRadius, outerRadius * outerRadius));
        Vector2 offset = Random.insideUnitCircle.normalized * distance;
        Vector3 worldPosition = originPoint.position + new Vector3(offset.x, 0.2f, offset.y);
        return transform.InverseTransformPoint(worldPosition);
    }

    private float GetUnarmedRadius() {
        return GetCharacterWidth() * unarmedRadiusWidthMultiplier;
    }

    private float GetUnarmedInnerRadius(float outerRadius) {
        return Mathf.Min(GetCharacterWidth() * unarmedInnerRadiusWidthMultiplier, outerRadius);
    }

    private float GetCharacterWidth() {
        // 【角色寬度計算】角色寬度 = Collider radius × 2。
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null && controller.radius > 0f) return controller.radius * 2f;
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null && capsule.radius > 0f) return capsule.radius * 2f;
        return unarmedRadiusFallback / Mathf.Max(unarmedRadiusWidthMultiplier, Mathf.Epsilon);
    }

    private void ApplyDamage(Collider[] hits, float innerRadius, float outerRadius) {
        Health ownHealth = GetComponent<Health>();
        foreach (Collider hit in hits) {
            if (!IsInsideDamageRing(hit, innerRadius, outerRadius)) continue;

            Health health = hit.GetComponentInParent<Health>();
            if (health != null && health != ownHealth) {
                health.TakeDamage(currentAttack.damage);
            }
        }
    }

    private bool IsInsideDamageRing(Collider hit, float innerRadius, float outerRadius) {
        Vector3 closestPoint = hit.ClosestPoint(originPoint.position);
        Vector3 horizontalOffset = closestPoint - originPoint.position;
        horizontalOffset.y = 0f;
        float distanceSquared = horizontalOffset.sqrMagnitude;
        return distanceSquared >= innerRadius * innerRadius && distanceSquared <= outerRadius * outerRadius;
    }

    // Reserved: overlap a circle around weaponPivot, then retain only targets in a 40-70 degree sector.
    private void StartWeaponMeleeAttack() {
        Debug.Log("Weapon melee attack is reserved for the weapon swing implementation.", this);
    }

    // Reserved: obtain mouse world position and spawn an arrow from projectileSpawnPoint on the server.
    private void StartWeaponRangedAttack() {
        Debug.Log("Weapon ranged attack is reserved for the bow/projectile implementation.", this);
    }

    [ClientRpc]
    private void SpawnPunchEffectClientRpc(Vector3 localOffset) {
        if (punchEffectPrefab == null) {
            Debug.LogWarning("CombatController: Punch Effect Prefab is not assigned.", this);
            return;
        }

        // Parenting the effect makes its animation follow this network player's movement on every client.
        GameObject effect = Instantiate(punchEffectPrefab, transform);
        effect.transform.localPosition = localOffset;
        effect.transform.localRotation = Quaternion.identity;
        Destroy(effect, punchEffectLifetime);
    }

    [ClientRpc]
    private void ShowWarningClientRpc(Vector3 position, float radius) {
        Debug.Log($"Attack warning shown at {position} with radius {radius}.");
    }

    [ClientRpc]
    private void HideWarningClientRpc() {
        Debug.Log("Attack warning hidden.");
    }
}
