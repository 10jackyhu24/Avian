using UnityEngine;

// Cosmetics stay separate from the networked pickup root and its collider.
[DisallowMultipleComponent]
public sealed class WorldLootVisual : MonoBehaviour {
    [Min(0f)] [SerializeField] private float spinSpeed = 45f;
    [Range(0f, 45f)] [SerializeField] private float uprightTiltCorrection = 15f;

    private Renderer iconRenderer;
    private SpriteRenderer iconSprite;
    private Transform iconTransform;
    private TextMesh countLabel;
    private Renderer labelRenderer;
    private Camera mainCamera;
    private Quaternion authoredRotation;
    private bool isCoin;
    private float startedAt;
    private float dropHeight;
    private float groundY;
    private float nextGroundCheck;

    public static WorldLootVisual Ensure(GameObject owner, Renderer source, int count, float fallHeight = 0.6f, float iconScale = 1f) {
        if (source == null || Application.isBatchMode) return null;
        WorldLootVisual visual = owner.GetComponent<WorldLootVisual>();
        if (visual == null) visual = owner.AddComponent<WorldLootVisual>();
        if (visual.iconRenderer == null) visual.Initialize(source, fallHeight, iconScale);
        visual.SetCount(count);
        return visual;
    }

    private void Initialize(Renderer source, float fallHeight, float iconScale) {
        GameObject icon = new GameObject("Loot Icon");
        iconTransform = icon.transform;
        iconTransform.SetParent(transform, false);
        Vector3 ownerScale = transform.lossyScale;
        Vector3 sourceScale = source.transform.lossyScale * iconScale;
        iconTransform.localScale = DivideScale(sourceScale, ownerScale);
        authoredRotation = source.transform.rotation;
        isCoin = GetComponent<CoinPickup>() != null;
        if (source is SpriteRenderer sourceSprite) {
            iconSprite = icon.AddComponent<SpriteRenderer>();
            iconSprite.sprite = sourceSprite.sprite;
            iconSprite.color = sourceSprite.color;
            iconRenderer = iconSprite;
        } else {
            MeshFilter sourceMesh = source.GetComponent<MeshFilter>();
            icon.AddComponent<MeshFilter>().sharedMesh = sourceMesh != null ? sourceMesh.sharedMesh : null;
            iconRenderer = icon.AddComponent<MeshRenderer>();
        }
        iconRenderer.sharedMaterials = source.sharedMaterials;
        iconRenderer.sortingLayerID = source.sortingLayerID;
        iconRenderer.sortingOrder = source.sortingOrder;
        source.enabled = false;

        GameObject label = new GameObject("Loot Count");
        label.transform.SetParent(transform, false);
        label.transform.localScale = DivideScale(Vector3.one, ownerScale);
        countLabel = label.AddComponent<TextMesh>();
        countLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countLabel.fontSize = 48;
        countLabel.characterSize = 0.055f;
        countLabel.anchor = TextAnchor.MiddleLeft;
        countLabel.alignment = TextAlignment.Left;
        countLabel.color = Color.white;
        labelRenderer = label.GetComponent<Renderer>();
        labelRenderer.sharedMaterial = countLabel.font.material;
        labelRenderer.sortingLayerID = source.sortingLayerID;
        labelRenderer.sortingOrder = source.sortingOrder + 1;
        startedAt = Time.time;
        dropHeight = Mathf.Max(0f, fallHeight);
        groundY = transform.position.y;
    }

    private static Vector3 DivideScale(Vector3 value, Vector3 divisor) {
        return new Vector3(value.x / Mathf.Max(0.001f, Mathf.Abs(divisor.x)),
            value.y / Mathf.Max(0.001f, Mathf.Abs(divisor.y)), value.z / Mathf.Max(0.001f, Mathf.Abs(divisor.z)));
    }

    public void SetCount(int count) {
        if (countLabel == null) return;
        countLabel.text = $"x {Mathf.Max(1, count)}";
        labelRenderer.enabled = iconRenderer != null && iconRenderer.enabled;
    }

    public void SetSprite(Sprite sprite, int count) {
        if (iconSprite == null) return;
        iconSprite.sprite = sprite;
        iconRenderer.enabled = sprite != null;
        SetCount(count);
    }

    private void LateUpdate() {
        if (iconRenderer == null || !iconRenderer.enabled) return;
        if (mainCamera == null || !mainCamera.isActiveAndEnabled) mainCamera = Camera.main;
        if (Time.time >= nextGroundCheck) {
            nextGroundCheck = Time.time + 0.25f;
            if (GroundPlacement.TryGetSurface(transform.position, out Vector3 ground)) groundY = ground.y;
        }

        float age = Time.time - startedAt;
        float falling = Mathf.Max(0f, dropHeight - 4.9f * age * age);
        float hover = falling > 0f ? 0f : 0.04f * (1f + Mathf.Sin(age * 1.8f));
        float angle = age * spinSpeed;
        Quaternion facingRotation = mainCamera != null
            ? mainCamera.transform.rotation * Quaternion.Euler(-uprightTiltCorrection, 0f, 0f)
            : authoredRotation;
        if (isCoin) {
            // The coin mesh lies in its local XY plane: remove camera pitch and
            // roll so the disc stands vertically and spins around world up.
            Vector3 facingDirection = (mainCamera != null ? mainCamera.transform.rotation : authoredRotation) * Vector3.forward;
            float yaw = Mathf.Atan2(facingDirection.x, facingDirection.z) * Mathf.Rad2Deg;
            facingRotation = Quaternion.Euler(0f, yaw, 0f);
        }
        iconTransform.rotation = facingRotation * Quaternion.AngleAxis(angle, Vector3.up);
        iconTransform.position = transform.position;
        iconTransform.position += Vector3.up * (groundY + 0.08f + hover + falling - iconRenderer.bounds.min.y);

        labelRenderer.enabled = mainCamera != null;
        if (mainCamera == null) return;
        Vector3 right = mainCamera.transform.right;
        Vector3 extent = iconRenderer.bounds.extents;
        float halfWidth = Mathf.Abs(right.x) * extent.x + Mathf.Abs(right.y) * extent.y + Mathf.Abs(right.z) * extent.z;
        countLabel.transform.position = iconRenderer.bounds.center + right * (halfWidth + 0.12f);
        countLabel.transform.rotation = mainCamera.transform.rotation;
    }
}
