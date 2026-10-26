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
    private EnemyArchetypeStats archetypeStats;
    private readonly Dictionary<int, EnemyProjectileVisual> projectileVisuals = new Dictionary<int, EnemyProjectileVisual>();
    private int nextProjectileId;

    public float AttackRange => archetypeStats != null ? archetypeStats.AttackRange :
        attackData != null ? Mathf.Max(0f, attackData.radius) : defaultRadius;
    public bool IsAttacking => attackRoutine != null;

    public void ApplyArchetype(EnemyArchetypeStats stats) {
        archetypeStats = stats;
    }

    public bool TryAttack(PlayerController target) {
        if (target == null || !target.IsSpawned || !target.IsAlive ||
            !GameState.isPlaying || !CanAttackOnServer() || attackRoutine != null ||
            Time.time < nextAttackTime || Time.time < stunnedUntil) {
            return false;
        }

        if (archetypeStats != null && archetypeStats.Pattern == EnemyAttackPattern.Projectile) {
            return TryRangedAttack(target);
        }

        AttackMode mode = archetypeStats == null && attackData != null ? attackData.attackmode : AttackMode.UnarmedMelee;
        if (mode == AttackMode.WeaponRanged) {
            return TryRangedAttack(target);
        }

        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) {
            direction = transform.forward;
        }
        direction.Normalize();

        Vector3 center = GetAttackCenter();
        float radius = AttackRange;
        if (radius <= 0f || (archetypeStats == null || archetypeStats.Pattern != EnemyAttackPattern.Slam) &&
            !IsInsideAttackArea(target.GetComponent<CharacterController>(), center, direction, radius)) {
            return false;
        }

        nextAttackTime = Time.time + (archetypeStats != null ? archetypeStats.Cooldown :
            Mathf.Max(0f, attackData != null ? attackData.cooldown : defaultCooldown));
        attackRoutine = StartCoroutine(archetypeStats != null && archetypeStats.Pattern == EnemyAttackPattern.Charge
            ? ChargeAttackRoutine(direction) : MeleeAttackRoutine(target, direction));
        ShowWarning(direction);
        return true;
    }

    private IEnumerator MeleeAttackRoutine(PlayerController target, Vector3 direction) {
        float windup = GetWindup();
        if (windup > 0f) {
            yield return new WaitForSeconds(windup);
        } else {
            yield return null;
        }

        HideWarning();
        attackRoutine = null;
        if (target == null || !target.IsSpawned || !GameState.isPlaying ||
            !CanAttackOnServer() || Time.time < stunnedUntil) {
            yield break;
        }

        Vector3 center = GetAttackCenter();
        float radius = AttackRange;
        if (archetypeStats == null || archetypeStats.Pattern != EnemyAttackPattern.Slam) {
            if (!IsInsideAttackArea(target.GetComponent<CharacterController>(), center, direction, radius)) yield break;
        }

        ApplyMeleeDamage(center, direction, radius);
        PlayMeleeEffect(center, direction);
        if (archetypeStats != null && archetypeStats.Pattern == EnemyAttackPattern.Slam) {
            for (int i = 0; i < 8; i++) {
                float angle = i * Mathf.PI * 0.25f;
                LaunchProjectile(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)),
                    archetypeStats.Damage * 0.5f, archetypeStats.ProjectileSpeed, 8f);
            }
        }
    }

    private IEnumerator ChargeAttackRoutine(Vector3 direction) {
        yield return new WaitForSeconds(GetWindup());
        HideWarning();
        if (!GameState.isPlaying || !CanAttackOnServer() || Time.time < stunnedUntil) {
            attackRoutine = null;
            yield break;
        }

        CharacterController controller = GetComponent<CharacterController>();
        HashSet<PlayerController> hitPlayers = new HashSet<PlayerController>();
        float remaining = archetypeStats.ChargeDuration;
        while (remaining > 0f && GameState.isPlaying && controller != null && controller.enabled) {
            float step = Mathf.Min(Time.deltaTime, remaining);
            CollisionFlags collision = controller.Move(direction * archetypeStats.ChargeSpeed * step);
            DamageNearbyPlayers(GetAttackCenter(), controller.radius + 0.8f, archetypeStats.Damage, hitPlayers);
            if ((collision & CollisionFlags.Sides) != 0) break;
            remaining -= step;
            yield return null;
        }
        attackRoutine = null;
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

        Vector3 center = GetAttackCenter();
        center.y = transform.position.y + warningHeight;
        warningObject.transform.position = center;
    }

    private void OnDisable() {
        if (attackRoutine != null) {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        HideWarningLocally();
        ClearProjectileVisuals();
    }

    public override void OnNetworkDespawn() {
        HideWarningLocally();
        ClearProjectileVisuals();
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

        float arc = archetypeStats != null ? archetypeStats.SectorAngle :
            attackData != null && attackData.attackShape == AttackShape.Sector
                ? Mathf.Clamp(attackData.sectorAngle, 1f, 360f) : 360f;
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

            player.TakeDamageFromEnemy(archetypeStats != null ? archetypeStats.Damage :
                attackData != null ? attackData.damage : defaultDamage);
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

        if (archetypeStats != null) return archetypeStats.SectorAngle >= 360f ||
            Vector3.Angle(direction, offset) <= archetypeStats.SectorAngle * 0.5f;
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

    private bool TryRangedAttack(PlayerController target) {
        if (archetypeStats == null || target == null) return false;

        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f || direction.sqrMagnitude > AttackRange * AttackRange) return false;
        direction.Normalize();
        nextAttackTime = Time.time + archetypeStats.Cooldown;
        attackRoutine = StartCoroutine(RangedAttackRoutine(direction));
        ShowWarning(direction);
        return true;
    }

    private IEnumerator RangedAttackRoutine(Vector3 direction) {
        yield return new WaitForSeconds(GetWindup());
        HideWarning();
        if (GameState.isPlaying && CanAttackOnServer() && Time.time >= stunnedUntil) {
            LaunchProjectile(direction, archetypeStats.Damage, archetypeStats.ProjectileSpeed, AttackRange);
        }
        attackRoutine = null;
    }

    private void LaunchProjectile(Vector3 direction, float damage, float speed, float distance) {
        Vector3 origin = transform.position + Vector3.up * 1.05f + direction * 0.35f;
        int id = ++nextProjectileId;
        float lifetime = distance / Mathf.Max(0.1f, speed);
        if (IsSpawned) LaunchProjectileClientRpc(id, origin, direction, speed, lifetime);
        else CreateProjectileVisual(id, origin, direction, speed, lifetime);
        StartCoroutine(ProjectileRoutine(id, origin, direction, damage, speed, distance));
    }

    private IEnumerator ProjectileRoutine(int id, Vector3 origin, Vector3 direction,
        float damage, float speed, float distance) {
        Vector3 position = origin;
        float traveled = 0f;
        while (traveled < distance && GameState.isPlaying) {
            float step = Mathf.Min(speed * Time.deltaTime, distance - traveled);
            RaycastHit[] hits = Physics.SphereCastAll(position, 0.23f, direction, step,
                ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Collider collided = null;
            foreach (RaycastHit hit in hits) {
                Collider candidate = hit.collider;
                if (candidate == null || candidate.transform.IsChildOf(transform) ||
                    candidate.GetComponentInParent<EnemyController>() != null) continue;
                // Ground is below projectile height; its collider must not hide the shot.
                if (hit.normal.y > 0.7f) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                collided = candidate;
            }
            if (collided != null) {
                PlayerController player = collided.GetComponentInParent<PlayerController>();
                if (player != null && player.IsSpawned && player.IsAlive) player.TakeDamageFromEnemy(damage);
                break;
            }
            position += direction * step;
            traveled += step;
            yield return null;
        }

        if (IsSpawned) StopProjectileClientRpc(id);
        else RemoveProjectileVisual(id);
    }

    [ClientRpc]
    private void LaunchProjectileClientRpc(int id, Vector3 origin, Vector3 direction,
        float speed, float lifetime) {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient) return;
        CreateProjectileVisual(id, origin, direction, speed, lifetime);
    }

    [ClientRpc]
    private void StopProjectileClientRpc(int id) {
        RemoveProjectileVisual(id);
    }

    private void CreateProjectileVisual(int id, Vector3 origin, Vector3 direction,
        float speed, float lifetime) {
        Color color = archetypeStats != null && archetypeStats.Pattern == EnemyAttackPattern.Slam
            ? new Color(0.8f, 0.38f, 1f) : new Color(0.35f, 1f, 0.45f);
        EnemyProjectileVisual visual = EnemyProjectileVisual.Create(origin, direction, speed, lifetime, color);
        projectileVisuals[id] = visual;
    }

    private void RemoveProjectileVisual(int id) {
        if (!projectileVisuals.TryGetValue(id, out EnemyProjectileVisual visual)) return;
        projectileVisuals.Remove(id);
        if (visual != null) Destroy(visual.gameObject);
    }

    private void ClearProjectileVisuals() {
        foreach (EnemyProjectileVisual visual in projectileVisuals.Values) {
            if (visual != null) Destroy(visual.gameObject);
        }
        projectileVisuals.Clear();
    }

    private void DamageNearbyPlayers(Vector3 center, float radius, float damage,
        HashSet<PlayerController> hitPlayers) {
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits) {
            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player == null || !player.IsSpawned || !player.IsAlive || !hitPlayers.Add(player)) continue;
            player.TakeDamageFromEnemy(damage);
        }
    }

    private float GetWindup() {
        return archetypeStats != null ? archetypeStats.Windup :
            Mathf.Max(0f, attackData != null ? attackData.warningDuration : defaultWindup);
    }

    private Vector3 GetAttackCenter() {
        return transform.position + (archetypeStats == null && attackData != null
            ? attackData.centerOffset : defaultCenterOffset);
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
