using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class UI_DragDropManager : MonoBehaviour {
    private struct DragData {
        public UI_BagItemSlot draggedSlot;
        public UI_BagItemSlot hoveredSlot;
        public bool isAbove;
        public UI_AttachmentSlot draggedAttachment;
    }

    [SerializeField] private Transform contentRoot;
    [SerializeField] private UI_DragIndicator dragIndicatorPrefab;
    private UI_DragIndicator dragIndicator;
    
    private UI_BagManager bagManager;
    
    private DragData dragData;

    private void Awake() {
        bagManager = FindFirstObjectByType<UI_BagManager>();
        dragIndicator = PrefabSpawn.SpawnInit(dragIndicatorPrefab, contentRoot);
        dragIndicator.Hide();
    }

    public void SetDraggedAttachment(UI_AttachmentSlot slot) {
        dragData.draggedAttachment = slot;
    }

    public void OnBagItemSlotBeginDrag(UI_BagItemSlot slot) {
        dragData.draggedSlot = slot;
        dragData.hoveredSlot = slot;
        dragData.draggedAttachment = null;
    }

    public void OnAttachmentSlotBeginDrag(UI_AttachmentSlot slot) {
        // draggedAttachment = slot;
        // draggedSlot = null;
        // isDraggingFromAttachment = true;
    }

    public void OnDragEnd() {
        if (dragData.draggedAttachment != null) {
            HandleAttachmentDropInSlot();
        } else {
            HandleSlotDropInBag();
        }

        dragData.draggedSlot = null;
        dragData.draggedAttachment = null;
        dragData.hoveredSlot = null;
    }

    public void OnBagItemSlotEnter(UI_BagItemSlot hoveredSlot) {
        dragData.hoveredSlot = hoveredSlot;
    }

    public void OnBagItemSlotHover(Vector2 pointerPosition) {
        if (dragData.draggedAttachment != null) return;
        RectTransform slotRect = dragData.hoveredSlot.GetComponent<RectTransform>();

        // Convert mouse screen position to local position relative to slotRect
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                slotRect, 
                pointerPosition, 
                null, // If UI Canvas Render Mode is Overlay, use null. If Camera Space, pass target Camera.
                out Vector2 localPoint)) {

            // In RectTransform local space, the vertical center is Y = 0
            dragData.isAbove = localPoint.y > 0;
        }
        dragIndicator.Hide();
        int index = ObjectControll.GetIndex(dragData.hoveredSlot);
        dragIndicator.Show(dragData.isAbove ? index : index + 1);
    }

    private void HandleSlotDropInBag() {
        dragIndicator.Hide();
        int begin = ObjectControll.GetIndex(dragData.draggedSlot);
        int end = ObjectControll.GetIndex(dragData.hoveredSlot);
        bagManager.MoveSlot(begin, dragData.isAbove ? end : end + 1);
    }

    private void HandleAttachmentDropInSlot() {
        int remove_index = ObjectControll.GetIndex(dragData.draggedSlot);
    }

    public void Hide() {
        dragIndicator.Hide();
    }

    public void Show() {
        dragIndicator.ShowDirectly();
    }

    private int FindSlotIndex(UI_BagItemSlot slot) {
        dragIndicator.Hide();
        int index = bagManager.FindSlotIndex(slot);
        dragIndicator.ShowDirectly();
        return index;
    }

    public bool IsAttachmentMatch(ItemType itemType) {
        return bagManager.bagDataList[FindSlotIndex(dragData.draggedSlot)].itemData.itemType == itemType;
    }
}
