# 快速參考卡

## 文件替換命令

### Windows (PowerShell)
```powershell
# 備份原始文件
Copy-Item "Assets/Scripts/UI/UI_BagItemSlot.cs" "Assets/Scripts/UI/UI_BagItemSlot.cs.backup"
Copy-Item "Assets/Scripts/UI/UI_AttachmentSlot.cs" "Assets/Scripts/UI/UI_AttachmentSlot.cs.backup"
Copy-Item "Assets/Scripts/UI/UI_BagManager.cs" "Assets/Scripts/UI/UI_BagManager.cs.backup"

# 複製新文件
Copy-Item "UI_BagItemSlot_Modified.cs" "Assets/Scripts/UI/UI_BagItemSlot.cs" -Force
Copy-Item "UI_AttachmentSlot_Modified.cs" "Assets/Scripts/UI/UI_AttachmentSlot.cs" -Force
Copy-Item "UI_BagManager_Modified.cs" "Assets/Scripts/UI/UI_BagManager.cs" -Force

# 新增文件
Copy-Item "UI_DragDropManager.cs" "Assets/Scripts/UI/UI_DragDropManager.cs"
Copy-Item "UI_DragIndicator.cs" "Assets/Scripts/UI/UI_DragIndicator.cs"
```

### macOS/Linux (Bash)
```bash
# 備份
cp Assets/Scripts/UI/UI_BagItemSlot.cs Assets/Scripts/UI/UI_BagItemSlot.cs.backup
cp Assets/Scripts/UI/UI_AttachmentSlot.cs Assets/Scripts/UI/UI_AttachmentSlot.cs.backup
cp Assets/Scripts/UI/UI_BagManager.cs Assets/Scripts/UI/UI_BagManager.cs.backup

# 複製
cp UI_BagItemSlot_Modified.cs Assets/Scripts/UI/UI_BagItemSlot.cs
cp UI_AttachmentSlot_Modified.cs Assets/Scripts/UI/UI_AttachmentSlot.cs
cp UI_BagManager_Modified.cs Assets/Scripts/UI/UI_BagManager.cs
cp UI_DragDropManager.cs Assets/Scripts/UI/
cp UI_DragIndicator.cs Assets/Scripts/UI/
```

---

## 常用代碼片段

### 1. 測試拖拽是否運行
```csharp
// 在 UI_DragDropManager 中添加調試
private void HandleSlotDrop() {
    int draggedIndex = bagManager.GetSlotIndex(draggedSlot);
    int hoveredIndex = bagManager.GetSlotIndex(hoveredSlot);
    Debug.Log($"[Drag] Moving slot {draggedIndex} to {hoveredIndex}");
    bagManager.MoveSlot(draggedIndex, hoveredIndex);
}
```

### 2. 禁用特定槽的拖拽
```csharp
// 在 UI_BagItemSlot 中
public void OnBeginDrag(PointerEventData eventData) {
    // 假設不能拖拽任務物品
    if (slotData?.itemData.itemType == ItemType.Quest) {
        eventData.pointerDrag = null;
        return;
    }
    
    dragDropManager?.OnBagItemSlotBeginDrag(this);
}
```

### 3. 添加拖拽視覺反饋
```csharp
// 在 UI_BagItemSlot 中
public void OnDrag(PointerEventData eventData) {
    GetComponent<CanvasGroup>().alpha = 0.6f;
}

public void OnEndDrag(PointerEventData eventData) {
    GetComponent<CanvasGroup>().alpha = 1f;
}
```

### 4. 限制拖拽到特定區域
```csharp
// 在 UI_DragDropManager 中
public void OnBagItemSlotHover(UI_BagItemSlot slot, Vector2 pointerPosition) {
    // 只允許在背包區域內拖拽
    RectTransform bagRect = bagManager.GetComponent<RectTransform>();
    if (!RectTransformUtility.RectangleContainsScreenPoint(bagRect, pointerPosition)) {
        hoveredSlot = null;
        dragIndicator.Hide();
        return;
    }
    
    hoveredSlot = slot;
    // ... 其他代碼
}
```

### 5. 拖拽時播放聲效
```csharp
// 在 UI_DragDropManager 中
private void HandleSlotDrop() {
    int draggedIndex = bagManager.GetSlotIndex(draggedSlot);
    int hoveredIndex = bagManager.GetSlotIndex(hoveredSlot);
    
    if (draggedIndex != hoveredIndex) {
        AudioManager.Instance.Play("drag_drop");
        bagManager.MoveSlot(draggedIndex, hoveredIndex);
    }
}
```

