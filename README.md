# Web 3D Viewer Backend（C#）

3D Viewer 的後端，以 ASP.NET Core 實作。
API 規格沿用 Node.js 版（[web3D_proto_backend](https://github.com/maxyeh666/web3D_proto_backend)）：
端點、錯誤碼、資料形狀都一致，前端切換後端時不需改程式。

## Goal

建立獨立 Backend，讓 3D Viewer 的 Asset 從 Frontend Config 演進為可透過 API 管理的資料。

```text
React / R3F
     ↓
REST API
     ↓
ASP.NET Core
     ↓
PostgreSQL
```

## Tech Stack

- .NET 10（ASP.NET Core）
- EF Core + Npgsql（PostgreSQL 的 .NET 驅動）
- PostgreSQL
- JWT（HS256）

資料表結構由手寫 SQL migration 管理，EF Core 負責資料讀寫。
Migration 與 Node.js 版共用同一批 SQL，目前由 Node.js 版執行（C# 版不跑 migration）。

## Progress Log

開發過程與設計決策見 [CHANGELOG.md](./CHANGELOG.md)。

## Roadmap

### Phase 1 — 環境與骨架

- [x] 建立分層資料夾（Controllers、Services、Data、Models、Constants、Middleware）與專案基本設定
- [x] 開發埠改為 `3000`，與前端 `VITE_API_BASE_URL` 一致
- [ ] CORS、API 存取日誌
- [x] 統一錯誤回應的全域設定（見 Phase 3，已完成）
- [x] `GET /health`、`GET /health/db`

### Phase 2 — Database

- [x] Npgsql 連線 PostgreSQL，建立 `assets` 資料表對應的實體
- [x] 資料表與預設資料已存在（由 Node.js 版套用 `000`–`004` 建立；C# 版不執行 migration，只讀寫資料）
- [x] `updatedAt` 輸出格式與 Node.js 版一致（`AssetService.ToDetailDto` 以 `yyyy-MM-ddTHH:mm:ss.fff'Z'` 輸出）

### Phase 3 — 錯誤處理與驗證

- [x] 錯誤碼與 HTTP 狀態碼對照表（與 Node.js 版一致，見 `Constants/ApiErrors.cs`）
- [x] 例外與 404 的統一處理（`ExceptionMiddleware` 掛最前、`MapFallback("{*path}")` 放最後，皆回契約形狀）
- [x] 請求資料驗證（資產 id、請求內容、登入密碼；驗證失敗回 400 `INVALID_REQUEST_BODY`，錯誤本文已統一）

### Phase 4 — API

- [x] `GET /assets`

- [x] `GET /assets/{id}`
- [x] `PUT /assets/{id}`（需管理者 token）
- [x] `POST /auth/login`
- [ ] `GET /models/{modelKey}`

### Phase 5 — 整合與上線

- [ ] 用 `.http` 對 C# 與 Node.js 兩版各驗一輪，確認狀態碼與回應欄位一致
- [ ] 前端以 `VITE_API_BASE_URL` 指向本服務，實測載入與儲存資產
- [ ] 部署到雲端（RENDER、正式環境變數）

## API

| Method | Path | 說明 |
| --- | --- | --- |
| GET | `/health` | 服務狀態 |
| GET | `/health/db` | 資料庫連線狀態（含 `durationMs`） |
| GET | `/assets` | 取得全部資產（摘要，只含辨識欄位） |
| GET | `/assets/{id}` | 取得單一資產 |
| PUT | `/assets/{id}` | 更新單一資產（需管理者 token） |
| POST | `/auth/login` | 管理者登入，回傳 15 分鐘有效的 JWT |
| GET | `/models/{modelKey}` | 取得模型檔（未登記的 key 回 404；尚未實作） |

錯誤回應統一為 `{ "error": { "code", "message" } }`，錯誤碼與狀態碼對照同 Node.js 版
（400 `INVALID_ASSET_ID` / `INVALID_REQUEST_BODY`、401 `INVALID_CREDENTIALS` / `UNAUTHORIZED`、
403 `FORBIDDEN`、404 `ASSET_NOT_FOUND` / `MODEL_NOT_FOUND` / `ROUTE_NOT_FOUND`、500 `INTERNAL_SERVER_ERROR`）。
例外、未知路由、驗證失敗與 Controller 明確回錯都回同一形狀。`/health/db` 失敗回 `{ "status": "error" }`——維運端點，刻意不套錯誤格式（與 Node 版一致）。

## 認證

- `POST /auth/login`：請求本文 `{ "password": "..." }`。密碼正確回 `{ "token": "..." }`；密碼錯誤回 401 `INVALID_CREDENTIALS`。
- token 用 HS256 簽發，有效 15 分鐘，claims 只有 `role: admin`。
- 需要 token 的端點：`PUT /assets/{id}`。用法：標頭 `Authorization: Bearer <token>`。未帶 token、token 過期或簽章不符皆回 401 `UNAUTHORIZED`；角色不符回 403 `FORBIDDEN`。

## Asset

資料形狀與 Node.js 版相同：

```text
Asset
├── id          ← 字串（對應資料庫 BIGINT）
├── name
├── config      ← 整包場景設定（JSONB）
├── updatedAt   ← ISO 8601 字串
└── modelKey    ← 模型檔的 key（資料庫存 key，不存絕對 URL）
```

`config` 的內容會隨資產類型變化，整包存成一欄，不拆成個別欄位。

## Project Structure

```text
Web3D.Backend/
├── Constants/      ApiErrors.cs           錯誤碼常數（與 Node 版一致）
├── Controllers/    AssetController.cs     GET /assets、GET/PUT /assets/{id}（PUT 需管理者 token）
│                   AuthController.cs      POST /auth/login（簽發 JWT）
│                   HealthController.cs    GET /health、GET /health/db
├── Services/       AssetService.cs        資產查詢與更新
│                   AuthService.cs         密碼比對與 token 簽發
├── Data/           AppDbContext.cs        資料庫對應與 DbSet
├── Middleware/     ExceptionMiddleware.cs 未處理例外 → 500，掛管線最前
├── Models/         Asset.cs               Entity，對應 assets 資料表
│                   AssetSummaryDto.cs     清單回應（id / name）
│                   AssetDetailDto.cs      單筆回應（config / updatedAt / modelKey）
│                   UpdateAssetDto.cs      PUT 請求本文（含 JsonObjectOnly 驗證屬性）
│                   LoginDto.cs            登入請求本文（password）
│                   ApiErrorResponse.cs    錯誤回應外層（{"error":{"code","message"}}）
├── Program.cs                         DI 註冊與 middleware 設定
├── appsettings.json                   非機密設定（連線字串留空，由 user-secrets 補）
├── Properties/launchSettings.json     本機啟動參數（埠 3000）
└── docs/learning/                     筆記：architecture（架構總覽）
                                        ＋csharp-basics（C# 與 TS 對照）
                                        ＋ef-core（查詢翻譯與型別對應）
                                        ＋aspnet-core（啟動、請求管線、DI、分層）
                                        ＋auth（JWT、claim、簽發型別鏈）
                                        ＋dotnet-packages（怎麼找、要不要裝套件）
                                        ＋error-handling（四個錯誤出口、與 Node 版對照）
```

## 共用且不可變動的部分
C# 版與 Node.js 版是同一套系統的兩個實作，以下項目共用、不因換語言而改變：

- **API 契約**：端點、請求／回應形狀、錯誤碼與狀態碼
- **資料庫結構**：`assets` 資料表、`schema_migrations` 記錄表
- **Migration 內容**：沿用 Node.js 版的 SQL 檔（`000`–`004`），不新增、不修改

兩版可以接同一顆資料庫：migration 以檔名記錄在 `schema_migrations`，已套用過的會跳過。
要改動上面任一項，必須兩個版本一起改、一起上線，否則前端會遇到行為不一致。

## C# 版與 Node.js 版的差異（實作方式）

只換實作方式，不動上面的契約與資料庫結構：

- **資料存取**：EF Core（Node 版用 `pg` 直接寫 SQL）。資料表結構仍由手寫 SQL migration 管理，
  不啟用 EF Migrations，避免兩套版本工具互相覆蓋。
- **設定來源**：`dotnet user-secrets`，不讀取 `.env`；部署仍由平台環境變數提供。
- **埠**：沿用 `3000`，與前端 `VITE_API_BASE_URL` 的預設值一致（與 Node.js 版同埠，兩版不可同時啟動；要對照驗證時先後啟動即可）。
- **認證**：驗請求的 token 用 `JwtBearer`，簽登入 token 用 `System.IdentityModel.Tokens.Jwt`（HS256、claim 名 `role` 與 Node 版一致）。

## Development

```bash
dotnet restore

# 連線字串：本機存在 user-secrets，不進版控
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=web3d_proto;Username=<帳號>;Password=<密碼>"
dotnet user-secrets list   # 確認 key 名稱是 ConnectionStrings:DefaultConnection
dotnet user-secrets set "JWT_SECRET" "<簽發與驗證 token 共用的隨機長字串>"
dotnet user-secrets set "ADMIN_PASSWORD" "<管理員登入密碼>"
# JWT_SECRET 沒設，啟動就丟例外；ADMIN_PASSWORD 沒設，第一次登入回 500。

# 資料表需先存在：由 Node.js 版套用 migration（000–004）建立，C# 版不執行 migration.
# 資料表不存在時，GET /assets 會回 42P01 relation "assets" does not exist.
# 驗證（另開一個終端機）：curl.exe http://localhost:3000/assets，預期回 [{"id":"1","name":"..."}]
dotnet run                # http://localhost:3000
```

連線字串只在 user-secrets（本機）或 `ConnectionStrings__DefaultConnection`（部署環境變數；
ASP.NET Core 以雙底線表示巢狀 key）提供，名字與 Node.js 版的 `DATABASE_URL` 不同，
這是實作差異、不是契約差異。
其餘名稱維持與 Node.js 版相同：`ADMIN_PASSWORD`、`JWT_SECRET`、`CORS_ORIGIN`、`PORT`
（`CORS_ORIGIN`、`PORT` 待對應功能實作；埠目前由 `Properties/launchSettings.json` 固定為 3000）。

## Non-goals

- `POST` / `DELETE /assets`：App 本身不新增、也不刪除資產
- `PATCH /assets/{id}`
- 自動化測試
- 物件儲存與 CDN

## Related Project

- 前端：[`web3D_proto_react`](https://github.com/maxyeh666/web3D_proto_react)
- Node.js 版後端：[`web3D_proto_backend`](https://github.com/maxyeh666/web3D_proto_backend)

前端以 `VITE_API_BASE_URL` 指定後端位址（預設 `http://localhost:3000`）。
未設定時，前端改用本機資料來源，不發出任何網路請求。
