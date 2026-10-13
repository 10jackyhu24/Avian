using System;
using UnityEngine;

/// <summary>Queries hitboxes without adding a physical collider to the player.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public class PlayerEnemySensor : MonoBehaviour {
    [Tooltip("World-space radius of the invisible enemy detection area.")]
    [Min(0.1f)] [SerializeField] private float detectionRadius = 12f;
    [SerializeField] private LayerMask enemyLayers = ~0;
    [Min(0.02f)] [SerializeField] private float scanInterval = 0.1f;

    private PlayerController player;
    private Collider[] hits = new Collider[64];
    private float nextScanTime;

    public EnemyHealthDrop Target { get; private set; }

    private void Awake() {
        player = GetComponent<PlayerController>();
    }

    private void Update() {
        if (!player.IsSpawned || !player.IsOwner || !player.IsClient || !player.IsAlive) {
            Target = null;
            return;
        }

        if (Target != null && (!Target.isActiveAndEnabled || !Target.IsAlive || !Target.IsSpawned)) {
            Target = null;
            nextScanTime = 0f;
        }

        if (Time.unscaledTime < nextScanTime) {
            return;
        }
        nextScanTime = Time.unscaledTime + Mathf.Max(0.02f, scanInterval);
        Scan();
    }

    private void Scan() {
        Vector3 center = transform.position;
        int count;
        // Retry a saturated query so scenery or a crowded group cannot hide enemies.
        while (true) {
            count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0.1f, detectionRadius),
                hits, enemyLayers, QueryTriggerInteraction.Collide);
            if (count < hits.Length) {
                break;
            }
            Array.Resize(ref hits, hits.Length * 2);
        }

        EnemyHealthDrop nearest = null;
        float nearestDistance = float.PositiveInfinity;
        bool targetStillInRange = false;
        for (int i = 0; i < count; i++) {
            Collider hit = hits[i];
            hits[i] = null;
            EnemyHealthDrop enemy = hit.GetComponentInParent<EnemyHealthDrop>();
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsAlive || !enemy.IsSpawned) {
                continue;
            }

            targetStillInRange |= enemy == Target;
            // Measure to the hitbox surface, including large enemies whose origin is outside.
            float distance = (hit.ClosestPoint(center) - center).sqrMagnitude;
            if (distance < nearestDistance) {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        if (!targetStillInRange) {
            Target = nearest;
        }
    }

    private void OnDisable() {
        Target = null;
        nextScanTime = 0f;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = new Color(0.5f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, detectionRadius));
    }
}
