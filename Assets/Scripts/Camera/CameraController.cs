using UnityEngine;

public class CameraController : MonoBehaviour {
    [Header("Follow Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 8f, -10f);
    [SerializeField] private float followSmoothTime = 0.2f;
    [SerializeField] private bool lookAtTarget = true;
    [SerializeField] private bool useCurrentOffsetWhenTargetAssigned = true;

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
        if (target == null) {
            return;
        }

        Vector3 desiredPosition = target.position + followOffset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            followSmoothTime
        );

        if (lookAtTarget) {
            transform.LookAt(target);
        }
    }

    public void SetTarget(Transform newTarget) {
        if (newTarget == null) {
            target = null;
            return;
        }

        if (useCurrentOffsetWhenTargetAssigned) {
            followOffset = transform.position - newTarget.position;
        }

        target = newTarget;
    }
}
