using UnityEngine.UI;
using UnityEngine;
using System;
using Unity.Mathematics;

public class GameMenu : MonoBehaviour {
    private UI_BagManager bagManager;
    [SerializeField] private GameObject[] hotbar = new GameObject[3];

    private void Awake() {
        bagManager = FindFirstObjectByType<UI_BagManager>();
    }

    public void Refresh() {
        int hotbar_size = hotbar.Length;
        int bag_list_size = bagManager.bagDataList.Count;
        int min_size = Math.Min(hotbar_size, bag_list_size);
        for (int i = 0; i < hotbar_size; ++i) {
            if (i < min_size) {
                Sprite icon = bagManager.bagDataList[i].itemData.itemObject.icon;
                if (icon != null) {
                    hotbar[i].GetComponent<Image>().sprite = icon;
                    hotbar[i].SetActive(true);
                    continue;
                }
            }
            hotbar[i].SetActive(false);
        }
    }
}