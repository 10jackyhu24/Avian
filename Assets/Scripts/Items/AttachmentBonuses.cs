using Unity.Collections;
using UnityEngine;

/// <summary>Values for one equipped weapon, resolved from its actual attachment IDs.</summary>
public struct AttachmentBonuses {
    public float AttackSpeed;
    public float Damage;
    public float CriticalChance;

    public float GetCooldown(float baseCooldown) => Mathf.Max(0f, baseCooldown) / (1f + AttackSpeed);
    public float GetBaseDamage(float baseDamage) => Mathf.Max(0f, baseDamage) * (1f + Damage);

    // The server supplies the roll once per attack so a sweep/projectile keeps its snapshot.
    public float GetDamage(float baseDamage, float criticalRoll) => GetBaseDamage(baseDamage) *
        (CriticalChance >= 1f || criticalRoll < CriticalChance ? 2f : 1f);

    public static AttachmentBonuses FromWeapon(InventoryItemState weapon) {
        AttachmentBonuses result = default;
        result.Add(weapon.AttackSpeedAttachmentId, AttachmentType.AttackSpeed);
        result.Add(weapon.DamageBonusAttachmentId, AttachmentType.DamageBonus);
        result.Add(weapon.CriticalRateAttachmentId, AttachmentType.CriticalRate);
        return result;
    }

    public static AttachmentBonuses FromBagSlot(BagSlotData weapon) {
        AttachmentBonuses result = default;
        if (weapon == null || !weapon.IsWeapon || weapon.attachments == null) return result;
        foreach (AttachmentSlotData slot in weapon.attachments) {
            if (slot != null && !slot.IsEmpty) result.Add(slot.attachedItem.itemObject);
        }
        return result;
    }

    private void Add(FixedString64Bytes id, AttachmentType expectedType) {
        if (id.Length > 0 && ItemManager.TryGetItemById(id.ToString(), out ItemData item) &&
            item.attachmentType == expectedType) Add(item.itemObject);
    }

    private void Add(ItemObject item) {
        if (item == null || !float.IsFinite(item.bonusPercent)) return;
        float value = Mathf.Clamp(item.bonusPercent, 0f, 1000f) * 0.01f;
        switch (item.itemType) {
            case ItemType.AttackSpeed: AttackSpeed += value; break;
            case ItemType.DamageBonus: Damage += value; break;
            case ItemType.CriticalRate: CriticalChance = Mathf.Clamp01(CriticalChance + value); break;
        }
    }
}
