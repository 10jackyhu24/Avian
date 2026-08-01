using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_AttachmentSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler {
    [Header("Slot Setup")]
    [SerializeField] private AttachmentType slotType = AttachmentType.None;

    [Header("Views")]
    [SerializeField] private Image iconImage;

    public AttachmentType SlotType => slotType;
    public ItemData CurrentAttachment { get; private set; }

    private AttachmentSlotData boundAttachmentSlotData;
    private UI_DragDropManager dragDropManager;

    private SelectCover selectCover;

    private void Awake() {
        dragDropManager = FindFirstObjectByType<UI_DragDropManager>();
        selectCover = GetComponent<SelectCover>();
        selectCover.Init(gameObject);
    }

    private void OnValidate() {

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
