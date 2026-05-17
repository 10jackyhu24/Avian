using UnityEngine;

public class MultiPlayersList : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void MultiRoomList() {
        menuManager.SwitchToPanel(PanelType.MultiplayerRoomList);
    }

    public void Cancel() {
        menuManager.SwitchToPanel(PanelType.Multiplayer);
    }
}