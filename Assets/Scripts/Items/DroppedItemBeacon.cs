using UnityEngine;

[DisallowMultipleComponent]
public class DroppedItemBeacon : MonoBehaviour {
    [SerializeField] private float beaconHeight = 2.5f;
    [SerializeField] private float bottomOffset = 0.25f;
    [SerializeField] private float baseWidth = 0.12f;
    [SerializeField] private float pulseSpeed = 2.5f;
    [SerializeField] private float minimumAlpha = 0.2f;
    [SerializeField] private float maximumAlpha = 0.7f;
    [SerializeField] private Color beaconColor = new Color(1f, 0.82f, 0.25f, 1f);

    private LineRenderer beaconRenderer;

    private void Awake() {
        if (Application.isBatchMode) {
            enabled = false;
            return;
        }

        SpriteRenderer itemRenderer = GetComponentInChildren<SpriteRenderer>();
        if (itemRenderer == null || itemRenderer.sharedMaterial == null) {
            enabled = false;
            return;
        }

        beaconRenderer = gameObject.AddComponent<LineRenderer>();
        beaconRenderer.sharedMaterial = itemRenderer.sharedMaterial;
        beaconRenderer.useWorldSpace = true;
        beaconRenderer.positionCount = 2;
        beaconRenderer.startWidth = baseWidth;
        beaconRenderer.endWidth = 0.02f;
        beaconRenderer.numCapVertices = 4;
        beaconRenderer.alignment = LineAlignment.View;
        beaconRenderer.sortingLayerID = itemRenderer.sortingLayerID;
        beaconRenderer.sortingOrder = itemRenderer.sortingOrder + 1;
    }

    private void Update() {
        if (beaconRenderer == null) {
            return;
        }

        Vector3 start = transform.position + Vector3.up * bottomOffset;
        beaconRenderer.SetPosition(0, start);
        beaconRenderer.SetPosition(1, start + Vector3.up * beaconHeight);

        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minimumAlpha, maximumAlpha, pulse);

        Color bottomColor = beaconColor;
        bottomColor.a = alpha;
        Color topColor = beaconColor;
        topColor.a = 0f;

        beaconRenderer.startColor = bottomColor;
        beaconRenderer.endColor = topColor;
    }
}
