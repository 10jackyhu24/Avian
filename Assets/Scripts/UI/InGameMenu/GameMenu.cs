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
    private int selectedIndex = -1;
    public event Action<int> OnSelectedIndexChanged; // 事件(selectHotbar 改變)
    /*  事件廣播接收
        在Awake()寫 gameMenu.OnSelectedIndexChanged += 功能名; 然後在OnDestroy() 裡面寫gameMenu.OnSelectedIndexChanged += 功能名;
        例：
        private void funtionName(int index) {
        ...
        }
        Awake() 寫 gameMenu.OnSelectedIndexChanged += funtionName;
        OnDestroy 寫gameMenu.OnSelectedIndexChanged -= funtionName;
    */

    private void Awake() {
        bagManager = FindFirstObjectByType<UI_BagManager>();
        if (bagManager != null) {
            bagManager.InventoryDisplayChanged += Refresh;
        }

        for (int i = 0; i < 3; ++i) {
            hotbar.Add(PrefabSpawn.SpawnInit(hotbarPrefab, hotbarParent.transform));
        }
        SetSelectedIndex(0);
    }

    private void OnDestroy() {
        if (bagManager != null) {
            bagManager.InventoryDisplayChanged -= Refresh;
        }
    }

    public void Refresh() {
        if (bagManager == null) {
            return;
        }

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

    // 設定Selected HotBar (外部取用)
    public void SetSelectedIndex(int newIndex) {
        if(newIndex == selectedIndex) return;
        if(newIndex < 0 || newIndex >= hotbar.Count) return;

        //避免原先index = -1(初始值) 加if 限制範圍
        if(selectedIndex >= 0 && selectedIndex < hotbar.Count) {
            GameObject oldFrame = hotbar[selectedIndex].FindChildObject("SelectedFrame");
            if (oldFrame != null) oldFrame.SetActive(false);
        }

        GameObject newFrame = hotbar[newIndex].FindChildObject("SelectedFrame");
        if (newFrame != null) newFrame.SetActive(true);
        
        selectedIndex = newIndex;

        OnSelectedIndexChanged?.Invoke(selectedIndex); //把index廣撥給有訂閱此事件的物件(腳本)
    }

}
