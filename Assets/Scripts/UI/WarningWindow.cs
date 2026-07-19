using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class WarningWindow : MonoBehaviour {
    [SerializeField] private GameObject parent;
    [SerializeField] private GameObject warningWindowPrefab;

    private TMP_Text titleText;
    private TMP_Text messageText;
    private Button closeButton;
    private GameObject windowInstance;

    public void Show(string title, string message) {
        if (parent == null) {
            Debug.LogError("[WarningWindow] Parent is not assigned.", this);
            return;
        }

        if (warningWindowPrefab == null) {
            Debug.LogError("[WarningWindow] Warning window prefab is not assigned.", this);
            return;
        }

        if (windowInstance != null) {
            Destroy(windowInstance);
        }

        windowInstance = PrefabSpawn.SpawnInit(warningWindowPrefab, parent.transform);
        if (windowInstance == null) {
            return;
        }

        titleText = FindComponentInChildrenByName<TMP_Text>(windowInstance.transform, "TitleBar_Text");
        messageText = FindComponentInChildrenByName<TMP_Text>(windowInstance.transform, "Content_Text");
        closeButton = FindComponentInChildrenByName<Button>(windowInstance.transform, "Close_Button");

        if (titleText == null || messageText == null || closeButton == null) {
            Debug.LogError("[WarningWindow] Missing UI references in warning window prefab.", windowInstance);
            Destroy(windowInstance);
            windowInstance = null;
            return;
        }

        titleText.text = title;
        messageText.text = message;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(CloseWindow);
        windowInstance.SetActive(true);
    }

    public void CloseWindow() {
        if (windowInstance == null) {
            return;
        }

        Destroy(windowInstance);
        windowInstance = null;
    }

    private static T FindComponentInChildrenByName<T>(Transform root, string targetName) where T : Component {
        if (root == null) {
            return null;
        }

        foreach (T component in root.GetComponentsInChildren<T>(true)) {
            if (component != null && string.Equals(component.gameObject.name, targetName, StringComparison.Ordinal)) {
                return component;
            }
        }

        return null;
    }
}
