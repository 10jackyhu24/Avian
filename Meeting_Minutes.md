# Avian 討論紀錄

## 2026 / 3 / 4

**地點:** SF648
**時間:** 

### 問題

1. Claude Projects的認識
2. Cmake的認識
3. 單元測試的認識
4. clion的認識
5. j unit的認識
6. C++ class template的認識
7. java map的認識
8. iterator pattern的認識
9. C++ STL的認識
10. stdlib vs std的認識
11. 紅黑樹的認識

## 2026 / 3 / 18

**地點:** SF651
**時間:** 13 : 00 ~ 

### 問題

1. 專題設計規格
2. 輔大圖書館資源介紹

## 2026 / 4 / 1

**地點:** SF648
**時間:** 14 : 20 ~ 15 : 00

### 問題

1. 設計規格書
2. 分工
3. 架構包含
4. 資料連接模式

## 2026 / 4 / 22

**地點:** SF648
**時間:** 

### 問題

1. 開放大世界改成英文
2. 為什麼選擇 2D 不是 3D
3. 研究生物、生態、習性
4. 找相關教學影片
    * https://youtu.be/rSzuAH9hy0o?si=FXoQu2tUaa6bxE9t
    * https://www.youtube.com/watch?v=jCx8CpIO5ig
    * https://www.youtube.com/watch?v=rbnERIj0AI0
5. 為什麼不挑其他遊戲引擎
6. 軟體系統架構圖

## 2026 / 5 / 13

**地點:** SF336
**時間:** 

### 問題

1. 建置過程
2. Java FX
3. Unity debugger
4. 開發環境每個目錄作用
5. code coverage
6. 什麼是單元測試、整合測試

## 2026 / 6 / 3

**地點:** SF648
**時間:** 13 : 20 ~ 13 : 48

### 問題

**1. Model view controller (controller -> model -> view(UI))**

* 是一種分散式軟體設計模式
    * controller，負責使用者的請求，並將其傳給對應部份的 model 來運算或修改資料，屬於下指令的角色。
    * model，系統中資料庫和運算發生的地方，由 controller 接收指令並完成運算後，將結果傳給 view (使用者UI)顯示或修改資料庫的資料。
    * view，與使用者接觸的部分，使用者介面為常見呈現方式，不會有複雜的邏輯運算，主要負責與 controller溝通已完成使用者所需功能。
        
**2. 「Data flow」跟「下指令 controller」之前的關係**

* Data flow：系統中「狀態」與「資訊」的傳遞，說明資料由哪裡產生並傳輸套哪裡。
* controller 指令：一種動作與控制，通常是由外往內傳遞(例：Controller 收到點擊事件，它下指令給 Model 說：「幫我把這筆訂單寫入資料庫，並扣除庫存」。)

**3. 軟體工程的測試(單元測試)**

* 測試的三大階段
    * 單元測試 (Unit Test): 針對最小的程式單元（例如單一個 Function）進行測試，確保輸入 A 一定會得到 B。通常由工程師自己寫。

    * 整合測試 (Integration Test): 把多個模組串在一起測試。例如：測試「API 能不能正確連到資料庫並撈出資料」。

    * 端到端測試 (E2E Test): 模擬真實使用者的操作流程。例如：自動化打開瀏覽器、點擊登入、檢查有沒有成功進到首頁。

