using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

public class UI_DragDropManager : MonoBehaviour {
    private struct DragData {
        public UI_BagItemSlot draggedSlot;
        public UI_BagItemSlot hoveredSlot;
        public bool isAbove;
        public UI_AttachmentSlot draggedAttachment;
        public UI_AttachmentSlot hoverAttachment;
        public bool isMyBagBegin;
        public bool isMyBagEnd;
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

    public UI_BagItemSlot GetCurrentDragSlot() {
        return dragData.draggedSlot;
    }

    public UI_AttachmentSlot GetCurrentDragAttachment() {
        return dragData.draggedAttachment;
    }

    public void SetHoverAttachment(UI_AttachmentSlot slot) {
        dragData.hoverAttachment = slot;
    }

    public void SetBag(bool isMyBag) {
        dragData.isMyBagEnd = isMyBag;
    }

    public void OnBagItemSlotBeginDrag(UI_BagItemSlot slot) {
        dragData.draggedAttachment = null;
        dragData.hoverAttachment = null;
        dragData.draggedSlot = slot;
        dragData.hoveredSlot = slot;
        dragData.isMyBagBegin = dragData.isMyBagEnd;
    }

    public void OnAttachmentSlotBeginDrag(UI_AttachmentSlot slot) {
        dragData.draggedAttachment = slot;
        dragData.hoverAttachment = slot;
        dragData.draggedSlot = null;
        dragData.hoveredSlot = dragData.draggedAttachment.GetComponentInParent<UI_BagItemSlot>();
        dragData.isMyBagBegin = dragData.isMyBagEnd;
    }

    public void OnDragEnd() {
        dragIndicator.Hide();
        if (dragData.isMyBagBegin && !dragData.isMyBagEnd) {
            HandleDropOutOfBag();
        }
        else if (dragData.isMyBagBegin && dragData.isMyBagEnd) {
            if (dragData.draggedSlot != null) {
                if (dragData.hoverAttachment != null) {
                    HandleAttachmentDropInSlot();
                } else {
                    HandleSlotDropInBag();
                }
            }
            else {
                if (dragData.hoverAttachment != null) {
                    HandleAttachmentDropInAttachment();
                } else {
                    HandleAttachmentDropInBag();
                }
            }
        }

        dragData.draggedSlot = null;
        dragData.hoverAttachment = null;
        dragData.hoveredSlot = null;
    }

    public void OnBagItemSlotEnter(UI_BagItemSlot hoveredSlot) {
        dragData.hoveredSlot = hoveredSlot;
    }

    public void OnBagItemSlotHover(Vector2 pointerPosition) {
        if (!dragData.isMyBagEnd) return;
        if (dragData.hoverAttachment != null) return;
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

    private void HandleDropOutOfBag() {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        Vector3 spawnPosition = player.transform.position;

        if (dragData.draggedSlot != null) {
            int slotIndex = ObjectControll.GetIndex(dragData.draggedSlot);
            if (slotIndex >= 0 && slotIndex < bagManager.bagDataList.Count) {
                ItemData itemData = bagManager.bagDataList[slotIndex].itemData;
                int count = bagManager.bagDataList[slotIndex].count;
                ItemManager.AddItemInScene(itemData.itemObject.itemName, spawnPosition, Quaternion.Euler(0f, 0f, 90f), count);
                bagManager.RemoveSlot(slotIndex);
            }
            return;
        }

        if (dragData.draggedAttachment != null) {
            UI_BagItemSlot parentSlot = dragData.draggedAttachment.GetComponentInParent<UI_BagItemSlot>();
            if (parentSlot == null) {
                return;
            }

            int slotIndex = ObjectControll.GetIndex(parentSlot);
            if (slotIndex < 0 || slotIndex >= bagManager.bagDataList.Count) {
                return;
            }

            ItemData itemData = dragData.draggedAttachment.CurrentAttachment;
            ItemManager.AddItemInScene(itemData.itemObject.itemName, spawnPosition, Quaternion.Euler(0f, 0f, 90f));
            bagManager.ClearAttachmentItem(slotIndex, ItemData.GetAttachmentType(dragData.draggedAttachment.GetItemType()));
            return;
        }
    }

    private void HandleSlotDropInBag() {
        int begin = ObjectControll.GetIndex(dragData.draggedSlot);
        int end = ObjectControll.GetIndex(dragData.hoveredSlot);
        bagManager.MoveSlot(begin, dragData.isAbove ? end : end + 1);
    }

    private void HandleAttachmentDropInSlot() {
        int begin = ObjectControll.GetIndex(dragData.draggedSlot);
        int end = ObjectControll.GetIndex(dragData.hoveredSlot);
        
        dragData.hoverAttachment.selectCover.Hide();
        ItemData itemData = bagManager.bagDataList[begin].itemData;
        if (bagManager.SetAttachmentItem(end, itemData.attachmentType, itemData)) {
            bagManager.RemoveSlot(begin);
        }
    }

    private void HandleAttachmentDropInAttachment() {
        int begin = ObjectControll.GetIndex(dragData.draggedAttachment.GetComponentInParent<UI_BagItemSlot>());
        int end = ObjectControll.GetIndex(dragData.hoveredSlot);

        dragData.hoverAttachment.selectCover.Hide();
        if (dragData.hoverAttachment.CurrentAttachment.itemType == ItemType.Empty) {
            bagManager.SetAttachmentItem(end, ItemData.GetAttachmentType(dragData.hoverAttachment.GetItemType()), dragData.draggedAttachment.CurrentAttachment);
            bagManager.ClearAttachmentItem(begin, ItemData.GetAttachmentType(dragData.draggedAttachment.GetItemType()));
        }
    }

    private void HandleAttachmentDropInBag() {
        int begin = ObjectControll.GetIndex(dragData.draggedAttachment.GetComponentInParent<UI_BagItemSlot>());
        int end = ObjectControll.GetIndex(dragData.hoveredSlot);
        bagManager.ClearAttachmentItem(begin, ItemData.GetAttachmentType(dragData.draggedAttachment.GetItemType()));
        bagManager.AddItem(dragData.draggedAttachment.CurrentAttachment, 1, dragData.isAbove ? end : end + 1);
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
        if (dragData.draggedSlot != null)
            return bagManager.bagDataList[FindSlotIndex(dragData.draggedSlot)].itemData.itemType == itemType;
        if (dragData.hoverAttachment != null)
            return dragData.draggedAttachment.GetItemType() == itemType;
        return false;
    }
}
