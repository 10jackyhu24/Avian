using UnityEngine;

// Cosmetic only. EnemyAttackController simulates hits on the server.
public sealed class EnemyProjectileVisual : MonoBehaviour {
    private Vector3 direction;
    private float speed;
    private float remainingDistance;
    private float remainingLifetime;
    private bool usesAnimatedSprite;
    private Camera mainCamera;
    private Material material;
    private Material trailMaterial;

    public static EnemyProjectileVisual Create(Vector3 origin, Vector3 direction,
        float speed, float lifetime, Color color, GameObject spritePrefab = null, float spriteScale = 1f) {
        GameObject body;
        Material material = null;
        Material trailMaterial = null;
        if (spritePrefab != null) {
            body = Instantiate(spritePrefab, origin, Quaternion.identity);
            body.transform.localScale *= Mathf.Max(0.01f, spriteScale);
        } else {
            body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.localScale = Vector3.one * 0.45f;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader);
            material.color = color;
            body.GetComponent<Renderer>().sharedMaterial = material;

            TrailRenderer trail = body.AddComponent<TrailRenderer>();
            trail.time = 0.17f;
            trail.startWidth = 0.3f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.03f;
            trailMaterial = new Material(material);
            trail.sharedMaterial = trailMaterial;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
        body.name = "Enemy Projectile";
        body.hideFlags = HideFlags.DontSave;
        body.transform.position = origin;
        // Visuals never participate in physics; only the server's sweep can hit.
        foreach (Collider collider in body.GetComponentsInChildren<Collider>(true)) {
            collider.enabled = false;
            Destroy(collider);
        }

        EnemyProjectileVisual visual = body.AddComponent<EnemyProjectileVisual>();
        visual.direction = direction.normalized;
        visual.speed = speed;
        visual.remainingDistance = speed * lifetime;
        visual.remainingLifetime = lifetime;
        visual.usesAnimatedSprite = spritePrefab != null;
        visual.material = material;
        visual.trailMaterial = trailMaterial;
        return visual;
    }

    private void Update() {
        if (!GameState.isPlaying) return;
        float step = Mathf.Min(speed * Time.deltaTime, remainingDistance);
        transform.position += direction * step;
        remainingDistance -= step;
        remainingLifetime -= Time.deltaTime;
        if (remainingDistance <= 0f || remainingLifetime <= 0f) Destroy(gameObject);
    }

    private void LateUpdate() {
        if (!usesAnimatedSprite) return;
        if (mainCamera == null || !mainCamera.isActiveAndEnabled) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 screenDirection = mainCamera.transform.InverseTransformDirection(direction);
        // The authored sprite points down-left (minus 135 degrees).
        float angle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg + 135f;
        transform.rotation = mainCamera.transform.rotation * Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void OnDestroy() {
        if (material != null) Destroy(material);
        if (trailMaterial != null) Destroy(trailMaterial);
    }
}
