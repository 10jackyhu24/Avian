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
    MultiplayerMatchmaking,
    PauseMenu,
    BagMenu
}

public class MenuManager : MonoBehaviour {
    [System.Serializable]
    private struct PanelData {
        public PanelType panelType;
        public GameObject panelObject;
    }

    [Header("UI Elements")]
    [SerializeField] private PanelData[] uiPanels;
    [SerializeField] private GameObject background;
    [SerializeField] private GameObject inGameBackground;

    public readonly Stack<PanelType> pageHistoryStack = new Stack<PanelType>();
    private readonly Dictionary<PanelType, GameObject> panelDict = new Dictionary<PanelType, GameObject>();

    private void Awake() {
        foreach (var data in uiPanels) {
            if (data.panelObject == null) {
                continue;
            }

            if (!panelDict.ContainsKey(data.panelType)) {
                panelDict.Add(data.panelType, data.panelObject);
            } else {
                Debug.LogError($"Duplicate panel types: {data.panelType}", this);
            }
        }
    }

    public void Start() {
        HideMenu();
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

    public void SetActivePanelOnly(PanelType panelType) {
        if (GameState.isPlaying) {
            inGameBackground.SetActive(true);
        } else {
            background.SetActive(true);
        }

        foreach (var pair in panelDict) {
            pair.Value.SetActive(pair.Key == panelType);
        }

        Debug.Log("Switch to panel:" + panelType);
    }

    public void Back() {
        TryBack();
    }

    public bool TryBack() {
        if (pageHistoryStack.Count <= 1) {
            return false;
        }

        pageHistoryStack.Pop();
        PanelType previousPanel = pageHistoryStack.Peek();
        if (panelDict.ContainsKey(previousPanel)) {
            SetActivePanelOnly(previousPanel);
            return true;
        }

        return false;
    }

    public void HideMenu() {
        background.SetActive(false);
        inGameBackground.SetActive(false);

        foreach (var pair in panelDict) {
            if (pair.Value != null) {
                pair.Value.SetActive(false);
            }
        }

        Debug.Log("Hiding all UI panels.");
    }

    public void ClearHistory() {
        pageHistoryStack.Clear();
    }
}
