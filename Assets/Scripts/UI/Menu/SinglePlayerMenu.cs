using UnityEngine;

public class SinglePlayerMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }
    
    public void SwitchToCreateWorldOptions() {
        menuManager.SwitchToPanel(PanelType.CreateWorldOptions);
    }
}