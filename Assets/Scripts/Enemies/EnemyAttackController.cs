using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAttackController : NetworkBehaviour {
    [Header("Attack")]
    [SerializeField] private AttackData attackData;
    [Min(0f)] [SerializeField] private float defaultDamage = 10f;
    [Min(0f)] [SerializeField] private float defaultRadius = 2f;
    [Min(0f)] [SerializeField] private float defaultCooldown = 1f;
    [Min(0f)] [SerializeField] private float defaultWindup = 0.3f;
    [Min(0f)] [SerializeField] private float hitStunDuration = 0.4f;
    [SerializeField] private Vector3 defaultCenterOffset = new Vector3(0f, 1f, 0f);

    [Header("Windup Warning")]
    [SerializeField] private Color warningColor = new Color(1f, 0.15f, 0.05f, 0.35f);
    [Range(8, 64)] [SerializeField] private int warningSegments = 32;
    [Min(0.01f)] [SerializeField] private float warningHeight = 0.12f;

    private float nextAttackTime;
    private float stunnedUntil;
    private Coroutine attackRoutine;
    private GameObject warningObject;
    private Mesh warningMesh;
    private Material warningMaterial;

    public float AttackRange => attackData != null ? Mathf.Max(0f, attackData.radius) : defaultRadius;
    public bool IsAttacking => attackRoutine != null;

    public bool TryAttack(PlayerController target) {
        if (target == null || !target.IsSpawned || !target.IsAlive ||
            !GameState.isPlaying || !CanAttackOnServer() || attackRoutine != null ||
            Time.time < nextAttackTime || Time.time < stunnedUntil) {
            return false;
        }

        AttackMode mode = attackData != null ? attackData.attackmode : AttackMode.UnarmedMelee;
        if (mode == AttackMode.WeaponRanged) {
            return TryRangedAttack(target);
        }

        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) {
            direction = transform.forward;
        }
        direction.Normalize();

        Vector3 center = transform.position + (attackData != null ? attackData.centerOffset : defaultCenterOffset);
        float radius = AttackRange;
        if (radius <= 0f || !IsInsideAttackArea(target.GetComponent<CharacterController>(), center, direction, radius)) {
            return false;
        }

        nextAttackTime = Time.time + Mathf.Max(0f, attackData != null ? attackData.cooldown : defaultCooldown);
        attackRoutine = StartCoroutine(MeleeAttackRoutine(target, direction));
        ShowWarning(direction);
        return true;
    }

    private IEnumerator MeleeAttackRoutine(PlayerController target, Vector3 direction) {
        float windup = Mathf.Max(0f, attackData != null ? attackData.warningDuration : defaultWindup);
        if (windup > 0f) {
            yield return new WaitForSeconds(windup);
        } else {
            yield return null;
        }

        HideWarning();
        attackRoutine = null;
        if (target == null || !target.IsSpawned || !target.IsAlive || !GameState.isPlaying ||
            !CanAttackOnServer() || Time.time < stunnedUntil) {
            yield break;
        }

        Vector3 center = transform.position + (attackData != null ? attackData.centerOffset : defaultCenterOffset);
        float radius = AttackRange;
        if (!IsInsideAttackArea(target.GetComponent<CharacterController>(), center, direction, radius)) {
            yield break;
        }

        ApplyMeleeDamage(center, direction, radius);
        PlayMeleeEffect(center, direction);
    }

    public void InterruptAttack() {
        if (!CanAttackOnServer()) {
            return;
        }

        if (attackRoutine != null) {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        HideWarning();
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + hitStunDuration);
    }

    private void LateUpdate() {
        if (warningObject == null) {
            return;
        }

        Vector3 center = transform.position + (attackData != null ? attackData.centerOffset : defaultCenterOffset);
        center.y = transform.position.y + warningHeight;
        warningObject.transform.position = center;
    }

    private void OnDisable() {
        if (attackRoutine != null) {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        HideWarningLocally();
    }

    public override void OnNetworkDespawn() {
        HideWarningLocally();
        base.OnNetworkDespawn();
    }

    private void ShowWarning(Vector3 direction) {
        if (IsSpawned) {
            ShowWarningClientRpc(direction);
        } else {
            ShowWarningLocally(direction);
        }
    }

    private void HideWarning() {
        if (IsSpawned) {
            HideWarningClientRpc();
        } else {
            HideWarningLocally();
        }
    }

    [ClientRpc]
    private void ShowWarningClientRpc(Vector3 direction) {
        ShowWarningLocally(direction);
    }

    [ClientRpc]
    private void HideWarningClientRpc() {
        HideWarningLocally();
    }

    private void ShowWarningLocally(Vector3 direction) {
        HideWarningLocally();
        float radius = AttackRange;
        if (radius <= 0f) {
            return;
        }

        float arc = attackData != null && attackData.attackShape == AttackShape.Sector
            ? Mathf.Clamp(attackData.sectorAngle, 1f, 360f)
            : 360f;
        int segments = Mathf.Max(2, Mathf.CeilToInt(warningSegments * arc / 360f));
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        for (int i = 0; i <= segments; i++) {
            float angle = (-arc * 0.5f + arc * i / segments) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (i == segments) {
                continue;
            }
            int index = i * 3;
            triangles[index] = 0;
            triangles[index + 1] = i + 2;
            triangles[index + 2] = i + 1;
        }

        warningMesh = new Mesh { name = "Enemy Attack Warning Mesh" };
        warningMesh.vertices = vertices;
        warningMesh.triangles = triangles;
        warningMesh.RecalculateBounds();

        warningObject = new GameObject("Enemy Attack Warning") { hideFlags = HideFlags.DontSave };
        warningObject.AddComponent<MeshFilter>().sharedMesh = warningMesh;
        MeshRenderer renderer = warningObject.AddComponent<MeshRenderer>();
        renderer.sortingOrder = 9;

        Shader shader = Resources.Load<Shader>("AttackPreview");
        if (shader == null) shader = Shader.Find("Avian/AttackPreview");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        warningMaterial = new Material(shader) { color = warningColor, renderQueue = 3000 };
        warningMaterial.mainTexture = Texture2D.whiteTexture;
        renderer.sharedMaterial = warningMaterial;

        float yaw = -Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
        warningObject.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        LateUpdate();
    }

    private void HideWarningLocally() {
        if (warningObject != null) Destroy(warningObject);
        if (warningMesh != null) Destroy(warningMesh);
        if (warningMaterial != null) Destroy(warningMaterial);
        warningObject = null;
        warningMesh = null;
        warningMaterial = null;
    }

    private void ApplyMeleeDamage(Vector3 center, Vector3 direction, float radius) {
        LayerMask layer = attackData != null ? attackData.targetLayer : (LayerMask)~0;
        Collider[] hits = Physics.OverlapSphere(center, radius, layer, QueryTriggerInteraction.Ignore);
        HashSet<PlayerController> damagedPlayers = new HashSet<PlayerController>();

        foreach (Collider hit in hits) {
            if (hit == null || !IsInsideAttackArea(hit, center, direction, radius)) {
                continue;
            }

            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player == null || !player.IsSpawned || !player.IsAlive || !damagedPlayers.Add(player)) {
                continue;
            }

            player.TakeDamageFromEnemy(attackData != null ? attackData.damage : defaultDamage);
        }
    }

    private bool IsInsideAttackArea(Collider hit, Vector3 center, Vector3 direction, float radius) {
        if (hit == null) {
            return false;
        }

        Vector3 offset = hit.ClosestPoint(center) - center;
        offset.y = 0f;
        if (offset.sqrMagnitude > radius * radius) {
            return false;
        }

        return attackData == null || attackData.attackShape != AttackShape.Sector ||
               Vector3.Angle(direction, offset) <= attackData.sectorAngle * 0.5f;
    }

    private void PlayMeleeEffect(Vector3 center, Vector3 direction) {
        if (attackData == null || attackData.attackEffect == null) {
            return;
        }

        Vector3 position = center + direction * Mathf.Clamp(attackData.effectForwardOffset, 0f, AttackRange);
        if (IsSpawned) {
            PlayMeleeEffectClientRpc(position, direction.x < 0f);
        } else {
            SpawnMeleeEffect(position, direction.x < 0f);
        }
    }

    [ClientRpc]
    private void PlayMeleeEffectClientRpc(Vector3 position, bool facingLeft) {
        SpawnMeleeEffect(position, facingLeft);
    }

    private void SpawnMeleeEffect(Vector3 position, bool facingLeft) {
        GameObject effect = Instantiate(attackData.attackEffect, position, Quaternion.identity);
        effect.transform.localScale *= Mathf.Max(0.01f, attackData.effectScale);
        foreach (SpriteRenderer renderer in effect.GetComponentsInChildren<SpriteRenderer>()) {
            renderer.flipX = facingLeft;
        }
        Destroy(effect, Mathf.Max(0.01f, attackData.effectLifetime));
    }

    // Ranged attacks can use the same target selection and server authority later.
    private bool TryRangedAttack(PlayerController target) {
        return false;
    }

    private static bool CanAttackOnServer() {
        return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening ||
               NetworkManager.Singleton.IsServer;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position +
            (attackData != null ? attackData.centerOffset : defaultCenterOffset), AttackRange);
    }
}
