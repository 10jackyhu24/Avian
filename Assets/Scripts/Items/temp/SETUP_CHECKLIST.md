# 快速設置清單

## ✅ 前置檢查

- [ ] 備份原始 `UI_BagItemSlot.cs`、`UI_AttachmentSlot.cs`、`UI_BagManager.cs`
- [ ] Unity 版本 2020.3 以上（支持 EventSystem）
- [ ] TextMeshPro 已安裝
- [ ] 項目有 `Utils` 命名空間（PrefabSpawn 工具類）

---

## ✅ 文件替換

### 1. 替換現有文件
```
/Assets/Scripts/UI/
├── UI_BagItemSlot.cs          ← 替換為 UI_BagItemSlot_Modified.cs 內容
├── UI_AttachmentSlot.cs       ← 替換為 UI_AttachmentSlot_Modified.cs 內容
└── UI_BagManager.cs           ← 替換為 UI_BagManager_Modified.cs 內容
```

**操作步驟**：
1. 複製新文件的全部代碼
2. 打開原始文件
3. 選擇全部（Ctrl+A）
4. 粘貼新代碼
5. 保存

### 2. 新增文件
```
/Assets/Scripts/UI/
├── UI_DragIndicator.cs        ← 複製 UI_DragIndicator.cs
└── UI_DragDropManager.cs      ← 複製 UI_DragDropManager.cs
```

---

## ✅ Scene 設置

### 場景結構參考
```
Canvas (RenderMode: Screen Space - Overlay)
│
├── BagPanel (RectTransform)
│   ├── [UI_BagManager] ← 添加組件
│   ├── [UI_DragDropManager] ← 添加組件
│   │
│   ├── TitleText
│   │   └── "背包 (容量: 80/100)"
│   │
│   ├── CapacityText (TextMeshProUGUI)
│   │   └── "80/100" ← 在檢查器中分配給 BagManager
│   │
│   └── ScrollView (Scroll Rect)
│       └── Viewport
│           └── ContentRoot (GridLayoutGroup) ← 在檢查器中分配給 BagManager
│               ├── ItemSlotPrefab (預製體) ← 在檢查器中分配給 BagManager
│               ├── ItemSlot_0 (拖拽指示線將生成在這裡)
│               ├── ItemSlot_1
│               └── ...
│
└── DragIndicator (Prefab) ← 在檢查器中分配給 BagManager
    ├── [Image] 組件
    │   ├── Color: (128, 128, 128, 255) 灰色
    │   ├── Source Image: Square (白色方塊)
    │   └── Preserve Aspect: OFF
    └── [UI_DragIndicator] 腳本
```

---

## ✅ 檢查器配置

### UI_BagManager 配置
| 屬性 | 值 | 說明 |
|------|-----|------|
| **Capacity Settings** |
| maxCapacity | 100 | 背包最大容量 |
| totalCapacityText | CapacityText | 顯示容量文本 |
| **UI Spawn Setup** |
| contentRoot | ContentRoot | 物品槽父物體 |
| itemSlotPrefab | ItemSlotPrefab | 物品槽預製體 |
| **Drag Drop** |
| dragIndicatorPrefab | DragIndicator | 拖拽指示線預製體 |

### UI_DragDropManager 配置
| 屬性 | 值 | 說明 |
|------|-----|------|
| dragIndicator | (自動連接) | DragIndicator 實例 |

---

## ✅ Prefab 創建

### 步驟 1：創建拖拽指示線 Prefab

**在 Scene 中**：
1. 右鍵 > UI > Image
2. 重命名為 `DragIndicator`
3. 放在 ContentRoot 旁邊

**配置 Image 組件**：
```
Image
├── Source Image: Square (內置)
├── Color: (0.5, 0.5, 0.5, 1) 灰色
└── Preserve Aspect: OFF

RectTransform
├── Width: 500
├── Height: 2
```

**添加 UI_DragIndicator 腳本**：
1. 在檢查器添加 `UI_DragIndicator` 腳本
2. 在 Image 欄位拖入 Image 組件

**保存為 Prefab**：
1. 將 DragIndicator 拖入 Assets/Resources/Prefabs/
2. 變成 Prefab
3. 刪除 Scene 中的實例

