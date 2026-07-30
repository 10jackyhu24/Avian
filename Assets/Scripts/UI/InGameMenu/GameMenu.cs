using UnityEngine.UI;
using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;

public class GameMenu : MonoBehaviour {
    private UI_BagManager bagManager;
    [SerializeField] GameObject hotbarParent;
    [SerializeField] GameObject hotbarPrefab;
    private List<GameObject> hotbar = new List<GameObject>();

    private void Awake() {
        bagManager = FindFirstObjectByType<UI_BagManager>();
        for (int i = 0; i < 3; ++i) {
            hotbar.Add(PrefabSpawn.SpawnInit(hotbarPrefab, hotbarParent.transform));
        }
    }

    public void Refresh() {
        int hotbar_size = hotbar.Count;
        int bag_list_size = bagManager.bagDataList.Count;
        int min_size = Math.Min(hotbar_size, bag_list_size);
        for (int i = 0; i < hotbar_size; ++i) {
            GameObject hotbar_icon = hotbar[i].FindChildObject("Icon");
            GameObject hotbar_count = hotbar[i].FindChildObject("Count");
            if (i < min_size) {
                Sprite icon = bagManager.bagDataList[i].itemData.itemObject.icon;
                int count = bagManager.bagDataList[i].count;
                if (icon != null) {
                    hotbar_icon.GetComponent<Image>().sprite = icon;
                    hotbar_icon.SetActive(true);
                    hotbar_count.GetComponent<TextMeshProUGUI>().text = count.ToString();
                    hotbar_count.SetActive(true);
                    continue;
                }
            }
            hotbar_icon.SetActive(false);
            hotbar_count.SetActive(false);
        }
    }
}