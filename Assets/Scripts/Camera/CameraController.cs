using UnityEngine;

public class CameraController : MonoBehaviour {
    [Header("Movement Settings")]
    [Tooltip("Camera movement speed")]
    public float moveSpeed = 15f;

    [Tooltip("Shift key press speed multiplier")]
    public float shiftBoost = 2f;

    void Update() {
        // 1. Get front, back, left, right inputs (Default: W/S for front/back, A/D for left/right)
        float horizontal = Input.GetAxis("Horizontal"); 
        float vertical = Input.GetAxis("Vertical");     

        // 2. Get up and down inputs (Default: E key for up, Q key for down)
        float upDown = 0f;
        if (Input.GetKey(KeyCode.E)) {
            upDown = 1f;
        } else if (Input.GetKey(KeyCode.Q)) {
            upDown = -1f;
        }

        // 3. Calculate movement vector
        Vector3 inputDirection = new Vector3(horizontal, upDown, vertical);

        // 4. Acceleration mechanism (Press Left Shift to speed up)
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) {
            currentSpeed *= shiftBoost;
        }

        // 5. Use world space for movement
        Vector3 moveVector = new Vector3(horizontal, upDown, vertical);
        
        // Limit diagonal movement speed to prevent it from being faster than straight movement
        if (moveVector.sqrMagnitude > 1f) {
            moveVector.Normalize();
        }

        transform.position += moveVector * currentSpeed * Time.deltaTime;
    }
}
