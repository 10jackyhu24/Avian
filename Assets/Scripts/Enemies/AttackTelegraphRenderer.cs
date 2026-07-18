using UnityEngine;

public class AttackTelegraphRenderer : MonoBehaviour {
    [Header("Visual")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.7f, 0.7f, 0.45f);
    [SerializeField] private float telegraphHeight = 0.06f;
    [SerializeField] private float verticalOffset = 0.03f;

    private const string TelegraphMeshName = "AttackTelegraph";
    private GameObject telegraphObject;
    private Renderer telegraphRenderer;
    private Material telegraphMaterial;
    private float hideAtTime;

    private void Update() {
        if (telegraphObject == null || !telegraphObject.activeSelf) {
            return;
        }

        if (Time.time >= hideAtTime) {
            HideTelegraph();
            return;
        }

        Vector3 ownerPosition = transform.position;
        telegraphObject.transform.position = new Vector3(
            ownerPosition.x,
            ownerPosition.y + verticalOffset,
            ownerPosition.z
        );
    }

    private void OnDestroy() {
        if (telegraphMaterial != null) {
            Destroy(telegraphMaterial);
        }
    }

    public void ShowTelegraph(float range, float duration) {
        ShowTelegraph(range, duration, telegraphColor);
    }

    public void ShowTelegraph(float range, float duration, Color color) {
        EnsureTelegraphObject();

        telegraphColor = color;
        telegraphMaterial.color = telegraphColor;

        float clampedRange = Mathf.Max(0.05f, range);
        telegraphObject.transform.localScale = new Vector3(
            clampedRange * 2f,
            telegraphHeight * 0.5f,
            clampedRange * 2f
        );

        Vector3 ownerPosition = transform.position;
        telegraphObject.transform.position = new Vector3(
            ownerPosition.x,
            ownerPosition.y + verticalOffset,
            ownerPosition.z
        );
        telegraphObject.SetActive(true);

        hideAtTime = Time.time + Mathf.Max(0.01f, duration);
    }

    public void HideTelegraph() {
        if (telegraphObject == null) {
            return;
        }

        telegraphObject.SetActive(false);
    }

    private void EnsureTelegraphObject() {
        if (telegraphObject != null) {
            return;
        }

        telegraphObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        telegraphObject.name = TelegraphMeshName;
        telegraphObject.transform.SetParent(transform, false);

        Collider telegraphCollider = telegraphObject.GetComponent<Collider>();
        if (telegraphCollider != null) {
            Destroy(telegraphCollider);
        }

        telegraphRenderer = telegraphObject.GetComponent<Renderer>();
        telegraphRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        telegraphRenderer.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null) {
            shader = Shader.Find("Standard");
        }

        telegraphMaterial = new Material(shader);
        telegraphMaterial.color = telegraphColor;
        telegraphRenderer.sharedMaterial = telegraphMaterial;

        telegraphObject.SetActive(false);
    }
}
