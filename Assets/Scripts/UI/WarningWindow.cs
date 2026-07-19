using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WarningWindow : MonoBehaviour {
    [SerializeField] private GameObject parent;
    [SerializeField] private GameObject warningWindow;

    private TMP_Text titleText;
    private TMP_Text messageText;
    private Button closeButton;

    public void Show(string title, string message) {
        GameObject spawnPrefab = PrefabSpawn.SpawnInit(warningWindow, parent.transform);
        titleText = spawnPrefab.FindComponentInChildren<TMP_Text>("TitleBar_Text");
        messageText = spawnPrefab.FindComponentInChildren<TMP_Text>("Content_Text");
        closeButton = spawnPrefab.FindComponentInChildren<Button>("Close_Button");

        titleText.text = title;
        messageText.text = message;
        closeButton.onClick.AddListener(() => CloseWindow(spawnPrefab));
        spawnPrefab.SetActive(true);
    }

    public void CloseWindow(GameObject windowInstance) {
        Destroy(windowInstance);
    }
}