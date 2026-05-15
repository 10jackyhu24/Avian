using UnityEngine;

public class MultiPlayersMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void MultiRoomList() {
        menuManager.SwitchToPanel(3);
    }

    public void Cancel() {
        menuManager.SwitchToPanel(0);
    }
}