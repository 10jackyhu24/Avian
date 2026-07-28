# 拖拽功能實現指南

## 概述
此實現使用**最少改動**和**高維護性**的架構，通過分離關注點來實現拖拽重排序功能。

### 核心設計原則
- **單一職責**：每個類只負責一個功能
- **解耦合**：UI 層只負責事件觸發，邏輯集中在 Manager
- **可擴展性**：輕鬆支持未來的新功能

---

## 文件說明

### 1. `UI_DragIndicator.cs` (新建)
**職責**：顯示拖拽位置指示線
- 在目標槽的上方或下方顯示一條線
- 跟隨目標槽的寬度
- 隱藏/顯示管理

```csharp
// 使用方式
dragIndicator.Show(targetSlot, isAbove: true);  // 在上方顯示
dragIndicator.Hide();                            // 隱藏
```

### 2. `UI_DragDropManager.cs` (新建)
**職責**：集中管理所有拖拽邏輯
- 監聽 BagItemSlot 和 AttachmentSlot 的拖拽事件
- 管理拖拽狀態（正在拖拽什麼）
- 決定放置邏輯（槽重排序或建立新槽）

**關鍵方法**：
```csharp
OnBagItemSlotBeginDrag()      // 槽開始拖拽
OnAttachmentSlotBeginDrag()   // 附件開始拖拽
OnBagItemSlotHover()          // 懸停在槽上
OnDragEnd()                   // 拖拽結束，執行重排序
```

### 3. `UI_BagItemSlot_Modified.cs`
**改動**：添加 Drag/Drop 事件處理器
- 實現 `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`
- 實現 `IPointerEnterHandler`, `IPointerExitHandler`
- 委托給 `UI_DragDropManager` 處理

**新增代碼量**：~60 行（主要是事件回調）

```csharp
public void OnBeginDrag(PointerEventData eventData) {
    dragDropManager?.OnBagItemSlotBeginDrag(this);
}
```

### 4. `UI_AttachmentSlot_Modified.cs`
**改動**：添加 Drag/Drop 事件處理器
- 允許拖拽附件（只有有物品時才能拖）
- 委托給 `UI_DragDropManager` 處理

**新增代碼量**：~50 行

### 5. `UI_BagManager_Modified.cs`
**改動**：添加槽管理和拖拽初始化
- `GetSlotIndex()`：獲取槽的索引
- `MoveSlot()`：移動槽並重建 UI
- `InsertItem()`：在指定位置插入新物品

**新增代碼量**：~70 行

---

## 集成步驟

### 步驟 1：替換文件
```
舊文件                        新文件
UI_BagItemSlot.cs      →    UI_BagItemSlot_Modified.cs
UI_AttachmentSlot.cs   →    UI_AttachmentSlot_Modified.cs
UI_BagManager.cs       →    UI_BagManager_Modified.cs
```

1. 備份原始文件
2. 複製新文件內容，覆蓋原始文件
3. 保持文件名不變（移除 `_Modified` 後綴）

### 步驟 2：新增組件
1. 在 Unity 中新建 `UI_DragIndicator.cs` 和 `UI_DragDropManager.cs`
2. 在場景中找到 BagManager GameObject

### 步驟 3：設置 Prefab
1. **創建拖拽指示線 Prefab**
   - 在場景中建立新 GameObject：`UI_DragIndicator`
   - 添加 `Image` 組件（設置為細線，例如灰色，高度 2px）
   - 添加 `UI_DragIndicator` 腳本
   - 保存為 Prefab

2. **配置 BagManager 節點**
   ```
   Canvas (或其他根節點)
   ├── BagManager (添加 UI_DragDropManager 組件)
   │   ├── ContentRoot (Grid Layout Group 容器)
   │   │   └── ItemSlotPrefab
   │   └── TotalCapacityText
   ```

### 步驟 4：檢查器配置
在 BagManager 的檢查器中設置：

```
[UI_BagManager]
- maxCapacity: 100
- totalCapacityText: (拖拽到此)
- contentRoot: (拖拽到此)
- itemSlotPrefab: (拖拽到此)
- dragIndicatorPrefab: (拖拽到此) ← 新增

[UI_DragDropManager]
- dragIndicator: (設置為上面的 prefab 實例)
```

