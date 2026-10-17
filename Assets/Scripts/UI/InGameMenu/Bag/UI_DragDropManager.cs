using UnityEngine;
using UnityEngine.UI;

public class UI_DragDropManager : MonoBehaviour {
    private struct DragData {
        public UI_BagItemSlot draggedSlot;
        public UI_BagItemSlot hoveredSlot;
        public bool isAbove;
        public int insertionIndex;
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
        // A visible indicator is a Content child and affects active sibling indexes.
        dragIndicator.Hide();
        dragData.hoverAttachment = slot;
        if (slot != null) {
            dragData.hoveredSlot = slot.GetComponentInParent<UI_BagItemSlot>();
        }

        if (slot != null && IsAttachmentMatch()) {
            dragIndicator.ShowAtAttachment(slot.GetComponent<RectTransform>());
        }
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
        dragData.isMyBagEnd = dragData.isMyBagBegin;
        dragData.insertionIndex = dragData.isMyBagBegin
            ? Mathf.Max(0, ObjectControll.GetIndex(slot))
            : bagManager.bagDataList.Count;
    }

    public void OnAttachmentSlotBeginDrag(UI_AttachmentSlot slot) {
        UI_BagItemSlot weaponSlot = slot.GetComponentInParent<UI_BagItemSlot>();
        if (weaponSlot == null || !bagManager.IsPlayerBagSlot(weaponSlot) || !slot.CurrentAttachment.IsValid) {
            return;
        }

        dragData.draggedSlot = null;
        dragData.draggedAttachment = slot;
        dragData.hoverAttachment = null;
        dragData.hoveredSlot = weaponSlot;
        dragData.isMyBagBegin = true;
        dragData.isMyBagEnd = true;
        dragData.insertionIndex = Mathf.Max(0, bagManager.FindSlotIndex(weaponSlot) + 1);
    }

    public void OnAttachmentSlotRightClick(UI_AttachmentSlot attachmentSlot) {
        if (attachmentSlot == null || !attachmentSlot.CurrentAttachment.IsValid) {
            return;
        }

        UI_BagItemSlot weaponSlot = attachmentSlot.GetComponentInParent<UI_BagItemSlot>();
        if (weaponSlot == null || !bagManager.IsPlayerBagSlot(weaponSlot)) {
            return;
        }

        int weaponIndex = ObjectControll.GetIndex(weaponSlot);
        AttachmentType attachmentType = ItemData.GetAttachmentType(attachmentSlot.GetItemType());
        bagManager.RequestDetachAttachment(weaponIndex, attachmentType);
    }

    public void OnDragEnd() {
        dragIndicator.Hide();

        if (dragData.draggedAttachment != null) {
            if (dragData.isMyBagEnd) {
                if (dragData.hoverAttachment != null) {
                    HandleAttachmentDropInAttachment();
                } else {
                    HandleAttachmentDropInBag();
                }
            } else {
                HandleAttachmentDropOutOfBag();
            }
        } else {
            bool isAttachmentInstall = dragData.isMyBagEnd &&
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
        }

        dragData.draggedSlot = null;
        dragData.draggedAttachment = null;
        dragData.hoverAttachment = null;
        dragData.hoveredSlot = null;
        dragData.insertionIndex = 0;
    }

    public void OnBagItemSlotEnter(UI_BagItemSlot hoveredSlot) {
        dragData.hoveredSlot = hoveredSlot;
    }

    public void OnBagItemSlotHover(Vector2 pointerPosition) {
        if (dragData.draggedSlot == null && dragData.draggedAttachment == null) return;
        if (!dragData.isMyBagEnd) return;
        if (dragData.hoverAttachment != null) return;

        // Restore the slots to their normal positions before checking which
        // half is under the pointer. The indicator is then inserted as a real
        // child of Content at the calculated position.
        dragIndicator.Hide();
        if (contentRoot is RectTransform contentRect) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        UI_BagItemSlot closestSlot = null;
        Vector2 closestPoint = Vector2.zero;
        float closestDistance = float.PositiveInfinity;
        foreach (Transform child in contentRoot) {
            if (!child.gameObject.activeInHierarchy || !child.TryGetComponent(out UI_BagItemSlot slot)) continue;

            RectTransform rect = (RectTransform)child;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, pointerPosition, null,
                    out Vector2 localPoint)) continue;

