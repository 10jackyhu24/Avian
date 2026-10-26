using System;
using System.Collections.Generic;
using Unity.Collections;

internal static class Program {
    private static int checks;
    private static void Near(float actual, float expected, string name) {
        if (Math.Abs(actual - expected) > 0.00001f) throw new Exception($"{name}: expected {expected}, got {actual}");
        checks++;
    }
    private static ItemObject Register(string id, ItemType type, float percent) {
        ItemObject item = new ItemObject { itemId = id, itemName = id, itemType = type, bonusPercent = percent };
        ItemManager.Items[id] = ItemData.FromItemObject(item);
        return item;
    }
    private static void Main() {
        ItemObject speed = Register("attack_speed", ItemType.AttackSpeed, 20f);
        ItemObject damage = Register("damage_bonus", ItemType.DamageBonus, 25f);
        ItemObject critical = Register("critical_rate", ItemType.CriticalRate, 15f);
        AttachmentBonuses plain = default;
        Near(plain.GetCooldown(1f), 1f, "No attachment cooldown");
        Near(plain.GetDamage(40f, 0f), 40f, "No attachment cannot crit");

        InventoryItemState fastWeapon = new InventoryItemState { AttackSpeedAttachmentId = new FixedString64Bytes(speed.itemId) };
        AttachmentBonuses fast = AttachmentBonuses.FromWeapon(fastWeapon);
        Near(fast.GetCooldown(1f), 1f / 1.2f, "20% attack speed");
        Near(fast.GetDamage(40f, 0.5f), 40f, "Speed does not change damage");
        InventoryItemState strongWeapon = new InventoryItemState { DamageBonusAttachmentId = new FixedString64Bytes(damage.itemId) };
        AttachmentBonuses strong = AttachmentBonuses.FromWeapon(strongWeapon);
        Near(strong.GetDamage(40f, 0.5f), 50f, "25% damage");
        Near(strong.GetCooldown(1f), 1f, "Damage does not change speed");
        Near(fast.GetCooldown(1f), 1f / 1.2f, "Other weapon stats remain independent");

        InventoryItemState critWeapon = new InventoryItemState { CriticalRateAttachmentId = new FixedString64Bytes(critical.itemId) };
        AttachmentBonuses crit = AttachmentBonuses.FromWeapon(critWeapon);
        Near(crit.GetDamage(40f, 0.149f), 80f, "Crit roll below 15%");
        Near(crit.GetDamage(40f, 0.15f), 40f, "Crit boundary is exclusive");
        Near(crit.CriticalChance, 0.15f, "Crit percentage points");

        InventoryItemState mismatched = new InventoryItemState { DamageBonusAttachmentId = new FixedString64Bytes(speed.itemId) };
        Near(AttachmentBonuses.FromWeapon(mismatched).GetDamage(40f, 0.5f), 40f, "Reject wrong attachment type");
        Near(AttachmentBonuses.FromWeapon(default).GetCooldown(1f), 1f, "Detaching removes next-attack bonus");
        Near(fast.GetCooldown(1f), 1f / 1.2f, "Existing attack snapshot is retained");

        BagSlotData displayedWeapon = new BagSlotData {
            itemData = ItemData.FromItemObject(new ItemObject { itemType = ItemType.Weapon }), count = 1,
            attachments = new List<AttachmentSlotData> {
                new AttachmentSlotData { requiredType = AttachmentType.AttackSpeed, attachedItem = ItemData.FromItemObject(speed) }
            }
        };
        Near(AttachmentBonuses.FromBagSlot(displayedWeapon).GetCooldown(1f), fast.GetCooldown(1f), "Bag and server agree");
        Near(speed.bonusPercent, 20f, "Shared item asset is unchanged");
        critical.bonusPercent = 100f;
        Near(AttachmentBonuses.FromWeapon(critWeapon).GetDamage(40f, 1f), 80f, "100% chance always crits");
        critical.bonusPercent = float.NaN;
        Near(AttachmentBonuses.FromWeapon(critWeapon).CriticalChance, 0f, "Invalid config is ignored");
        Console.WriteLine($"PASS {checks} checks using the production Item.cs and AttachmentBonuses.cs sources.");
    }
}
