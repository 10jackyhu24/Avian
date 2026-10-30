# 後端資料庫與帳號測試

測試對象是目前部署中的後端。GitHub Actions 的執行順序為：

```text
backend-connection：BACKEND_HEALTH_URL 回傳 HTTP 2xx
    ↓ needs: backend-connection
database-tests：執行 test_backend_database.py
    01 連接資料庫
    02 建立使用者與兩個登入 session
    03 註銷帳號，確認所有 session 已刪除
```

BackendConnection 失敗時，整個 `database-tests` job 會跳過。
三部分在同一個 Python 程序中依 `test_01`、`test_02`、`test_03` 的名稱順序執行，
共用本輪測試帳號與 token。資料庫檢查失敗時不會建立使用者；使用者建立或
session 驗證失敗時，第三部分會跳過，並由清理程序嘗試註銷本輪帳號。

## 三部分檢查內容

1. **連接資料庫**：呼叫 `GET /auth/health/database`。Lobby 以實際的資料庫
   連線執行 `SELECT 1`；成功回傳 HTTP 200，資料庫錯誤回傳 HTTP 503。
2. **建立使用者**：以隨機 `ci_` 使用者名稱註冊，再登入一次，取得兩個不同的
   session token。檢查使用者 ID、名稱，以及兩個 session 都能通過 `/auth/validate`。
3. **註銷帳號**：用其中一個 token 呼叫 `DELETE /auth/account`。
   除了檢查刪除回應，也要求 `remaining_sessions` 是整數 0，兩個舊 token
   都回傳 HTTP 401，原帳號密碼也無法再次登入。

`remaining_sessions` 來自刪除 API 在同一筆交易內實際執行的 `COUNT(*)`。
如果還有 session 資料列，API 回傳 HTTP 500 並回滾交易。
這避免僅因 `/auth/validate` 查不到使用者，就誤以為 session 資料列也已刪除。

## 部署與 GitHub 設定

先同步並部署最新的 `backend/lobby/main.py` 與整個 `backend/lobby/sql` 目錄。
在部署主機的 `backend` 目錄執行：

```bash
docker compose up -d --build avian-lobby
```

Caddy 現有的 `/auth/*` 會轉送資料庫檢查與註銷請求。
GitHub 沿用 Actions Secret 或 Variable `BACKEND_HEALTH_URL`，例如：

```text
https://api.example.com:8443/health
```

測試從這個網址取得相同的協定、主機與埠，再呼叫 `/auth/...`。
Actions 不需要資料庫密碼，也不需要額外 Python 套件。
尚未部署新端點時，資料庫測試會因 HTTP 404 失敗。
在 fork PR 中，GitHub 不提供原儲存庫的 Secrets；沒有可用 URL 時連線檢查會失敗。

## 測試資料清理

每輪只建立一個隨機的測試帳號，註銷成功後會刪除它與它的登入資料。
`tearDownClass()` 也會在中途失敗時嘗試清理，並在刪除前確認 token 屬於本輪
隨機帳號。如果無法清理，測試會回報錯誤。程序遭強制中止時可能來不及清理。
測試不會印出密碼或 token，也不會把登入資料上傳成 artifact。

## 本機執行（PowerShell）

```powershell
$env:BACKEND_HEALTH_URL = "https://api.example.com:8443/health"
python -m unittest discover -s tests -p test_backend_connection.py -v
if ($LASTEXITCODE -ne 0) { throw "BackendConnection failed" }
python -m unittest discover -s tests -p test_backend_database.py -v
```

這三部分會呼叫真實 API 與資料庫，因此屬於整合測試。
`tests/test_account_deletion.py` 則是使用模擬資料庫的單元測試。
