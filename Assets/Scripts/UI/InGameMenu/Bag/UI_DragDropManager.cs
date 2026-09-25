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
        dragData.isMyBagBegin = bagManager.IsPlayerBagSlot(slot);
    }

    public void OnAttachmentSlotBeginDrag(UI_AttachmentSlot slot) {
        Debug.Log("[UI_DragDropManager] Attachment changes are not implemented yet.", this);
    }

    public void OnAttachmentSlotRightClick(UI_AttachmentSlot attachmentSlot) {
        if (attachmentSlot == null || !attachmentSlot.CurrentAttachment.IsValid) {
            return;
        }

        UI_BagItemSlot weaponSlot = attachmentSlot.GetComponentInParent<UI_BagItemSlot>();
        if (weaponSlot == null) {
            return;
        }

        int weaponIndex = ObjectControll.GetIndex(weaponSlot);
        AttachmentType attachmentType = ItemData.GetAttachmentType(attachmentSlot.GetItemType());
        bagManager.RequestDetachAttachment(weaponIndex, attachmentType);
    }

    public void OnDragEnd() {
        dragIndicator.Hide();

        bool isAttachmentInstall = dragData.isMyBagBegin &&
                                   dragData.isMyBagEnd &&
                                   dragData.draggedSlot != null &&
                                   dragData.hoverAttachment != null;

        bool isBagReorder = dragData.isMyBagBegin &&
                            dragData.isMyBagEnd &&
                            dragData.draggedSlot != null &&
                            dragData.hoverAttachment == null;

        bool isWorldPickup = !dragData.isMyBagBegin &&
                             dragData.isMyBagEnd &&
                             dragData.draggedSlot != null;

        if (isAttachmentInstall) {
            HandleAttachmentDropInSlot();
        }
        else if (isBagReorder) {
            HandleSlotDropInBag();
        }
        else if (isWorldPickup) {
            HandlePickupIntoBag();
        }
        else if (dragData.isMyBagBegin && !dragData.isMyBagEnd && dragData.draggedSlot != null) {
            HandleDropOutOfBag();
        }

        dragData.draggedSlot = null;
        dragData.draggedAttachment = null;
        dragData.hoverAttachment = null;
        dragData.hoveredSlot = null;
    }

    public void OnBagItemSlotEnter(UI_BagItemSlot hoveredSlot) {
        dragData.hoveredSlot = hoveredSlot;
    }

    public void OnBagItemSlotHover(Vector2 pointerPosition) {
        if (dragData.draggedSlot == null || dragData.hoveredSlot == null) return;
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
        int slotIndex = ObjectControll.GetIndex(dragData.draggedSlot);
        if (slotIndex >= 0 && slotIndex < bagManager.bagDataList.Count) {
            bagManager.RequestDropSlot(slotIndex);
        }
    }

    private void HandlePickupIntoBag() {
        int pickupableIndex = ObjectControll.GetIndex(dragData.draggedSlot);
        if (pickupableIndex < 0 || pickupableIndex >= bagManager.nearbyPickupables.Count) {
            return;
        }

        bagManager.RequestPickup(bagManager.nearbyPickupables[pickupableIndex]);
    }

    private void HandleSlotDropInBag() {
        if (dragData.draggedSlot == null || dragData.hoveredSlot == null) {
            return;
        }

        int begin = ObjectControll.GetIndex(dragData.draggedSlot);
        int end = ObjectControll.GetIndex(dragData.hoveredSlot);
        int insertionIndex = dragData.isAbove ? end : end + 1;
        bagManager.RequestMoveSlot(begin, insertionIndex);
    }

    private void HandleAttachmentDropInSlot() {
        UI_BagItemSlot weaponSlot = dragData.hoverAttachment.GetComponentInParent<UI_BagItemSlot>();
        if (weaponSlot == null) {
            return;
        }

        int attachmentIndex = ObjectControll.GetIndex(dragData.draggedSlot);
        int weaponIndex = ObjectControll.GetIndex(weaponSlot);
        AttachmentType attachmentType = ItemData.GetAttachmentType(dragData.hoverAttachment.GetItemType());

        dragData.hoverAttachment.selectCover.Hide();
        bagManager.RequestInstallAttachment(attachmentIndex, weaponIndex, attachmentType);
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
        if (dragData.draggedAttachment != null) return false;
        if (dragData.draggedSlot != null)
            return bagManager.bagDataList[FindSlotIndex(dragData.draggedSlot)].itemData.itemType == itemType;
        if (dragData.hoverAttachment != null)
            return dragData.draggedAttachment.GetItemType() == itemType;
        return false;
    }
}
