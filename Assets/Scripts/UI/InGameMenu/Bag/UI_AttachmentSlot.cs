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

    }

    public void OnDrag(PointerEventData eventData) {

    }

    public void OnEndDrag(PointerEventData eventData) {
        
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (eventData.dragging && dragDropManager.IsAttachmentMatch(itemType)) {
            dragDropManager.SetDraggedAttachment(this);
            dragDropManager.Hide();
            selectCover.Show();
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (eventData.dragging) {
            selectCover.Hide();
            dragDropManager.SetDraggedAttachment(null);
            dragDropManager.Show();
        }
    }
    #endregion
}