### 6. 拖拽時顯示提示信息
```csharp
// 在 UI_DragDropManager 中
public void OnAttachmentSlotBeginDrag(UI_AttachmentSlot slot) {
    draggedAttachment = slot;
    draggedSlot = null;
    isDraggingFromAttachment = true;
    
    // 顯示提示
    TooltipManager.Instance.Show("拖拽附件會建立新物品");
}

public void OnDragEnd() {
    TooltipManager.Instance.Hide();
    // ... 其他代碼
}
```

### 7. 阻止某些物品被拖拽
```csharp
// 在 UI_BagItemSlot 中
public void OnBeginDrag(PointerEventData eventData) {
    // 假設裝備的物品不能拖拽
    if (slotData?.itemData.itemType == ItemType.Equipment) {
        eventData.pointerDrag = null;
        return;
    }
    
    dragDropManager?.OnBagItemSlotBeginDrag(this);
}
```

### 8. 拖拽上限限制
```csharp
// 在 UI_DragDropManager 中
private bool CanDropHere(UI_BagItemSlot target) {
    // 例：只能在同類型物品附近拖拽
    BagSlotData targetData = bagManager.GetSlotData(bagManager.GetSlotIndex(target));
    BagSlotData draggedData = bagManager.GetSlotData(bagManager.GetSlotIndex(draggedSlot));
    
    return targetData.itemData.itemType == draggedData.itemData.itemType;
}
```

---

## Unity Inspector 快速配置

### 複製粘貼值
如果無法手動拖拽，可以用 Inspector 複製以下值：

#### UI_BagManager
```
UI_BagManager:
  - maxCapacity: 100
  - dragIndicatorPrefab: <從 Prefab 文件夾中選擇>
  
UI_DragDropManager:
  - dragIndicator: <自動連接>
```

#### ItemSlotPrefab (ImageSlotPrefab)
```
ItemSlotPrefab:
  - Has UI_BagItemSlot component: YES
  - Icon child name: "Icon"
  - Weight child name: "Weight"
  - Count child name: "Count"
  - Attachment container child name: "Attachment"
  - Attachment slot child names: ["AttackSpeed", "DamageBonus", "CriticalRate"]
```

---

## 日誌查看

### 啟用完整拖拽日誌

在 `UI_DragDropManager.cs` 中的 `OnBagItemSlotHover()` 添加：

```csharp
private void OnBagItemSlotHover(UI_BagItemSlot slot, Vector2 pointerPosition) {
    hoveredSlot = slot;

    RectTransform slotRect = slot.GetComponent<RectTransform>();
    Vector3[] slotCorners = new Vector3[4];
    slotRect.GetWorldCorners(slotCorners);
    
    float slotCenterY = (slotCorners[0].y + slotCorners[1].y) / 2f;
    bool isAbove = pointerPosition.y > slotCenterY;

    // 日誌
    Debug.Log($"Hovering {slot.name}, Position: {(isAbove ? "ABOVE" : "BELOW")}");

    dragIndicator.Show(slotRect, isAbove);
}
```

### 查看 Bag 數據結構

```csharp
// 在 BagManager 中添加
[ContextMenu("Print Bag Data")]
private void PrintBagData() {
    for (int i = 0; i < bagDataList.Count; i++) {
        var slot = bagDataList[i];
        Debug.Log($"[{i}] {slot.itemData.itemObject.itemName} x{slot.count}, " +
                  $"Weight: {slot.GetTotalWeight()}");
    }
}
```

執行：在 Inspector 中右鍵 BagManager > Print Bag Data

---

## 性能優化技巧

### 1. 對象池化
```csharp
// 為高頻創建銷毀的 UI 添加對象池
public class UI_SlotPool : MonoBehaviour {
    private Queue<UI_BagItemSlot> availableSlots = new Queue<UI_BagItemSlot>();
    
    public UI_BagItemSlot Get() {
        return availableSlots.Count > 0 ? 
            availableSlots.Dequeue() : 
            CreateNewSlot();
    }
    
    public void Return(UI_BagItemSlot slot) {
        slot.gameObject.SetActive(false);
        availableSlots.Enqueue(slot);
    }
}
```

### 2. 延遲重建
```csharp
// 如果拖拽時性能下降，使用延遲重建
private Coroutine rebuildCoroutine;

private IEnumerator DelayedRebuild() {
    yield return null; // 等待一幀
    RebuildSlots();
}

public void MoveSlot(int fromIndex, int toIndex) {
    // ... 移動邏輯 ...
    
    if (rebuildCoroutine != null) StopCoroutine(rebuildCoroutine);
    rebuildCoroutine = StartCoroutine(DelayedRebuild());
}
```

