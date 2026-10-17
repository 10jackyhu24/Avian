using UnityEngine;

// Cosmetic only. EnemyAttackController simulates hits on the server.
public sealed class EnemyProjectileVisual : MonoBehaviour {
    private Vector3 direction;
    private float speed;
    private float expiresAt;
    private Material material;
    private Material trailMaterial;

    public static EnemyProjectileVisual Create(Vector3 origin, Vector3 direction,
        float speed, float lifetime, Color color) {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Enemy Projectile";
        body.hideFlags = HideFlags.DontSave;
        body.transform.position = origin;
        body.transform.localScale = Vector3.one * 0.45f;
        Collider collider = body.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.color = color;
        body.GetComponent<Renderer>().sharedMaterial = material;

        TrailRenderer trail = body.AddComponent<TrailRenderer>();
        trail.time = 0.17f;
        trail.startWidth = 0.3f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.03f;
        Material trailMaterial = new Material(material);
        trail.sharedMaterial = trailMaterial;
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);

        EnemyProjectileVisual visual = body.AddComponent<EnemyProjectileVisual>();
        visual.direction = direction.normalized;
        visual.speed = speed;
        visual.expiresAt = Time.time + lifetime + 0.3f;
        visual.material = material;
        visual.trailMaterial = trailMaterial;
        return visual;
    }

    private void Update() {
        transform.position += direction * speed * Time.deltaTime;
        if (Time.time >= expiresAt) Destroy(gameObject);
    }

    private void OnDestroy() {
        if (material != null) Destroy(material);
        if (trailMaterial != null) Destroy(trailMaterial);
    }
}
