using UnityEngine;

// Ground queries exclude actors and loot so another pickup cannot become a floor.
public static class GroundPlacement {
    public static bool IsEnvironment(Collider collider) {
        return collider != null && !collider.isTrigger &&
            collider.GetComponentInParent<PlayerController>() == null &&
            collider.GetComponentInParent<EnemyController>() == null &&
            collider.GetComponentInParent<Pickupable>() == null &&
            collider.GetComponentInParent<CoinPickup>() == null;
    }

    public static bool TryGetSurface(Vector3 position, out Vector3 ground) {
        ground = position;
        float nearest = float.MaxValue;
        bool found = false;
        foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 2f, Vector3.down,
                     64f, ~0, QueryTriggerInteraction.Ignore)) {
            if (!IsEnvironment(hit.collider) || hit.normal.y < 0.6f || hit.distance >= nearest) continue;
            nearest = hit.distance;
            ground = hit.point;
            found = true;
        }
        return found;
    }

    public static bool TryFindDropGround(Vector3 position, out Vector3 ground) {
        TerrainController terrain = TerrainController.Instance;
        if (terrain != null) {
            if (terrain.TryGetClearGroundPosition(position, out ground)) return true;
            for (int ring = 1; ring <= 3; ring++) {
                for (int index = 0; index < 8; index++) {
                    float angle = index * Mathf.PI * 0.25f;
                    Vector3 candidate = position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 0.6f);
                    if (terrain.TryGetClearGroundPosition(candidate, out ground)) return true;
                }
            }
        }
        return TryGetSurface(position, out ground);
    }
}
