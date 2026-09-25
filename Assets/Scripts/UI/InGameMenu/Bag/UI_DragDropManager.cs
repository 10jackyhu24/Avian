using System.Runtime.CompilerServices;
using System.Collections;
using Unity.VisualScripting;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

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
        if (!dragData.isMyBagEnd) {
            Debug.Log("[UI_DragDropManager] World pickup is not implemented yet.", this);
            return;
        }

        dragData.draggedAttachment = null;
        dragData.hoverAttachment = null;
        dragData.draggedSlot = slot;
        dragData.hoveredSlot = slot;
        dragData.isMyBagBegin = dragData.isMyBagEnd;
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

        if (isAttachmentInstall) {
            HandleAttachmentDropInSlot();
        }
        else if (isBagReorder) {
            HandleSlotDropInBag();
        }
        else if (dragData.draggedSlot != null) {
            Debug.Log("[UI_DragDropManager] World drop and pickup are not implemented yet.", this);
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
        PlayerController player = FindFirstObjectByType<PlayerController>();
        Vector3 spawnPosition = player.transform.position;

        if (dragData.draggedSlot != null) {
            int slotIndex = ObjectControll.GetIndex(dragData.draggedSlot);
            if (slotIndex >= 0 && slotIndex < bagManager.bagDataList.Count) {
                ItemManager.AddItemInScene(bagManager.bagDataList[slotIndex], spawnPosition, Quaternion.Euler(90f, 0f, 90f));
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
            BagSlotData slotData = new BagSlotData {
                itemData = dragData.draggedAttachment.CurrentAttachment,
                count = 1
            };
            ItemManager.AddItemInScene(slotData, spawnPosition, Quaternion.Euler(90f, 0f, 90f));
            bagManager.ClearAttachmentItem(slotIndex, ItemData.GetAttachmentType(dragData.draggedAttachment.GetItemType()));
            return;
        }
    }

    private void HandleDropIntoBag() {
        if (dragData.draggedSlot != null) {
            int pickupableIndex = ObjectControll.GetIndex(dragData.draggedSlot);
            if (pickupableIndex < 0 || pickupableIndex >= bagManager.nearbyPickupables.Count) {
                return;
            }

            Pickupable pickupable = bagManager.nearbyPickupables[pickupableIndex];
            if (pickupable == null || !pickupable.HasItem) {
                return;
            }

            BagSlotData slotData = pickupable.ToBagSlotData();
            int insertIndex = dragData.hoveredSlot != null
                ? ObjectControll.GetIndex(dragData.hoveredSlot)
                : bagManager.bagDataList.Count;

            int beforeCount = bagManager.bagDataList.Count;
            bagManager.AddItem(slotData.itemData, slotData.count, dragData.isAbove ? insertIndex : insertIndex + 1, slotData.attachments?.ConvertAll(a => a.requiredType).ToArray());
            if (bagManager.bagDataList.Count > beforeCount) {
                int addedIndex = dragData.isAbove ? insertIndex : insertIndex + 1;
                BagSlotData addedSlot = bagManager.bagDataList[addedIndex];
                addedSlot.attachments = slotData.attachments;
                bagManager.RebuildSlots();
                RemovePickupableFromScene(pickupable);
            }
        }
        if (dragData.draggedAttachment != null) {
            UI_BagItemSlot parentSlot = dragData.draggedAttachment.GetComponentInParent<UI_BagItemSlot>();

            int pickupableIndex = ObjectControll.GetIndex(parentSlot);
            if (pickupableIndex < 0 || pickupableIndex >= bagManager.nearbyPickupables.Count) {
                return;
            }

            Pickupable pickupable = bagManager.nearbyPickupables[pickupableIndex];
            if (pickupable == null || !pickupable.HasItem) {
                return;
            }

            ItemData attachmentItem = dragData.draggedAttachment.CurrentAttachment;
            if (!attachmentItem.IsValid) {
                return;
            }

            AttachmentType attachmentType = ItemData.GetAttachmentType(dragData.draggedAttachment.GetItemType());
            ItemManager.ClearAttachmentItemInScene(pickupable, attachmentType);

            int insertIndex = dragData.hoveredSlot != null
                ? ObjectControll.GetIndex(dragData.hoveredSlot)
                : bagManager.bagDataList.Count;

            bagManager.AddItem(
                attachmentItem,
                1,
                dragData.isAbove ? insertIndex : insertIndex + 1
            );
        }
    }

    private void RemovePickupableFromScene(Pickupable pickupable) {
        if (pickupable == null) {
            return;
        }

        NetworkObject networkObject = pickupable.GetComponentInParent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned) {
            networkObject.Despawn(true);
            return;
        }

        GameObject targetObject = pickupable.gameObject;
        if (pickupable.transform.parent != null && pickupable.GetComponent<NetworkObject>() == null) {
            targetObject = pickupable.transform.root.gameObject;
        }

        Destroy(targetObject);
    }

    private IEnumerator RefreshPickupablesNextFrame() {
        yield return null;
        bagManager.RefreshNearbyPickupables();
        bagManager.RefreshNearbyPickupablesUI();
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
