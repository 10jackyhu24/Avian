// Minimal managed substitutes for running the real item/modifier code without a Unity player.
using System;
using System.Collections.Generic;

namespace UnityEngine {
    public class ScriptableObject { }
    public class Sprite { }
    public class GameObject { }
    public class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    public class TextAreaAttribute : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public static class Mathf {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float value, float minimum, float maximum) => Math.Clamp(value, minimum, maximum);
        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
}

namespace Unity.Collections {
    public struct FixedString64Bytes {
        private string value;
        public FixedString64Bytes(string text) { value = text; }
        public int Length => value?.Length ?? 0;
        public override string ToString() => value ?? string.Empty;
    }
}

public class AttackData { }
public class ItemEffect { }
public struct InventoryItemState {
    public Unity.Collections.FixedString64Bytes AttackSpeedAttachmentId;
    public Unity.Collections.FixedString64Bytes DamageBonusAttachmentId;
    public Unity.Collections.FixedString64Bytes CriticalRateAttachmentId;
}
public static class ItemManager {
    public static readonly Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>();
    public static bool TryGetItemById(string id, out ItemData item) => Items.TryGetValue(id, out item);
}
