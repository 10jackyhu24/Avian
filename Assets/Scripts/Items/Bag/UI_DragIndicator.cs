using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a line indicating where items will be inserted during drag operations
/// </summary>
public class UI_DragIndicator : MonoBehaviour {
    [SerializeField] private Image indicatorLine;
    private RectTransform rectTransform;

    private void Awake() {
        rectTransform = GetComponent<RectTransform>();
        Hide();
    }

    /// <summary>
    /// Shows the indicator at the specified position (above or below target slot)
    /// </summary>
    public void Show(RectTransform targetSlot, bool isAbove) {
        gameObject.SetActive(true);

        // Position the line at the top or bottom of the target slot
        float yOffset = isAbove ? (targetSlot.rect.height / 2f) : -(targetSlot.rect.height / 2f);
        
        Vector3 targetPos = targetSlot.position;
        targetPos.y += yOffset;
        
        rectTransform.position = targetPos;
        
        // Make the line span the width of the target slot
        rectTransform.sizeDelta = new Vector2(targetSlot.rect.width, 2f);
    }

    public void Hide() {
        gameObject.SetActive(false);
    }
}
