using UnityEngine;
using System.Collections.Generic;

public enum PanelType {
    MainMenu,
    SinglePlayer,
    Multiplayer,
    MultiplayerRoomList,
    Options
}

public class MenuManager : MonoBehaviour {
    [System.Serializable]
    public struct PanelData {
        public PanelType panelType;
        public GameObject panelObject;
    }

    [Header("UI Elements")]
    [SerializeField] private PanelData[] uiPanels;

    private Dictionary<PanelType, GameObject> panelDict = new Dictionary<PanelType, GameObject>();
    
    private void Awake() {
        foreach (var data in uiPanels) {
            if (data.panelObject == null) continue;
            
            if (!panelDict.ContainsKey(data.panelType)) {
                panelDict.Add(data.panelType, data.panelObject);
            } else {
                Debug.LogError($"Duplicate panel types: {data.panelType}", this);
            }
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
        foreach (var pair in panelDict) {
            pair.Value.SetActive(pair.Key == panelType);
        }
        Debug.Log("Switch to panel:" + panelType);
    }

}
