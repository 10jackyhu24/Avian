using UnityEngine;

public class MenuManager : MonoBehaviour {
    [Header("UI Elements")]
    [Tooltip("Drag all Panels in sequence")]
    [SerializeField] private GameObject[] uiPanels;

    public void Start() {
        if (uiPanels == null || uiPanels.Length == 0) {
            Debug.LogError("UI Panels not assigned in the inspector.");
            return;
        }
        SwitchToPanel(0);
    }

    public void SwitchToPanel(int panelIndex) {
        if (panelIndex < 0 || panelIndex >= uiPanels.Length) {
            Debug.LogError("Panel index out of range: " + panelIndex);
            return;
        }
        for (int i = 0; i < uiPanels.Length; ++i) {
            uiPanels[i].SetActive(i == panelIndex);
        }
        Debug.Log("Switch to page" + panelIndex);
    }
}
