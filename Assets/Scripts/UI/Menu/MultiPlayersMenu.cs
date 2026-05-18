using UnityEngine;

public class MultiPlayersMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void SwitchToRoomList() {
        menuManager.SwitchToPanel(PanelType.MultiplayerRoomList);
    }
}