using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_AttachmentSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler {
    [Header("Slot Setup")]
    [SerializeField] private AttachmentType slotType = AttachmentType.None;

    [Header("Views")]
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject emptyRoot;
    [SerializeField] private GameObject filledRoot;
    [SerializeField] private TextMeshProUGUI slotTypeText;
    [SerializeField] private CanvasGroup canvasGroup;

    public AttachmentType SlotType => slotType;
    public ItemData CurrentAttachment { get; private set; }

    private AttachmentSlotData boundAttachmentSlotData;
    private UI_DragDropManager dragDropManager;

    private void Awake() {
        ApplyVisualState(CurrentAttachment);
        dragDropManager = FindFirstObjectByType<UI_DragDropManager>();
    }

    private void OnValidate() {

    }

    public void SetupSlot(AttachmentType type, ItemData currentItem) {
        boundAttachmentSlotData = null;

        if (type != AttachmentType.None) {
            slotType = type;
        }

        CurrentAttachment = currentItem;
        ApplyVisualState(currentItem);
    }

    public void SetupSlot(AttachmentSlotData attachmentSlotData) {
        boundAttachmentSlotData = attachmentSlotData;

        if (attachmentSlotData == null) {
            CurrentAttachment = ItemData.Empty;
            ApplyVisualState(CurrentAttachment);
            return;
        }

        slotType = attachmentSlotData.requiredType;
        CurrentAttachment = attachmentSlotData.attachedItem;
        ApplyVisualState(CurrentAttachment);
    }

    public void ClearSlot() {
        if (boundAttachmentSlotData != null) {
            boundAttachmentSlotData.Clear();
        }

        CurrentAttachment = ItemData.Empty;
        ApplyVisualState(CurrentAttachment);
    }

    public ItemData DetachAttachment() {
        ItemData attachment = CurrentAttachment;

        if (boundAttachmentSlotData != null) {
            boundAttachmentSlotData.Clear();
        }

        CurrentAttachment = ItemData.Empty;
        ApplyVisualState(CurrentAttachment);
        return attachment;
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

    #region Drag Drop Handlers

    public void OnBeginDrag(PointerEventData eventData) {

    }

    public void OnDrag(PointerEventData eventData) {

    }

    public void OnEndDrag(PointerEventData eventData) {

    }

    public void OnPointerEnter(PointerEventData eventData) {

    }

    public void OnPointerExit(PointerEventData eventData) {
    
    }
    #endregion
}
