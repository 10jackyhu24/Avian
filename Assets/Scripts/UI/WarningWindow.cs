using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WarningWindow : MonoBehaviour {
    [SerializeField] private GameObject parent;
    [SerializeField] private GameObject warningWindow;

    public void Show(string title, string message) {
        CreateWindow(title, message);
    }

    public GameObject ShowConfirmation(string title, string message, Action onConfirm, Action onCancel = null, string confirmLabel = "Confirm") {
        GameObject window = CreateWindow(title, message);
        if (window == null) {
            return null;
        }
        AddModalBlocker(window);

        TMP_Text content = window.FindComponentInChildren<TMP_Text>("Content_Text");
        content.richText = false;
        content.rectTransform.anchoredPosition = new Vector2(5f, -5f);
        content.rectTransform.sizeDelta = new Vector2(content.rectTransform.sizeDelta.x, 215f);

        Button closeButton = window.FindComponentInChildren<Button>("Close_Button");
        Button cancelButton = CreateDialogButton(closeButton, "Cancel_Button", "Cancel", new Vector2(-160f, -165f), new Color(0.35f, 0.35f, 0.35f));
        Button confirmButton = CreateDialogButton(closeButton, "Confirm_Button", confirmLabel, new Vector2(160f, -165f), new Color(0.72f, 0.18f, 0.18f));
        bool resolved = false;

        void Resolve(bool confirmed) {
            if (resolved) {
                return;
            }
            resolved = true;
            CloseWindow(window);
            if (confirmed) {
                onConfirm?.Invoke();
            } else {
                onCancel?.Invoke();
            }
        }

        closeButton.onClick = new Button.ButtonClickedEvent();
        closeButton.onClick.AddListener(() => Resolve(false));
        cancelButton.onClick.AddListener(() => Resolve(false));
        confirmButton.onClick.AddListener(() => Resolve(true));
        cancelButton.Select();
        return window;
    }

    public GameObject ShowProgress(string title, string message) {
        GameObject window = CreateWindow(title, message);
        if (window != null) {
            AddModalBlocker(window);
            window.FindComponentInChildren<Button>("Close_Button").interactable = false;
        }
        return window;
    }

    private GameObject CreateWindow(string title, string message) {
        if (parent == null || warningWindow == null) {
            Debug.LogError("WarningWindow requires a parent and window prefab.", this);
            return null;
        }
        GameObject spawnPrefab = PrefabSpawn.SpawnInit(warningWindow, parent.transform);
        TMP_Text titleText = spawnPrefab.FindComponentInChildren<TMP_Text>("TitleBar_Text");
        TMP_Text messageText = spawnPrefab.FindComponentInChildren<TMP_Text>("Content_Text");
        Button closeButton = spawnPrefab.FindComponentInChildren<Button>("Close_Button");

        titleText.text = title;
        messageText.text = message;
        closeButton.onClick.AddListener(() => CloseWindow(spawnPrefab));
        spawnPrefab.SetActive(true);
        return spawnPrefab;
    }

    private void AddModalBlocker(GameObject window) {
        Canvas canvas = window.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 100);

        GameObject backdrop = new GameObject("Modal_Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = backdrop.GetComponent<RectTransform>();
        rect.SetParent(window.transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
    }

    private Button CreateDialogButton(Button template, string name, string label, Vector2 position, Color color) {
        Button button = Instantiate(template, template.transform.parent);
        button.name = name;
        button.onClick = new Button.ButtonClickedEvent();
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(280f, 64f);
        button.GetComponent<Image>().color = color;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        text.text = label;
        text.color = Color.white;
        text.fontSize = 28f;
        text.fontSizeMin = 18f;
        text.fontSizeMax = 28f;
        text.enableAutoSizing = true;
        return button;
    }

    public void CloseWindow(GameObject windowInstance) {
        if (windowInstance != null) {
            windowInstance.SetActive(false);
            Destroy(windowInstance);
        }
    }
}
