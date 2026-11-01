using UnityEngine;

public class ProfileMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }
    public void SwitchToScoreBoard() {
        menuManager.SwitchToPanel(PanelType.ScoreBoard);
    }
    public void SwitchToAccountSetting() {
        menuManager.SwitchToPanel(PanelType.AccountSetting);
    }

}
