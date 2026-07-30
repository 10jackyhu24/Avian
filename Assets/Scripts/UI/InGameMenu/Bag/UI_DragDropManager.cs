using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class UI_DragDropManager : MonoBehaviour {
    private struct DragData {
        public UI_BagItemSlot draggedSlot;
        public UI_BagItemSlot hoveredSlot;
        public bool isAbove;
        public bool isDraggingFromAttachment;
    }

    [SerializeField] private Transform contentRoot;
    [SerializeField] private UI_DragIndicator dragIndicatorPrefab;
    private UI_DragIndicator dragIndicator;
    
    // private UI_AttachmentSlot draggedAttachment;
    private UI_BagManager bagManager;
    
    private DragData dragData;

    private void Awake() {
        bagManager = FindFirstObjectByType<UI_BagManager>();
        dragIndicator = PrefabSpawn.SpawnInit(dragIndicatorPrefab, contentRoot);
        dragIndicator.Hide();
    }

    public void OnBagItemSlotBeginDrag(UI_BagItemSlot slot) {
        dragData.draggedSlot = slot;
        dragData.hoveredSlot = slot;
        // dragData.draggedAttachment = null;
        dragData.isDraggingFromAttachment = false;
    }

    public void OnAttachmentSlotBeginDrag(UI_AttachmentSlot slot) {
        // draggedAttachment = slot;
        // draggedSlot = null;
        // isDraggingFromAttachment = true;
    }

    public void OnDragEnd() {
        if (dragData.isDraggingFromAttachment) {
            HandleAttachmentDrop();
        } else {
            HandleSlotDropInBag();
        }

        dragData.draggedSlot = null;
        // dragData.draggedAttachment = null;
        dragData.hoveredSlot = null;
    }

    public void OnBagItemSlotEnter(UI_BagItemSlot hoveredSlot) {
        dragData.hoveredSlot = hoveredSlot;
    }

    public void OnBagItemSlotHover(Vector2 pointerPosition) {
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

    private void HandleAttachmentDrop() {

    }
}
