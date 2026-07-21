using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Unity.Netcode;

public enum PanelType {
    MainMenu,
    SinglePlayer,
    Multiplayer,
    MultiplayerRoomList,
    CreateWorldOptions,
    Options,
    Auth,
    MultiplayerMatchmaking,
    PauseMenu
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

    private Stack<PanelType> pageHistoryStack = new Stack<PanelType>();
    private Dictionary<PanelType, GameObject> panelDict = new Dictionary<PanelType, GameObject>();
    private GameInput inputActions;
    private bool isPauseMenuVisible;
    
    private void Awake() {
        inputActions = new GameInput();

        foreach (var data in uiPanels) {
            if (data.panelObject == null) continue;
            
            if (!panelDict.ContainsKey(data.panelType)) {
                panelDict.Add(data.panelType, data.panelObject);
            } else {
                Debug.LogError($"Duplicate panel types: {data.panelType}", this);
            }
        }
    }

    private void OnEnable() {
        if (inputActions == null) {
            inputActions = new GameInput();
        }

        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
        OptionsMenu.BindingOverridesChanged += HandleBindingOverridesChanged;
        inputActions.Player.Cancel.performed += OnCancelPerformed;
        LoadBindingOverrides();
        inputActions.Player.Enable();
    }

    private void OnDisable() {
        if (inputActions == null) {
            return;
        }

        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
        inputActions.Player.Cancel.performed -= OnCancelPerformed;
        inputActions.Player.Disable();

        if (isPauseMenuVisible) {
            isPauseMenuVisible = false;
            Time.timeScale = 1f;
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

    private void ShowPauseMenu() {
        if (panelDict.ContainsKey(PanelType.PauseMenu) == false) {
            Debug.LogError("PauseMenu panel not found.", this);
            return;
        }

        isPauseMenuVisible = true;
        pageHistoryStack.Clear();
        pageHistoryStack.Push(PanelType.PauseMenu);
        SetActivePanelOnly(PanelType.PauseMenu);
        Time.timeScale = 0f;
    }

    private void SetActivePanelOnly(PanelType panelType) {
        if (GameState.isPlaying)
            inGameBackground.SetActive(true);
        else
            background.SetActive(true);
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

    private void OnCancelPerformed(InputAction.CallbackContext context) {
        if (!context.performed || !GameState.isPlaying) {
            return;
        }

        if (!isPauseMenuVisible) {
            ShowPauseMenu();
            return;
        }

        if (pageHistoryStack.Count > 0 && pageHistoryStack.Peek() != PanelType.PauseMenu) {
            Back();
            return;
        }

        ResumeGame();
    }

    public void ResumeGame() {
        isPauseMenuVisible = false;
        Time.timeScale = 1f;
        HideMenu();
        clearHistory();
    }

    public void LeaveCurrentGame() {
        isPauseMenuVisible = false;
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
            NetworkManager.Singleton.Shutdown();
        }

        GameState.ResetWorld();
        HideMenu();
        clearHistory();
        SwitchToPanel(PanelType.MainMenu);
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

    public void clearHistory() {
        pageHistoryStack.Clear();
    }

    private void OnDestroy() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
        if (inputActions == null) {
            return;
        }

        inputActions.Player.Cancel.performed -= OnCancelPerformed;
        inputActions.Dispose();
    }

    private void LoadBindingOverrides() {
        if (inputActions == null) {
            return;
        }

        string overridesJson = PlayerPrefs.GetString("InputBindingOverrides", string.Empty);
        if (string.IsNullOrEmpty(overridesJson)) {
            return;
        }

        bool wasEnabled = inputActions.Player.enabled;
        if (wasEnabled) {
            inputActions.Player.Disable();
        }

        inputActions.asset.LoadBindingOverridesFromJson(overridesJson);

        if (wasEnabled) {
            inputActions.Player.Enable();
        }
    }

    private void HandleBindingOverridesChanged() {
        LoadBindingOverrides();
    }
}
