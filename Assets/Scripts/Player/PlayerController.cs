using UnityEngine;
using Unity.Netcode; // for NGO

[RequireComponent(typeof(CharacterController))]

//原本是MonoBehaviour 為了適配NGO改成 NetworkBehaviour
public class PlayerController : NetworkBehaviour {
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float rotationSpeed = 12f;
    Animator animator;
    SpriteRenderer spriteRenderer;
    private CharacterController characterController;
    private float verticalVelocity;
    bool previousFlipX = false;
    
    private void Awake() {
        characterController = GetComponent<CharacterController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void Update() {
        //IsOwner 來判定是否是玩家本人操控的
        if(!IsOwner) return;

        HandleMovement();
    }

    private void HandleMovement() {
        if (characterController.isGrounded && verticalVelocity < 0f) {
            verticalVelocity = -2f;
        }
        
        float horizontal = Input.GetAxisRaw("Horizontal");
        
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (moveDirection.sqrMagnitude > 0f) {
            if(horizontal <=0) {
            if(!previousFlipX) {
                spriteRenderer.flipX = true;
                previousFlipX = true;
            }
        }
        else {
            if(previousFlipX) {
                spriteRenderer.flipX = false;
                previousFlipX = false;
            }
        }
            animator.SetBool("isMoving", true);
        }
        else {
            animator.SetBool("isMoving", false);
        }

        if (Input.GetButtonDown("Jump") && characterController.isGrounded) {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        transform.rotation=new Quaternion(0,0,0,0);
        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalVelocity;
        
        characterController.Move(velocity * Time.deltaTime);
        
        
    }
}