---

## ✅ ItemSlotPrefab 檢查

**ItemSlotPrefab 結構**（應該已有）：
```
ItemSlotPrefab (Panel)
├── Icon (Image)
│   └── [名稱] "Icon"
├── Weight (TextMeshProUGUI)
│   └── [名稱] "Weight"
├── Count (TextMeshProUGUI)
│   └── [名稱] "Count"
└── Attachment (Panel) [名稱] "Attachment"
    ├── AttackSpeed (AttachmentSlot)
    ├── DamageBonus (AttachmentSlot)
    └── CriticalRate (AttachmentSlot)
```

**驗證**：
- [ ] 所有子物體名稱匹配（Icon, Weight, Count, Attachment)
- [ ] 附件槽名稱為 AttackSpeed, DamageBonus, CriticalRate
- [ ] 每個附件槽有 UI_AttachmentSlot 腳本

---

## ✅ 代碼引用驗證

### 確保引用可解析
```csharp
// ✅ 應該存在於項目中
using Utils;                    // PrefabSpawn
using UnityEngine.EventSystems; // IBeginDragHandler 等

// ✅ 腳本應該能找到彼此
ItemLoader.LoadAllItemData();   // 在 Awake 中使用
```

**如果遇到錯誤**：
- [ ] 確保有 `Utils` 命名空間的 `PrefabSpawn` 類
- [ ] 確保有 `ItemLoader` 工具類
- [ ] 檢查 `using` 語句是否完整

---

## ✅ 測試流程

### 1. 編譯檢查
```
Unity Console 中應無編譯錯誤
```

### 2. 運行測試

**操作 1：拖拽物品槽**
```
操作：按住滑鼠 → 拖到另一槽上方 → 釋放
預期：
  ✅ 灰色線顯示在上方
  ✅ 釋放後槽重新排序
  ✅ 容量文本不變
```

**操作 2：拖拽附件**
```
操作：按住附件 → 拖到目標槽 → 釋放
預期：
  ✅ 灰色線在目標位置
  ✅ 新建 BagSlot (附件變成獨立物品)
  ✅ 原槽附件清空
  ✅ 容量可能改變
```

**操作 3：快速拖拽**
```
操作：快速拖拽多次
預期：
  ✅ UI 流暢無卡頓
  ✅ 沒有內存洩漏
```

---

## ✅ 常見設置問題

### 問題 1：DragIndicator 不顯示
**解決**：
- [ ] 檢查 Image 的 Color Alpha 值（不要設為 0）
- [ ] 檢查 RectTransform 高度（設為 2-5px）
- [ ] 檢查 Source Image 不為空

### 問題 2：拖拽無反應
**解決**：
- [ ] 檢查 Canvas 上有 GraphicRaycaster
- [ ] 檢查 ItemSlot 是否有 CanvasGroup（不應該勾選 Ignore Layout）
- [ ] 檢查 ContentRoot 有 LayoutGroup

### 問題 3：槽重排序後位置錯誤
**解決**：
- [ ] 檢查 GridLayoutGroup 的 Child Force Size 設置
- [ ] 確認 RebuildSlots() 在拖拽結束後調用
- [ ] 檢查 bagDataList 的順序是否正確

---

## ✅ 優化建議

### 性能優化
```csharp
// 如果有大量物品，啟用對象池
[SerializeField] private ObjectPool<UI_BagItemSlot> slotPool;
```

### 視覺優化
```csharp
// 在 OnDrag() 中添加反饋
draggedSlot.GetComponent<CanvasGroup>().alpha = 0.6f;
```

### 交互優化
```csharp
// 設置最小拖拽距離
dragThreshold = 10;
```

---

## ✅ 最終驗證

運行游戲前，檢查以下列表：

- [ ] Unity Console 無紅色錯誤
- [ ] BagManager 檢查器所有字段已分配
- [ ] DragDropManager 組件已添加到 BagManager 物體
- [ ] ItemSlotPrefab 子物體名稱正確
- [ ] DragIndicatorPrefab 已保存為 Prefab
- [ ] 項目可以編譯並運行
- [ ] 可以拖拽物品槽
- [ ] 可以拖拽附件

**全部打勾後，功能應該可以正常使用！**

