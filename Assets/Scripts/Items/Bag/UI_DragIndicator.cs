using UnityEngine;
using UnityEngine.UI;

public class UI_DragIndicator : MonoBehaviour {
    [SerializeField] private Image indicatorLine;
    private RectTransform rectTransform;

    private void Awake() {
        rectTransform = GetComponent<RectTransform>();
        Hide();
    }
    
    public void Show(RectTransform targetSlot, bool isAbove) {
        if (targetSlot == null) {
            Hide();
            return;
        }

        gameObject.SetActive(true);

        Transform targetParent = targetSlot.parent;
        if (targetParent != null && transform.parent != targetParent) {
            transform.SetParent(targetParent, false);
        }

        int targetIndex = targetSlot.GetSiblingIndex();
        MoveObject.MoveToIndex(transform, isAbove ? targetIndex : targetIndex + 1);

        if (indicatorLine != null) {
            indicatorLine.raycastTarget = false;
        }
    }

    public void Hide() {
        gameObject.SetActive(false);
    }
}
