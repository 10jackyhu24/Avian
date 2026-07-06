using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WarningWindow : MonoBehaviour {
    [SerializeField] private GameObject warningWindow;

    [Header("UI Elements")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button closeButton;

    private void Awake() {
        closeButton.onClick.AddListener(CloseWindow);
    }

    private void Start() {
        warningWindow.SetActive(false);
    }

    public void Show(string title, string message) {
        titleText.text = title;
        messageText.text = message;

        warningWindow.SetActive(true);
    }

    public void CloseWindow() {
        warningWindow.SetActive(false);
    }
}