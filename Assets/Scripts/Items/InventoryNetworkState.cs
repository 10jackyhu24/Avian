using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// Network-safe state for one item instance. ScriptableObject references are
/// deliberately excluded; each client resolves ItemId through ItemManager.
/// </summary>
[Serializable]
public struct InventoryItemState : INetworkSerializable, IEquatable<InventoryItemState> {
    public ulong InstanceId;
    public FixedString64Bytes ItemId;
    public int Count;
    public FixedString64Bytes AttackSpeedAttachmentId;
    public FixedString64Bytes DamageBonusAttachmentId;
    public FixedString64Bytes CriticalRateAttachmentId;

    public bool IsValid => InstanceId != 0 && ItemId.Length > 0 && Count > 0;

    public static InventoryItemState Empty => default;

    public FixedString64Bytes GetAttachmentId(AttachmentType attachmentType) {
        return attachmentType switch {
            AttachmentType.AttackSpeed => AttackSpeedAttachmentId,
            AttachmentType.DamageBonus => DamageBonusAttachmentId,
            AttachmentType.CriticalRate => CriticalRateAttachmentId,
            _ => default
        };
    }

    public void SetAttachmentId(AttachmentType attachmentType, FixedString64Bytes attachmentItemId) {
        switch (attachmentType) {
            case AttachmentType.AttackSpeed:
                AttackSpeedAttachmentId = attachmentItemId;
                break;
            case AttachmentType.DamageBonus:
                DamageBonusAttachmentId = attachmentItemId;
                break;
            case AttachmentType.CriticalRate:
                CriticalRateAttachmentId = attachmentItemId;
                break;
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref InstanceId);
        serializer.SerializeValue(ref ItemId);
        serializer.SerializeValue(ref Count);
        serializer.SerializeValue(ref AttackSpeedAttachmentId);
        serializer.SerializeValue(ref DamageBonusAttachmentId);
        serializer.SerializeValue(ref CriticalRateAttachmentId);
    }

    public bool Equals(InventoryItemState other) {
        return InstanceId == other.InstanceId &&
               ItemId.Equals(other.ItemId) &&
               Count == other.Count &&
               AttackSpeedAttachmentId.Equals(other.AttackSpeedAttachmentId) &&
               DamageBonusAttachmentId.Equals(other.DamageBonusAttachmentId) &&
               CriticalRateAttachmentId.Equals(other.CriticalRateAttachmentId);
    }

    public override bool Equals(object obj) {
        return obj is InventoryItemState other && Equals(other);
    }

    public override int GetHashCode() {
        unchecked {
            int hashCode = InstanceId.GetHashCode();
            hashCode = (hashCode * 397) ^ ItemId.GetHashCode();
            hashCode = (hashCode * 397) ^ Count;
            hashCode = (hashCode * 397) ^ AttackSpeedAttachmentId.GetHashCode();
            hashCode = (hashCode * 397) ^ DamageBonusAttachmentId.GetHashCode();
            hashCode = (hashCode * 397) ^ CriticalRateAttachmentId.GetHashCode();
            return hashCode;
        }
    }
}

/// <summary>
/// Associates an item instance with its current ordered inventory slot.
/// </summary>
[Serializable]
public struct InventorySlotState : INetworkSerializable, IEquatable<InventorySlotState> {
    public int SlotIndex;
    public InventoryItemState Item;

    public bool IsEmpty => !Item.IsValid;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref SlotIndex);
        serializer.SerializeValue(ref Item);
    }

    public bool Equals(InventorySlotState other) {
        return SlotIndex == other.SlotIndex && Item.Equals(other.Item);
    }

    public override bool Equals(object obj) {
        return obj is InventorySlotState other && Equals(other);
    }

    public override int GetHashCode() {
        unchecked {
            return (SlotIndex * 397) ^ Item.GetHashCode();
        }
    }
}
