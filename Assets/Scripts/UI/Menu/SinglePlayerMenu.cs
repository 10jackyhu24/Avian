using UnityEngine;

public class SinglePlayerMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void Cancel() {
        menuManager.SwitchToPanel(0);
    }
}