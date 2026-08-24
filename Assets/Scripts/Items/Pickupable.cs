using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Pickupable : MonoBehaviour {
    private ItemData itemData;
    [SerializeField, Min(1)] private int count = 1;
    private List<AttachmentSlotData> attachments;

    public ItemData ItemData => itemData;
    public bool HasItem => itemData.IsValid;
    public int Count => count;
    public List<AttachmentSlotData> Attachments => attachments;

    public void SetItemData(ItemData value) {
        itemData = value;
    }

    public void SetAttachment(List<AttachmentSlotData> value) {
        attachments = value;
    }

    public void SetCount(int value) {
        count = Mathf.Max(1, value);
    }

    private void Awake() {
        IgnorePlayerAndEnemyCollision();
    }

    private void Reset() {
        itemData = ItemData.Empty;
    }

    private void OnCollisionEnter(Collision collision) {
        if (collision == null || collision.collider == null) {
            return;
        }

        if (collision.collider.GetComponentInParent<PlayerController>() != null ||
            collision.collider.GetComponentInParent<EnemyHealthDrop>() != null) {
            IgnoreCollisionWith(collision.collider);
        }
    }

    private void IgnorePlayerAndEnemyCollision() {
        Collider[] selfColliders = GetComponentsInChildren<Collider>();
        if (selfColliders == null || selfColliders.Length == 0) {
            return;
        }

        foreach (Collider selfCollider in selfColliders) {
            if (selfCollider == null) {
                continue;
            }

            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController player in players) {
                if (player == null) {
                    continue;
                }

                Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
                foreach (Collider playerCollider in playerColliders) {
                    if (playerCollider != null) {
                        Physics.IgnoreCollision(selfCollider, playerCollider, true);
                    }
                }

                CharacterController characterController = player.GetComponent<CharacterController>();
                if (characterController != null) {
                    Physics.IgnoreCollision(selfCollider, characterController, true);
                }
            }

            EnemyHealthDrop[] enemies = FindObjectsByType<EnemyHealthDrop>(FindObjectsSortMode.None);
            foreach (EnemyHealthDrop enemy in enemies) {
                if (enemy == null) {
                    continue;
                }

                Collider[] enemyColliders = enemy.GetComponentsInChildren<Collider>();
                foreach (Collider enemyCollider in enemyColliders) {
                    if (enemyCollider != null) {
                        Physics.IgnoreCollision(selfCollider, enemyCollider, true);
                    }
                }
            }
        }
    }

    private void IgnoreCollisionWith(Collider other) {
        if (other == null) {
            return;
        }

        Collider[] selfColliders = GetComponentsInChildren<Collider>();
        foreach (Collider selfCollider in selfColliders) {
            if (selfCollider != null) {
                Physics.IgnoreCollision(selfCollider, other, true);
            }
        }
    }
}