            float distance = Mathf.Abs(localPoint.y - rect.rect.center.y);
            if (distance < closestDistance) {
                closestDistance = distance;
                closestSlot = slot;
                closestPoint = localPoint;
            }
        }

        if (closestSlot == null) {
            if (bagManager.bagDataList.Count == 0) {
                dragData.insertionIndex = 0;
                dragIndicator.Show(0);
            }
            return;
        }

        dragData.hoveredSlot = closestSlot;
        RectTransform slotRect = closestSlot.GetComponent<RectTransform>();
        dragData.isAbove = closestPoint.y > slotRect.rect.center.y;
        int index = ObjectControll.GetIndex(closestSlot);
        if (index < 0 || index >= bagManager.bagDataList.Count) {
            return;
        }

        dragData.insertionIndex = Mathf.Clamp(
            dragData.isAbove ? index : index + 1,
            0,
            bagManager.bagDataList.Count
        );
        dragIndicator.Show(dragData.insertionIndex);
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

        bagManager.RequestPickup(
            bagManager.nearbyPickupables[pickupableIndex],
            dragData.insertionIndex
        );
    }

    private void HandleSlotDropInBag() {
        if (dragData.draggedSlot == null) {
            return;
        }

        int begin = ObjectControll.GetIndex(dragData.draggedSlot);
        bagManager.RequestMoveSlot(begin, dragData.insertionIndex);
    }

    private void HandleAttachmentDropInSlot() {
        UI_BagItemSlot weaponSlot = dragData.hoverAttachment.GetComponentInParent<UI_BagItemSlot>();
        if (weaponSlot == null || !bagManager.IsPlayerBagSlot(weaponSlot) || !IsAttachmentMatch()) {
            return;
        }

        int weaponIndex = ObjectControll.GetIndex(weaponSlot);
        dragData.hoverAttachment.selectCover.Hide();

        if (dragData.isMyBagBegin) {
            int attachmentIndex = ObjectControll.GetIndex(dragData.draggedSlot);
            AttachmentType attachmentType = bagManager.bagDataList[attachmentIndex].itemData.attachmentType;
            bagManager.RequestInstallAttachment(attachmentIndex, weaponIndex, attachmentType);
        } else {
            int pickupableIndex = ObjectControll.GetIndex(dragData.draggedSlot);
            bagManager.RequestPickupAttachment(bagManager.nearbyPickupables[pickupableIndex], weaponIndex);
        }
    }

    private void HandleAttachmentDropInAttachment() {
        if (!IsAttachmentMatch()) return;

        UI_BagItemSlot sourceWeapon = dragData.draggedAttachment.GetComponentInParent<UI_BagItemSlot>();
        UI_BagItemSlot targetWeapon = dragData.hoverAttachment.GetComponentInParent<UI_BagItemSlot>();
        int sourceIndex = bagManager.FindSlotIndex(sourceWeapon);
        int targetIndex = bagManager.FindSlotIndex(targetWeapon);
        dragData.hoverAttachment.selectCover.Hide();
        bagManager.RequestTransferAttachment(sourceIndex, targetIndex, dragData.draggedAttachment.CurrentAttachment.attachmentType);
    }

    private void HandleAttachmentDropInBag() {
        UI_BagItemSlot sourceWeapon = dragData.draggedAttachment.GetComponentInParent<UI_BagItemSlot>();
        if (!bagManager.IsPlayerBagSlot(sourceWeapon)) return;

        int sourceIndex = bagManager.FindSlotIndex(sourceWeapon);
        bagManager.RequestDetachAttachment(sourceIndex, dragData.draggedAttachment.CurrentAttachment.attachmentType,
            dragData.insertionIndex);
    }

    private void HandleAttachmentDropOutOfBag() {
        UI_BagItemSlot sourceWeapon = dragData.draggedAttachment.GetComponentInParent<UI_BagItemSlot>();
        if (!bagManager.IsPlayerBagSlot(sourceWeapon)) return;

        int sourceIndex = bagManager.FindSlotIndex(sourceWeapon);
        bagManager.RequestDropAttachment(sourceIndex, dragData.draggedAttachment.CurrentAttachment.attachmentType);
    }

    public void Hide() {
        dragIndicator.Hide();
    }

    public bool IsAttachmentMatch() {
        if (dragData.hoverAttachment == null ||
            dragData.hoverAttachment.CurrentAttachment.IsValid) return false;

        UI_BagItemSlot weaponSlot = dragData.hoverAttachment.GetComponentInParent<UI_BagItemSlot>();
        if (weaponSlot == null || !bagManager.IsPlayerBagSlot(weaponSlot)) return false;

        if (dragData.draggedAttachment != null) {
            if (!dragData.isMyBagBegin) return false;
            UI_BagItemSlot sourceWeapon = dragData.draggedAttachment.GetComponentInParent<UI_BagItemSlot>();
            int targetIndex = bagManager.FindSlotIndex(weaponSlot);
            return sourceWeapon != weaponSlot && bagManager.IsPlayerBagSlot(sourceWeapon) &&
                   dragData.draggedAttachment.CurrentAttachment.IsValid &&
                   targetIndex >= 0 && targetIndex < bagManager.bagDataList.Count &&
                   bagManager.bagDataList[targetIndex].IsWeapon;
        }

        if (dragData.draggedSlot == null) return false;
        int weaponIndex = bagManager.FindSlotIndex(weaponSlot);
        if (weaponIndex < 0 || weaponIndex >= bagManager.bagDataList.Count ||
            !bagManager.bagDataList[weaponIndex].IsWeapon) return false;

        ItemData item;
        if (dragData.isMyBagBegin) {
            int index = bagManager.FindSlotIndex(dragData.draggedSlot);
            if (index < 0 || index >= bagManager.bagDataList.Count) return false;
            item = bagManager.bagDataList[index].itemData;
        } else {
            if (!bagManager.IsNearbyPickupSlot(dragData.draggedSlot)) return false;
            int index = ObjectControll.GetIndex(dragData.draggedSlot);
            if (index < 0 || index >= bagManager.nearbyPickupables.Count) return false;
            Pickupable pickupable = bagManager.nearbyPickupables[index];
            if (pickupable == null || !pickupable.HasItem) return false;
            item = pickupable.ItemData;
        }
        return item.IsValid && item.attachmentType != AttachmentType.None;
    }
}