### 3. 批量操作優化
```csharp
// 多個拖拽操作時，禁用 LayoutGroup 更新
private void DisableLayout() {
    contentRoot.GetComponent<LayoutGroup>().enabled = false;
}

private void EnableLayout() {
    contentRoot.GetComponent<LayoutGroup>().enabled = true;
    LayoutRebuilder.ForceRebuildLayoutImmediate(
        contentRoot as RectTransform);
}
```

---

## 調試命令

### 重置拖拽狀態
```csharp
// 如果拖拽卡住，可以強制重置
[ContextMenu("Reset Drag State")]
private void ResetDragState() {
    draggedSlot = null;
    draggedAttachment = null;
    hoveredSlot = null;
    isDraggingFromAttachment = false;
    dragIndicator.Hide();
    Debug.Log("[DragDropManager] Drag state reset");
}
```

### 強制重建 UI
```csharp
// 如果 UI 顯示錯誤，強制重建
[ContextMenu("Force Rebuild")]
private void ForceRebuild() {
    RebuildSlots();
    UpdateCapacityLabel();
    Debug.Log("[BagManager] UI rebuilt");
}
```

### 驗證配置
```csharp
// 檢查所有配置是否正確
[ContextMenu("Validate Setup")]
private void ValidateSetup() {
    if (dragIndicatorPrefab == null) 
        Debug.LogError("dragIndicatorPrefab 未分配！");
    if (contentRoot == null) 
        Debug.LogError("contentRoot 未分配！");
    if (itemSlotPrefab == null) 
        Debug.LogError("itemSlotPrefab 未分配！");
    Debug.Log("[BagManager] Validation complete");
}
```

---

## 常見問題快速解決

### "NullReferenceException: dragDropManager is null"
**解決**：
```csharp
// 在 OnBeginDrag 中添加延遲查找
if (dragDropManager == null) {
    FindDragDropManager();
}
```

### 拖拽時 UI 跳躍
**解決**：
```csharp
// 檢查 CanvasGroup 設置
CanvasGroup cg = GetComponent<CanvasGroup>();
cg.blocksRaycasts = false; // 拖拽時禁用射線檢測
```

### 指示線不顯示
**解決**：
```csharp
// 檢查 Image 組件
Image lineImage = dragIndicator.GetComponent<Image>();
lineImage.color = new Color(0.5f, 0.5f, 0.5f, 1f); // 確保不透明
```

### 拖拽後槽不更新
**解決**：
```csharp
// 確保在 OnDragEnd 後調用了 RebuildSlots()
public void OnDragEnd() {
    dragIndicator.Hide();
    
    if (hoveredSlot != null && draggedSlot != null) {
        HandleSlotDrop();
        // 確保這裡調用了 MoveSlot，它會調用 RebuildSlots()
    }
}
```

---

## 一鍵測試

### 在 Play Mode 中測試拖拽
```csharp
// 在 UI_DragDropManager 中添加
#if UNITY_EDITOR
private void Update() {
    if (Input.GetKeyDown(KeyCode.R)) {
        Debug.Log("Reset drag state");
        draggedSlot = null;
        draggedAttachment = null;
        hoveredSlot = null;
        dragIndicator.Hide();
    }
}
#endif
```

### 一鍵生成測試物品
```csharp
// 在 UI_BagManager 中添加
[ContextMenu("Add Test Items")]
private void AddTestItems() {
    Dictionary<string, ItemData> items = ItemLoader.LoadAllItemData();
    for (int i = 0; i < 5; i++) {
        AddItem(items["Gun"]);
    }
    Debug.Log("Added 5 test items");
}
```

---

## 關鍵檢查點（完整清單）

- [ ] 編譯無錯誤
- [ ] BagManager 有 UI_DragDropManager 組件
- [ ] dragIndicatorPrefab 已配置
- [ ] 拖拽指示線在 Prefab 中有 Image 組件
- [ ] ItemSlotPrefab 的子物體名稱正確
- [ ] 可以添加物品到背包
- [ ] 滑鼠按住槽時光標改變
- [ ] 拖拽時灰色線顯示
- [ ] 釋放滑鼠時槽重排序
- [ ] 拖拽附件時建立新槽
- [ ] 容量文本正確更新

**全部檢查點通過 = 功能完成！** ✅

