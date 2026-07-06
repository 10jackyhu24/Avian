using UnityEngine;
using System.Collections.Generic;

public enum PanelType {
    MainMenu,
    SinglePlayer,
    Multiplayer,
    MultiplayerRoomList,
    CreateWorldOptions,
    Options,
    Auth,
    MultiplayerMatchmaking
}

public class MenuManager : MonoBehaviour {
    [System.Serializable]
    private struct PanelData {
        public PanelType panelType;
        public GameObject panelObject;
    }

    [Header("UI Elements")]
    [SerializeField] private PanelData[] uiPanels;

    private Stack<PanelType> pageHistoryStack = new Stack<PanelType>();
    private Dictionary<PanelType, GameObject> panelDict = new Dictionary<PanelType, GameObject>();
    
    private void Awake() {
        foreach (var data in uiPanels) {
            if (data.panelObject == null) continue;
            
            if (!panelDict.ContainsKey(data.panelType)) {
                panelDict.Add(data.panelType, data.panelObject);
            } else {
                Debug.LogError($"Duplicate panel types: {data.panelType}", this);
            }
        }
    }

    public void Start() {
        SwitchToPanel(PanelType.MainMenu);
    }

    public void SwitchToPanel(PanelType panelType) {
        if (!panelDict.ContainsKey(panelType)) {
            Debug.LogError("Panel not found: " + panelType, this);
            return;
        }
        pageHistoryStack.Push(panelType);
        SetActivePanelOnly(panelType);
    }

    private void SetActivePanelOnly(PanelType panelType) {
        foreach (var pair in panelDict) {
            pair.Value.SetActive(pair.Key == panelType);
        }
        Debug.Log("Switch to panel:" + panelType);
    }

    public void Back() {
        if (pageHistoryStack.Count <= 1) {
            return;
        }
        pageHistoryStack.Pop();
        PanelType previousPanel = pageHistoryStack.Peek();
        if (panelDict.ContainsKey(previousPanel)) {
            SetActivePanelOnly(previousPanel);
        }
    }

    public void HideMenu() {
        foreach (var pair in panelDict) {
            if (pair.Value != null) {
                pair.Value.SetActive(false);
            }
        }
        Debug.Log("Hiding all UI panels.");
    }
}
