using UnityEngine;

// Rotate the artwork on each client while leaving the networked physics root upright.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(200)]
public class CameraFacingSpriteVisual : MonoBehaviour {
    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private CharacterController enemyBody;
    private Vector3 authoredLocalPosition;

    private void Awake() {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnemyController enemy = GetComponentInParent<EnemyController>();
        enemyBody = enemy != null ? enemy.GetComponent<CharacterController>() : null;
        authoredLocalPosition = transform.localPosition;
    }

    private void LateUpdate() {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled) {
            mainCamera = Camera.main;
        }

        if (mainCamera != null) {
            transform.rotation = mainCamera.transform.rotation;
        }

        if (enemyBody != null && spriteRenderer != null && spriteRenderer.sprite != null) {
            // Move artwork only. Physics and attack origins stay on the upright root.
            transform.localPosition = authoredLocalPosition;
            float clearance = enemyBody.bounds.min.y + 0.04f - spriteRenderer.bounds.min.y;
            if (clearance > 0f) transform.position += Vector3.up * clearance;
        }
    }
}
