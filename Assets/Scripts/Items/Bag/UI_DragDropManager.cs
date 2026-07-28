using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages drag-drop operations for inventory items and attachments
/// Handles slot reordering and creating new slots from attachments
/// </summary>
public class UI_DragDropManager : MonoBehaviour {
    [SerializeField] private UI_DragIndicator dragIndicator;
    
    private UI_BagItemSlot draggedSlot;
    private UI_AttachmentSlot draggedAttachment;
    private UI_BagItemSlot hoveredSlot;
    private bool isDraggingFromAttachment;
    
    private Canvas canvas;
    private UI_BagManager bagManager;

    private void Awake() {
        canvas = GetComponentInParent<Canvas>();
        bagManager = GetComponent<UI_BagManager>();
    }

    /// <summary>
    /// Called when dragging starts on a BagItemSlot
    /// </summary>
    public void OnBagItemSlotBeginDrag(UI_BagItemSlot slot) {
        draggedSlot = slot;
        draggedAttachment = null;
        isDraggingFromAttachment = false;
    }

    /// <summary>
    /// Called when dragging starts on an AttachmentSlot
    /// Creates a temporary copy as a new bag slot
    /// </summary>
    public void OnAttachmentSlotBeginDrag(UI_AttachmentSlot slot) {
        draggedAttachment = slot;
        draggedSlot = null;
        isDraggingFromAttachment = true;
    }

    /// <summary>
    /// Called when dragging ends - performs the actual reordering
    /// </summary>
    public void OnDragEnd() {
        dragIndicator.Hide();

        if (hoveredSlot != null && (draggedSlot != null || draggedAttachment != null)) {
            if (isDraggingFromAttachment) {
                HandleAttachmentDrop();
            } else {
                HandleSlotDrop();
            }
        }

        draggedSlot = null;
        draggedAttachment = null;
        hoveredSlot = null;
    }

    /// <summary>
    /// Called when hovering over a BagItemSlot during drag
    /// Shows indicator and determines if it's above or below
    /// </summary>
    public void OnBagItemSlotHover(UI_BagItemSlot slot, Vector2 pointerPosition) {
        hoveredSlot = slot;

        RectTransform slotRect = slot.GetComponent<RectTransform>();
        if (slotRect == null) return;

        // Check if pointer is in upper or lower half of the slot
        Vector3[] slotCorners = new Vector3[4];
        slotRect.GetWorldCorners(slotCorners);
        
        float slotCenterY = (slotCorners[0].y + slotCorners[1].y) / 2f;
        bool isAbove = pointerPosition.y > slotCenterY;

        dragIndicator.Show(slotRect, isAbove);
    }

    public void OnDragExit() {
        hoveredSlot = null;
        dragIndicator.Hide();
    }

    /// <summary>
    /// Reorders bag slots: moves draggedSlot before or after hoveredSlot
    /// </summary>
    private void HandleSlotDrop() {
        if (draggedSlot == hoveredSlot) return;

        int draggedIndex = bagManager.GetSlotIndex(draggedSlot);
        int hoveredIndex = bagManager.GetSlotIndex(hoveredSlot);

        if (draggedIndex < 0 || hoveredIndex < 0) return;

        // Determine insertion position based on visual indicator
        RectTransform hoveredRect = hoveredSlot.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        hoveredRect.GetWorldCorners(corners);
        
        // Get the current pointer position through EventSystem
        Vector2 pointerPos = Input.mousePosition;
        float slotCenterY = (corners[0].y + corners[1].y) / 2f;
        bool insertAbove = pointerPos.y > slotCenterY;

        int insertIndex = insertAbove ? hoveredIndex : hoveredIndex + 1;

        bagManager.MoveSlot(draggedIndex, insertIndex);
    }

    /// <summary>
    /// Creates a new bag slot from the dragged attachment
    /// </summary>
    private void HandleAttachmentDrop() {
        if (draggedAttachment == null || draggedAttachment.CurrentAttachment.itemObject == null) {
            return;
        }

        ItemData attachmentData = draggedAttachment.CurrentAttachment;
        int insertIndex = bagManager.GetSlotIndex(hoveredSlot);

        if (insertIndex < 0) {
            bagManager.AddItem(attachmentData, 1);
        } else {
            bagManager.InsertItem(attachmentData, 1, insertIndex);
        }

        // Clear the attachment from original slot
        draggedAttachment.ClearSlot();
    }
}
