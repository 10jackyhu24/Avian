using UnityEngine;

// Rotate the artwork on each client while leaving the networked physics root upright.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(200)]
public class CameraFacingSpriteVisual : MonoBehaviour {
    private Camera mainCamera;

    private void LateUpdate() {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled) {
            mainCamera = Camera.main;
        }

        if (mainCamera != null) {
            transform.rotation = mainCamera.transform.rotation;
        }
    }
}
