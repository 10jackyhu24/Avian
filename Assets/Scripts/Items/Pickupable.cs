using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Pickupable : MonoBehaviour {
    private ItemData itemData;
    [SerializeField, Min(1)] private int count = 1;
    private List<AttachmentSlotData> attachments;

    public ItemData ItemData => itemData;
    public bool HasItem => itemData.IsValid;
    public int Count => count;
    public List<AttachmentSlotData> Attachments => attachments;

    public void SetItemData(ItemData value) {
        itemData = value;
    }

    public void SetAttachment(List<AttachmentSlotData> value) {
        attachments = value;
    }

    public void SetCount(int value) {
        count = Mathf.Max(1, value);
    }

    private void Reset() {
        itemData = ItemData.Empty;
    }
}
