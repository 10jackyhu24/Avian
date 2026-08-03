using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_AttachmentSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler {
    [Header("Slot Setup")]
    [SerializeField] private ItemType itemType;

    [Header("Views")]
    [SerializeField] private Image iconImage;

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

    #region Drag Drop Handlers

    public void OnBeginDrag(PointerEventData eventData) {

    }

    public void OnDrag(PointerEventData eventData) {

    }

    public void OnEndDrag(PointerEventData eventData) {

    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (dragDropManager.IsAttachmentMatch(itemType) && eventData.dragging) {
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
