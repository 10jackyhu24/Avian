using UnityEngine;
using UnityEngine.UI;

public class UI_DragIndicator : MonoBehaviour {
    [SerializeField] private Image indicatorLine;

    private bool isInitialized;

    private void Awake() {
        EnsureInitialized();
    }

    private void EnsureInitialized() {
        if (isInitialized) {
            return;
        }

        if (indicatorLine != null) {
            indicatorLine.raycastTarget = false;
        }

        isInitialized = true;
    }
    
    public void Show(int insertionIndex) {
        EnsureInitialized();

        Transform parentTransform = transform.parent;
        if (parentTransform == null) {
            return;
        }

        int siblingIndex = Mathf.Clamp(insertionIndex, 0, parentTransform.childCount - 1);
        transform.SetSiblingIndex(siblingIndex);
        gameObject.SetActive(true);

        if (parentTransform is RectTransform contentRect) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    public void ShowDirectly() {
        EnsureInitialized();
        gameObject.SetActive(true);
    }
}