---

## 使用流程

### 拖拽 BagItemSlot
```
1. 按住滑鼠在槽上
2. 移動到另一個槽
3. 如果在上半部分，顯示線在上方
4. 如果在下半部分，顯示線在下方
5. 釋放滑鼠 → 槽重新排序
```

### 拖拽 AttachmentSlot
```
1. 按住滑鼠在附件上
2. 移動到目標槽上
3. 顯示插入位置線
4. 釋放滑鼠 → 建立新 BagSlot，清除原附件
```

---

## 代碼更改總結

| 文件 | 行數 | 改動類型 | 說明 |
|------|------|---------|------|
| UI_BagItemSlot | +60 | 新增方法 | 拖拽事件處理 |
| UI_AttachmentSlot | +50 | 新增方法 | 拖拽事件處理 |
| UI_BagManager | +70 | 新增方法 | 槽管理邏輯 |
| UI_DragDropManager | +180 | 新建 | 集中邏輯管理 |
| UI_DragIndicator | +50 | 新建 | UI 指示器 |

**總計**：~410 行代碼，其中 ~150 行是核心邏輯，260 行是必要的樣板代碼。

---

## 架構圖

```
用戶輸入 (Mouse Events)
    ↓
UI_BagItemSlot / UI_AttachmentSlot (事件觸發)
    ↓
UI_DragDropManager (邏輯決策)
    ├─→ 更新 UI_DragIndicator (視覺反饋)
    └─→ 調用 UI_BagManager (執行操作)
    ↓
bagDataList (數據層，不變)
```

---

## 擴展建議

### 1. 拖拽視覺反饋
在 `UI_BagItemSlot.OnDrag()` 中添加：
```csharp
public void OnDrag(PointerEventData eventData) {
    // 降低透明度或改變顏色
    GetComponent<CanvasGroup>().alpha = 0.6f;
}
```

### 2. 拖拽聲效
```csharp
private void HandleSlotDrop() {
    AudioManager.Play("drag_drop_sfx");
    bagManager.MoveSlot(draggedIndex, insertIndex);
}
```

### 3. 限制拖拽區域
```csharp
public void OnBagItemSlotHover(UI_BagItemSlot slot, ...) {
    if (!slot.gameObject.activeSelf) return; // 跳過不活躍的槽
    // ...
}
```

### 4. 支持類型限制
```csharp
private bool CanDropHere(BagSlotData fromSlot, BagSlotData toSlot) {
    // 例如：同類型物品相鄰排列
    return fromSlot.itemData.itemType == toSlot.itemData.itemType;
}
```

---

## 調試技巧

### 啟用調試日誌
在 `UI_DragDropManager` 中添加：
```csharp
Debug.Log($"Dragging slot at index {draggedIndex} to {hoveredIndex}");
Debug.Log($"Drop above: {isAbove}");
```

### 檢查拖拽狀態
在編輯器中添加監視窗口，追蹤：
- `draggedSlot` (當前拖拽的槽)
- `hoveredSlot` (懸停的槽)
- `bagDataList.Count` (列表長度)

---

## 常見問題

**Q: 如何防止拖拽空槽？**
A: 在 `UI_BagItemSlot.OnBeginDrag()` 中檢查：
```csharp
if (slotData.IsEmpty) {
    eventData.pointerDrag = null;
    return;
}
```

**Q: 如何支持快速拖拽？**
A: 調整 Unity 的 `Draggable Threshold` 設置（Edit > Project Settings > Input Manager）

**Q: 附件拖拽後如何更新權重？**
A: `HandleAttachmentDrop()` 自動建立新槽，權重自動計算

---

## 性能考量

- **重建 UI**：`RebuildSlots()` 為 O(n)，輕量級操作
- **事件觸發**：使用標準 Unity EventSystem，無額外開銷
- **內存**：只存儲活躍的 UI 槽引用，垃圾回收友好

---

## 後續維護

- 如果要修改拖拽邏輯，只需編輯 `UI_DragDropManager.cs`
- 如果要新增槽管理功能，在 `UI_BagManager` 中添加公共方法
- 不需修改 `Item.cs` 等數據層代碼

