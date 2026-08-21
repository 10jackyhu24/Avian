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
    [SerializeField] private float unarmedRadiusWidthMultiplier = 2f;
    // 找不到 CharacterController / CapsuleCollider 時使用的固定半徑。
    [SerializeField] private float unarmedRadiusFallback = 1.2f;

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
        float radius = GetUnarmedRadius();

        SpawnPunchEffectClientRpc(GetRandomPointInAttackCircle(radius));
        ShowWarningClientRpc(originPoint.position, radius);
        yield return new WaitForSeconds(currentAttack.warningDuration);

        if (!visualTestOnly) {
            ApplyDamage(Physics.OverlapSphere(originPoint.position, radius, targetLayer, QueryTriggerInteraction.Ignore));
        }
        HideWarningClientRpc();
        IsAttacking.Value = false;
    }

    private Vector3 GetRandomPointInAttackCircle(float radius) {
        Vector2 randomOffset = Random.insideUnitCircle * radius;
        return originPoint.position + new Vector3(randomOffset.x, 0.5f, randomOffset.y);
    }

    private float GetUnarmedRadius() {
        // 【無武器攻擊圓圈半徑計算】角色寬度 = Collider radius × 2。
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null && controller.radius > 0f) return controller.radius * 2f * unarmedRadiusWidthMultiplier;
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null && capsule.radius > 0f) return capsule.radius * 1.25f * unarmedRadiusWidthMultiplier;
        return unarmedRadiusFallback;
    }

    private void ApplyDamage(Collider[] hits) {
        Health ownHealth = GetComponent<Health>();
        foreach (Collider hit in hits) {
            Health health = hit.GetComponentInParent<Health>();
            if (health != null && health != ownHealth) {
                health.TakeDamage(currentAttack.damage);
            }
        }
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
    private void SpawnPunchEffectClientRpc(Vector3 position) {
        if (punchEffectPrefab == null) {
            Debug.LogWarning("CombatController: Punch Effect Prefab is not assigned.", this);
            return;
        }

        GameObject effect = Instantiate(punchEffectPrefab, position, Quaternion.identity);
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
