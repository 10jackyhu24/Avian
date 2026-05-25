using UnityEngine;

public class CameraController : MonoBehaviour {
    [Header("Follow Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 followOffset = new Vector3(0, 16.2999992f, -11.8999996f);
    [SerializeField] private float followSmoothTime = 0.2f;

    private Vector3 followVelocity;

    private void Start() {
        if (target == null) {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) {
                SetTarget(player.transform);
            }
        }
    }

    private void LateUpdate() {
        if (!GameState.isPlaying) return;
        Vector3 desiredPosition = target.position + followOffset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            followSmoothTime
        );
    }

    public void SetTarget(Transform newTarget) {
        if (newTarget == null) {
            target = null;
            return;
        }
        target = newTarget;
    }
}
