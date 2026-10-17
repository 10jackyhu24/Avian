using UnityEngine;
using UnityEngine.UI;

public class UI_DragIndicator : MonoBehaviour {
    [SerializeField] private Image indicatorLine;

    private bool isInitialized;
    private LayoutElement layoutElement;

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

        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null) {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        isInitialized = true;
    }
    
    public void Show(int insertionIndex) {
        EnsureInitialized();

        RestoreLayoutMode();

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

    public void ShowAtAttachment(RectTransform attachmentRect) {
        if (attachmentRect == null) {
            Hide();
            return;
        }

        EnsureInitialized();
        if (transform.parent is not RectTransform contentRect) {
            return;
        }

        gameObject.SetActive(false);
        layoutElement.ignoreLayout = true;
        transform.SetAsLastSibling();

        RectTransform lineRect = (RectTransform)transform;
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(0f, 1f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);

        Vector3 bottomCenter = attachmentRect.TransformPoint(new Vector3(attachmentRect.rect.center.x, attachmentRect.rect.yMin));
        Vector3 localPoint = contentRect.InverseTransformPoint(bottomCenter);
        lineRect.anchoredPosition = new Vector2(localPoint.x - contentRect.rect.xMin,
            localPoint.y - contentRect.rect.yMax);
        lineRect.sizeDelta = new Vector2(attachmentRect.rect.width, 6f);
        gameObject.SetActive(true);
    }

    public void Hide() {
        gameObject.SetActive(false);
        RestoreLayoutMode();
    }

    private void RestoreLayoutMode() {
        EnsureInitialized();
        layoutElement.ignoreLayout = false;
        RectTransform lineRect = (RectTransform)transform;
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta = new Vector2(0f, 6f);
    }

    public void ShowDirectly() {
        EnsureInitialized();
        gameObject.SetActive(true);
    }
}
