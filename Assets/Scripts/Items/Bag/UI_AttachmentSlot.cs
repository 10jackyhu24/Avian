using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_AttachmentSlot : MonoBehaviour {
    [Header("Slot Setup")]
    [SerializeField] private AttachmentType slotType = AttachmentType.None;

    [Header("Views")]
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject emptyRoot;
    [SerializeField] private GameObject filledRoot;
    [SerializeField] private TextMeshProUGUI slotTypeText;

    public AttachmentType SlotType => slotType;
    public ItemData CurrentAttachment { get; private set; }

    private void Awake() {
        ApplyVisualState(CurrentAttachment);
    }

    public void SetupSlot(AttachmentType type, ItemData currentItem) {
        if (type != AttachmentType.None) {
            slotType = type;
        }

        CurrentAttachment = currentItem;
        ApplyVisualState(currentItem);
    }

    public void ClearSlot() {
        CurrentAttachment = ItemData.Empty;
        ApplyVisualState(CurrentAttachment);
    }

    private void ApplyVisualState(ItemData currentItem) {
        bool hasItem = currentItem.itemObject != null && currentItem.itemType != ItemType.Empty;

        if (slotTypeText != null) {
            slotTypeText.text = slotType == AttachmentType.None ? string.Empty : slotType.ToString();
        }

        if (filledRoot != null) {
            filledRoot.SetActive(hasItem);
        }

        if (emptyRoot != null) {
            emptyRoot.SetActive(!hasItem);
        }

        if (iconImage != null) {
            iconImage.enabled = hasItem && currentItem.itemObject.icon != null;
            iconImage.sprite = hasItem ? currentItem.itemObject.icon : null;
        }
    }

    // Validation method for drag and drop
    public bool CanEquipAttachment(ItemData incomingItem) {
        if (incomingItem.itemObject == null) return false;
        if (slotType == AttachmentType.None) return false;
        if (incomingItem.attachmentType == AttachmentType.None) return false;
        return incomingItem.attachmentType == slotType;
    }
}
