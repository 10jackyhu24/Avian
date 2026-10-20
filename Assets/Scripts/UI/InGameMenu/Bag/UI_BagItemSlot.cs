using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UI_BagItemSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler {
    [Header("UI Component References")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI weightText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI effectText;
    [SerializeField] private GameObject attachmentContainer;

    [Header("Auto Bind Names")]
    [SerializeField] private string iconChildName = "Icon";
    [SerializeField] private string weightTextChildName = "Weight";
    [SerializeField] private string countTextChildName = "Count";
    [SerializeField] private string attachmentContainerChildName = "Attachment";

    [Header("Attachment Slots")]
    [SerializeField] private List<string> attachmentSlotChildNames = new List<string> {
        "AttackSpeed",
        "DamageBonus",
        "CriticalRate"
    };
    public List<UI_AttachmentSlot> attachmentSlots = new List<UI_AttachmentSlot>();

    private UI_DragDropManager dragDropManager;

    private SelectCover selectCover;

    private void Awake() {
        BindReferences();
        dragDropManager = FindFirstObjectByType<UI_DragDropManager>();
        selectCover = GetComponent<SelectCover>();
        selectCover.Init(gameObject);
    }

    public Sprite GetIcon() {
        return itemIcon.sprite;
    }

    public void UpdateSlotUI(BagSlotData slotData) {
        BindReferences();

        if (slotData == null || slotData.IsEmpty) {
            SetEmptyState();
            return;
        }

        // Render Main Item Info
        ItemObject item = slotData.itemData.itemObject;
        if (itemNameText != null) itemNameText.text = item.DisplayName;
        if (effectText != null) effectText.text = GetItemDetails(slotData);
        if (itemIcon != null) {
            Sprite iconSprite = slotData.itemData.itemObject.icon;
            itemIcon.enabled = iconSprite != null;
            itemIcon.sprite = iconSprite;

            if (iconSprite != null) {
                itemIcon.preserveAspect = true;
            }
        }

        // Render black weight text
        if (weightText != null) {
            weightText.text = $"Weight: {slotData.GetTotalWeight()}";
        }

        // Render green count text
        if (countText != null) {
            int maxCount = Mathf.Max(1, slotData.itemData.itemObject.maxCount);
            countText.text = $"{slotData.GetDisplayCount()} / {maxCount}";
        }

        // Render Orange Attachment Boxes (Weapons Only)
        if (slotData.IsWeapon) {
            attachmentContainer.SetActive(true);

            // Keep the typed data for network/drop compatibility, but expose one shared slot.
            AttachmentSlotData displayedAttachment = slotData.attachments.Find(slot => slot != null && !slot.IsEmpty);
            for (int i = 0; i < attachmentSlots.Count; i++) {
                if (attachmentSlots[i] == null) {
                    continue;
                }
                attachmentSlots[i].gameObject.SetActive(i == 0);
                attachmentSlots[i].BindAttachmentSlotData(i == 0 ? displayedAttachment : null);
            }
        } else {
            attachmentContainer.SetActive(false);
            HideAttachmentSlots();
        }
    }

    private void SetEmptyState() {
        itemIcon.enabled = false;
        itemIcon.sprite = null;
        weightText.text = string.Empty;
        countText.text = string.Empty;
        if (itemNameText != null) itemNameText.text = string.Empty;
        if (effectText != null) effectText.text = string.Empty;
        attachmentContainer.SetActive(false);

        HideAttachmentSlots();
    }

    private void HideAttachmentSlots() {
        for (int i = 0; i < attachmentSlots.Count; i++) {
            if (attachmentSlots[i] != null) {
                attachmentSlots[i].gameObject.SetActive(false);
                attachmentSlots[i].BindAttachmentSlotData(null);
            }
        }
    }

    private void BindReferences() {
        itemIcon = gameObject.FindComponentInChildren<Image>(iconChildName);
        weightText = gameObject.FindComponentInChildren<TextMeshProUGUI>(weightTextChildName);
        countText = gameObject.FindComponentInChildren<TextMeshProUGUI>(countTextChildName);
        EnsureInformationLabels();

        attachmentSlots.Clear();
        foreach (string childName in attachmentSlotChildNames) {
            if (string.IsNullOrWhiteSpace(childName)) {
                continue;
            }

            UI_AttachmentSlot slot = attachmentContainer.FindComponentInChildren<UI_AttachmentSlot>(childName);
            if (slot != null) {
                attachmentSlots.Add(slot);
            }
        }
    }

    private static string GetItemDetails(BagSlotData slot) {
        ItemObject item = slot.itemData.itemObject;
        if (item.IsAttachment) return $"{item.EffectSummary}\n{item.description}";
        if (!item.IsWeapon || item.attackData == null) {
            return string.IsNullOrWhiteSpace(item.description) ? item.itemType.ToString() : item.description;
        }

        AttachmentBonuses bonuses = AttachmentBonuses.FromBagSlot(slot);
        string stats = $"Damage: {bonuses.GetBaseDamage(item.attackData.damage):0.#} | " +
            $"Cooldown: {bonuses.GetCooldown(item.attackData.cooldown):0.##}s";
        AttachmentSlotData attachment = slot.attachments?.Find(value => value != null && !value.IsEmpty);
        return attachment != null
            ? $"{stats}\n{attachment.attachedItem.itemObject.DisplayName}: {attachment.attachedItem.itemObject.EffectSummary}"
            : $"{stats}\nWeapon attachment slot: empty";
    }

    private void EnsureInformationLabels() {
        if (itemNameText == null) itemNameText = CreateInformationLabel("ItemName");
        if (effectText == null) effectText = CreateInformationLabel("ItemEffect");
        LayoutText(itemNameText, new Vector2(0.23f, 0.69f), new Vector2(0.98f, 0.97f), 30f);
        LayoutText(effectText, new Vector2(0.23f, 0.24f), new Vector2(0.98f, 0.69f), 21f);
        if (itemNameText != null) itemNameText.fontStyle = FontStyles.Bold;
        if (effectText != null) effectText.color = new Color(0.82f, 0.96f, 0.87f);
        LayoutText(weightText, new Vector2(0.23f, 0.01f), new Vector2(0.65f, 0.23f), 22f);
        LayoutText(countText, new Vector2(0.67f, 0.01f), new Vector2(0.98f, 0.23f), 22f);
        if (countText != null) countText.alignment = TextAlignmentOptions.MidlineRight;

        if (itemIcon != null) {
            LayoutRect(itemIcon.rectTransform, new Vector2(0.025f, 0.28f), new Vector2(0.205f, 0.96f));
            itemIcon.preserveAspect = true;
        }
        Transform iconBackground = transform.Find("Icon_Background");
        if (iconBackground is RectTransform backgroundRect) {
            LayoutRect(backgroundRect, new Vector2(0.025f, 0.28f), new Vector2(0.205f, 0.96f));
        }
        if (attachmentContainer != null && attachmentContainer.transform is RectTransform attachmentRect) {
            LayoutRect(attachmentRect, new Vector2(0.09f, 0.13f), new Vector2(0.09f, 0.13f));
            attachmentRect.sizeDelta = new Vector2(40f, 40f);
            // The first typed slot is the single visible, shared attachment slot.
            Transform firstSlot = attachmentContainer.transform.Find("AttackSpeed");
            if (firstSlot is RectTransform slotRect) {
                LayoutRect(slotRect, Vector2.one * 0.5f, Vector2.one * 0.5f);
                slotRect.sizeDelta = new Vector2(40f, 40f);
            }
        }
    }

    private TextMeshProUGUI CreateInformationLabel(string labelName) {
        Transform existing = transform.Find(labelName);
        if (existing != null) return existing.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI label;
        if (weightText != null) {
            label = Instantiate(weightText, transform);
            label.name = labelName;
        } else {
            GameObject labelObject = new GameObject(labelName, typeof(RectTransform));
            labelObject.transform.SetParent(transform, false);
            label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
        }
        label.raycastTarget = false;
        label.text = string.Empty;
        return label;
    }

    private static void LayoutText(TextMeshProUGUI label, Vector2 minimum, Vector2 maximum, float fontSize) {
        if (label == null) return;
        LayoutRect(label.rectTransform, minimum, maximum);
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = fontSize;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
    }

    private static void LayoutRect(RectTransform rect, Vector2 minimum, Vector2 maximum) {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    #region Drag Drop Handlers

    public void OnBeginDrag(PointerEventData eventData) {
        selectCover.Show();
        dragDropManager.OnBagItemSlotBeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData) {
        dragDropManager.OnBagItemSlotHover(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData) {
        selectCover.Hide();
        dragDropManager.OnDragEnd();
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (eventData.dragging) {
            dragDropManager.OnBagItemSlotEnter(this);
        }
    }

    #endregion
}
