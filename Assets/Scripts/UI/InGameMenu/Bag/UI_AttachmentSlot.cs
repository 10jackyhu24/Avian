using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_AttachmentSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler {
    [Header("Slot Setup")]
    [SerializeField] private ItemType itemType;

    public ItemData CurrentAttachment { get; private set; }

    private AttachmentSlotData boundAttachmentSlotData;
    private UI_DragDropManager dragDropManager;

    public SelectCover selectCover;

    private void Awake() {
        dragDropManager = FindFirstObjectByType<UI_DragDropManager>();
        selectCover = GetComponent<SelectCover>();
        selectCover.Init(gameObject);
    }

    public void BindAttachmentSlotData(AttachmentSlotData attachmentSlotData) {
        boundAttachmentSlotData = attachmentSlotData;
        if (boundAttachmentSlotData != null) {
            setAttachment(boundAttachmentSlotData.attachedItem);
        } else {
            setAttachment(ItemData.Empty);
        }
    }

    public ItemType GetItemType() {
        return itemType;
    }

    public void setAttachment(ItemData itemData) {
        CurrentAttachment = itemData;

        GameObject iconObject = UIBindingUtility.FindChildObject(gameObject, "Icon");
        if (iconObject == null) {
            return;
        }

        Image image = iconObject.GetComponent<Image>();
        if (image == null) {
            return;
        }

        if (itemData.itemObject == null) {
            image.sprite = null;
            image.enabled = false;
            return;
        }

        image.sprite = itemData.itemObject.icon;
        image.enabled = image.sprite != null;
    }

    #region Drag Drop Handlers

    public void OnBeginDrag(PointerEventData eventData) {
        if (CurrentAttachment.itemType == ItemType.Empty) return;
        selectCover.Show();
        dragDropManager.OnAttachmentSlotBeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData) {
        if (CurrentAttachment.itemType == ItemType.Empty) return;
        dragDropManager.OnBagItemSlotHover(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (CurrentAttachment.itemType == ItemType.Empty) return;
        selectCover.Hide();
        dragDropManager.OnDragEnd();
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (dragDropManager.GetCurrentDragAttachment() == null && dragDropManager.GetCurrentDragSlot() == null) return;
        dragDropManager.SetHoverAttachment(this);
        if (dragDropManager.GetCurrentDragAttachment() != this && eventData.dragging && dragDropManager.IsAttachmentMatch(itemType)) {
            dragDropManager.Hide();
            selectCover.Show();
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (dragDropManager.GetCurrentDragAttachment() == null && dragDropManager.GetCurrentDragSlot() == null) return;
        dragDropManager.SetHoverAttachment(null);
        if (dragDropManager.GetCurrentDragAttachment() != this && eventData.dragging) {
            selectCover.Hide();
            dragDropManager.Show();
        }
    }
    #endregion
}
