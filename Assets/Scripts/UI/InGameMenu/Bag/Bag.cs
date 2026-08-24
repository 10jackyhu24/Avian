using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Bag : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    private UI_DragDropManager dragDropManager;

    private void Awake() {
        dragDropManager = FindFirstObjectByType<UI_DragDropManager>();
    }

    public void OnPointerEnter(PointerEventData eventData) {
        dragDropManager.SetBag(true);
    }

    public void OnPointerExit(PointerEventData eventData) {
        dragDropManager.SetBag(false);
        dragDropManager.Hide();
    }
}