using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_BagItemSlot : MonoBehaviour {
    [Header("UI Component References")]
    [SerializeField] private Image itemIcon;                 // Brown box (Icon)
    [SerializeField] private TextMeshProUGUI weightText;     // Black text (Single slot weight)
    [SerializeField] private TextMeshProUGUI countText;      // Green text ("1/1" or "3/64")
    [SerializeField] private GameObject attachmentContainer; // Parent object for orange boxes

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
    [SerializeField] private List<UI_AttachmentSlot> attachmentSlots = new List<UI_AttachmentSlot>(); // Left, Middle, Right orange boxes


    private void Awake() {
        BindReferences();
    }

    private void OnValidate() {
        BindReferences();
    }

    public void UpdateSlotUI(BagSlotData slotData) {
        BindReferences();

        if (slotData == null || slotData.IsEmpty) {
            SetEmptyState();
            return;
        }

        // Render Main Item Info
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
            weightText.text = slotData.GetTotalWeight().ToString();
        }

        // Render green count text
        if (countText != null) {
            int maxCount = Mathf.Max(1, slotData.itemData.itemObject.maxCount);
            countText.text = $"{slotData.GetDisplayCount()}/{maxCount}";
        }

        // Render Orange Attachment Boxes (Weapons Only)
        if (slotData.IsWeapon) {
            attachmentContainer.SetActive(true);

            for (int i = 0; i < attachmentSlots.Count; i++) {
                if (attachmentSlots[i] == null) {
                    continue;
                }
                var attachData = slotData.attachments[i];
                attachmentSlots[i].gameObject.SetActive(true);
                attachmentSlots[i].SetupSlot(attachData.requiredType, attachData.attachedItem);
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
        attachmentContainer.SetActive(false);

        HideAttachmentSlots();
    }

    private void HideAttachmentSlots() {
        for (int i = 0; i < attachmentSlots.Count; i++) {
            if (attachmentSlots[i] != null) {
                attachmentSlots[i].gameObject.SetActive(false);
            }
        }
    }

    private void BindReferences() {
        itemIcon = gameObject.FindComponentInChildren<Image>(iconChildName);
        weightText = gameObject.FindComponentInChildren<TextMeshProUGUI>(weightTextChildName);
        countText = gameObject.FindComponentInChildren<TextMeshProUGUI>(countTextChildName);

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
}
