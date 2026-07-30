using UnityEngine.UI;
using UnityEngine;

public class GameMenu : MonoBehaviour {
    private UI_BagManager bagManager;
    [SerializeField] private GameObject[] hotbar = new GameObject[3];

    private void Awake() {
        bagManager = FindFirstObjectByType<UI_BagManager>();
    }

    public void Refresh() {
        for (int i = 0; i < hotbar.Length; ++i) {
            Sprite icon = bagManager.bagDataList[i].itemData.itemObject.icon;
            if (icon != null) {
                hotbar[i].GetComponent<Image>().sprite = icon;
                hotbar[i].SetActive(true);
            }
            else {
                hotbar[i].SetActive(false);
            }
        }
    }
}