**4. 「建置」做了什麼**
* .exe 建置
    一般 C/C++ 程式可能是：Source Code → Compiler → Linker → Executable 如圖:
    ![image](https://hackmd.io/_uploads/BytR9cGZGe.png)
    
* Unity 建置

    Unity 因為還要建置其場景(Scenes)還有其他資源的緣故，流程比較像：
    ```
     Unity Project
      ├─ C# Scripts
      ├─ Scenes
      ├─ Prefabs
      ├─ Textures / Models / Audio
      ├─ Shaders
      └─ Project Settings
    ```

    → 匯入與處理資源
    → 編譯 C# 腳本
    → 打包場景與資源
    → 依照目標平台產生 Player
    → 輸出可執行檔與資料夾
    source: https://docs.unity3d.com/6000.6/Documentation/Manual/WindowsStandaloneBinaries.html
    https://docs.unity3d.com/ScriptReference/Build.Profile.BuildProfile.html
    https://docs.unity3d.com/Manual/build-profiles-reference.html
    https://docs.unity3d.com/Manual/csharp-compiler.html

    也就是說，Unity build 的重點包含兩大部分：

    * 程式碼處理以及遊戲資源打包。
    * 程式碼部分會把 C# 腳本編譯起來，而場景、圖片、模型、音效、材質、Shader 等資源則會被 Unity 轉換成遊戲執行時可以讀取的格式。

    而建置後資料夾如圖:
    ![image](https://hackmd.io/_uploads/S1LC3qMZMx.png)

    每個元件大概工作為:
    * .exe = 啟動遊戲
    * UnityPlayer.dll = Unity 引擎核心
    * _Data = 遊戲內容資料
    * D3D12 = 圖形渲染支援
    * MonoBleedingEdge = C# 腳本執行環境
    * CrashHandler = 當機處理
    * BurstDebugInformation = Burst 除錯資料
            
        
**5. Stack frame**

Stack frame是在程式呼叫函式時，在 stack (是一塊暫存資料的區域) 中建立的一塊暫時記憶體空間。每一次函式被呼叫，都會有自己的 stack frame，用來保存這個函式執行期間需要的資料，例如：return address、參數、區域變數、暫存的 register，還有上一個函式的 frame pointer。

在 assembly language 中，stack frame 的作用是讓程式能夠正確管理函式呼叫。當一個函式開始執行時，通常會用像是：

```
push ebp
mov ebp, esp
sub esp, local_size
```

來建立 stack frame。ebp (ebp 是 base pointer, esp 則是 stack pointer) 通常作為固定的參考點，方便存取參數與區域變數，例如 [ebp+8] 可能是第一個參數，而 [ebp-4] 可能是區域變數。esp 則會隨著 push、pop 或配置空間而改變。

當函式結束時，會用：

```
mov esp, ebp
pop ebp
ret
```

來刪除目前的 stack frame、恢復上一個函式的 frame pointer，並透過 ret 回到呼叫函式的位置。

綜合上術，stack frame 是每個函式在 stack 上的工作區。它讓 assembly 程式能夠管理函式呼叫、保存返回位置、存放區域變數與參數，而在函式結束後回到原本的位置。

**6. ebp 暫存器**
ebp (extended base pointer) 主要是儲存再進入一個函式前當前的一個基準點，比如: 在 main 中可能有 local var a 和 b 在 stack 上，那在 main 中想要進入 (call) 到另外一個函式 test()，這時 call 會在 stack 中放入 return address，那 ebp 則是儲存在上一個函式的基準點(記憶體位置)。把 ebp push (儲存後)，透過 esp (extended stack pointer) 來進行其他改動如設置 local var。而函式結束後透過
```
mov esp, ebp
pop ebp
ret
```
來還原先前的位置

**7. x86定址與間接定址**

在了解 addressing modes 前要先稍微了解 operand (運算元，也就是被操作的資料或位置如: 數字 1, 2, 3或記憶體位置 0x00002345 或暫存器如 eax、ebx...)和 mnemonic (想成是指令的縮寫如 move 寫成 mov)。在 assembly 中，一行程式通常是由一個 mnemonic 和 operand 組成的或兩個 operand + 1 個 mnemonic 組成如:
```
mov esp, ebp // 1個mnemonic+兩個operand
pop ebp // 1個mnemonic+1個operand
```
而在做運算時，addressing mode 則是指定 operand 來操作或獲取資料的方法，cpu 在執行時要知道資料在在指令、register 或 memory，而有不同的 address mode:

1. immediate addressing: ```mov eax, 5```
直接將想要的數值移入

2. register addressing: ```mov eax, ebx```
將一個 register 如 ebx 的值(可能是數字1,2,3或記憶體位置)移入到另一個 register 如 eax

3. direct memory addressing: ```mov eax, [0x00000234]```
直接從記憶體位址如 0x00000234 取值

4. register indirect addressing: ```mov eax, [ebx]```
從 ebx 指向的記憶體取值如 ebx 指向 0x00000234，而 0x00000234 有數字 5，那 eax=5

5. based addressing: ```mov eax, [ebp-4]```
以 ebp 為基準移動取值

6. indexed addressing：```mov eax, [array + esi]```
用 index register 找陣列元素類似於based addressing，但在 based addressing，以 ebp 為基準移動讀取當前的 stack frame 位移，而在這裡從自訂義 array 中計算為移取值。其中根據 data type 大小(int=4 bytes, word =2 bytes...) 作倍率調整如: ```mov eax, [array + esi*4]```

**8. 系統架構圖字體放大**