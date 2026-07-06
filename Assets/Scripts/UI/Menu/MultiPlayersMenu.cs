using UnityEngine;

public class MultiPlayersMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void SwitchToRoomList() {
        menuManager.SwitchToPanel(PanelType.MultiplayerRoomList);
    }

    public void SwitchToMatchmaking() {
        menuManager.SwitchToPanel(PanelType.MultiplayerMatchmaking);
    }
}