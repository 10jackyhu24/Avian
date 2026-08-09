using UnityEngine;
using UnityEngine.UI;

public class UI_DragIndicator : MonoBehaviour {
    [SerializeField] private Image indicatorLine;

    private void Awake() {
        Hide();
    }
    
    public void Show(int index) {
        gameObject.SetActive(true);
        ObjectControll.MoveToIndex(transform, index);
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    public void ShowDirectly() {
            gameObject.SetActive(true);
    }
}
