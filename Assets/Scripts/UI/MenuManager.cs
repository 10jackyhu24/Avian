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

    private readonly Stack<PanelType> pageHistoryStack = new Stack<PanelType>();
    private readonly Dictionary<PanelType, GameObject> panelDict = new Dictionary<PanelType, GameObject>();
    // private GameInput inputActions;
    // private BagMenu bagMenu;
    private bool isBagMenuVisible;

    private void Awake() {
        // inputActions = new GameInput();

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

    // private void OnBagPerformed(InputAction.CallbackContext context) {
    //     if (!context.performed || !GameState.isPlaying) {
    //         return;
    //     }

    //     if (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) {
    //         return;
    //     }

    //     if (isBagMenuVisible) {
    //         CloseBagMenu();
    //         return;
    //     }

    //     ShowBagMenu();
    // }

    // private void OnCancelPerformed(InputAction.CallbackContext context) {
    //     if (!context.performed || !GameState.isPlaying) {
    //         return;
    //     }

    //     PauseMenu pauseMenu = PauseMenu.Instance;
    //     if (pauseMenu != null) {
    //         pauseMenu.HandleCancel();
    //     }
    // }

    // private void ShowBagMenu() {
    //     if (!panelDict.ContainsKey(PanelType.BagMenu)) {
    //         Debug.LogError("BagMenu panel not found.", this);
    //         return;
    //     }

    //     isBagMenuVisible = true;
    //     pageHistoryStack.Clear();
    //     pageHistoryStack.Push(PanelType.BagMenu);
    //     SetActivePanelOnly(PanelType.BagMenu);
    //     Time.timeScale = 0f;

    //     if (bagMenu == null && panelDict.TryGetValue(PanelType.BagMenu, out GameObject bagPanel) && bagPanel != null) {
    //         bagMenu = bagPanel.GetComponent<BagMenu>();
    //     }

    //     BagInventory inventory = FindLocalBagInventory();
    //     bagMenu?.Open(inventory);
    // }

    // private void CloseBagMenu() {
    //     if (!isBagMenuVisible) {
    //         return;
    //     }

    //     isBagMenuVisible = false;
    //     Time.timeScale = 1f;
    //     bagMenu?.Close();
    //     HideMenu();
    //     ClearHistory();
    // }

    // private BagInventory FindLocalBagInventory() {
    //     if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null) {
    //         NetworkObject playerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
    //         if (playerObject != null) {
    //             BagInventory inventory = playerObject.GetComponent<BagInventory>();
    //             if (inventory != null) {
    //                 return inventory;
    //             }

    //             inventory = playerObject.GetComponentInChildren<BagInventory>();
    //             if (inventory != null) {
    //                 return inventory;
    //             }
    //         }
    //     }

    //     return FindFirstObjectByType<BagInventory>();
    // }

    // private void HandleBindingOverridesChanged() {
    //     KeyBinding.LoadBindingOverrides(inputActions);
    // }
}
