# 架構總覽：一次請求走過哪些地方

> 本文件說明系統整體結構、請求流程與檔案對應。細節分別見：C# 語法見 [csharp-basics.md](./csharp-basics.md)，資料庫對應見 [ef-core.md](./ef-core.md)，框架機制見 [aspnet-core.md](./aspnet-core.md)。

## 這個後端在做什麼

前端（React / Three.js）在瀏覽器中執行，需要 3D 場景設定時以 HTTP 向後端請求；後端僅回傳 JSON，不產生任何網頁；資料存放於 PostgreSQL 的 `assets` 資料表。

| 誰 | 負責什麼 | 跑在哪裡 |
| --- | --- | --- |
| 前端（web3D_proto_react） | 畫面與互動 | 瀏覽器，預設請求 `http://localhost:3000` |
| 後端（本專案） | 提供資產設定的讀寫介面 | 本機 `http://localhost:3000`（埠由 `Properties/launchSettings.json` 指定） |
| PostgreSQL | 存放 `assets` 資料表 | 本機 5432；連線字串存放於本機 user-secrets，不進版控 |

本專案與 Node.js 版為同一套系統的兩種實作：連接同一個 PostgreSQL 資料庫、遵循同一套端點契約，前端無需修改程式。兩者差異僅在實作方式；契約與共用規則見 [README](../../README.md)。

## 一次請求的完整旅程

下表列出一次請求經過的各階段。以 `GET /assets/1` 為例，自瀏覽器發出至回應返回，共 7 個階段；各階段的機制細節見括號所指文件。

| 序 | 階段 | 說明 | 關鍵名詞 | 本專案對應檔案 | Node 版對應 |
| --- | --- | --- | --- | --- | --- |
| 起 | 1 啟動與管線掛載 | 程式啟動時決定請求依序經過的處理步驟 | middleware | `Program.cs` | `app.use(...)` |
| 2 | 2 路由比對 | 依網址比對樣板，決定由哪個方法處理 | routing、route constraint | `[Route("assets")]`、`[HttpGet("{id}")]` | `router.get('/:id')` |
| 3 | 3 參數繫結 | 將網址中的 `"1"` 轉換為數字、請求本文的 JSON 轉換為物件 | model binding | Controller 方法的參數 | `req.params` / `req.body`（自行取值與轉型） |
| 4 | 4 參數驗證 | 執行驗證屬性；驗證失敗直接回傳 400，不進入方法 | model validation、ModelState | `UpdateAssetDto` 上的 `[Required]` | 自行撰寫 `if` 判斷 |
| 5 | 5 方法執行 | 執行方法內的程式碼，呼叫 Service | Action | `AssetController.GetAssetDetail` | handler 函式本體 |
| 6 | 6 資料庫查詢 | 將查詢轉換為 SQL，讀回 Entity 或投影為 DTO | EF Core、DbSet、IQueryable、變更追蹤 | `AssetService`、`AppDbContext` | `pool.query('SELECT ...')` |
| 7 | 7 回應封裝 | 將物件序列化為 JSON，附加狀態碼後回傳前端 | serialization、status code | `Ok()` / `NotFound()` | `res.json()` / `res.status(404)` |

任一階段拋出未處理的例外，回應結果為 500（由最外層的 `ExceptionMiddleware` 收斂成契約形狀，見[統一錯誤處理](./error-handling.md)）。

## 為什麼要分層

各層的設計理由以移除後的後果說明：

| 移除對象 | 後果 | 本專案對應情況 |
| --- | --- | --- |
| Controller（接收請求、轉交 Service、封裝回應） | 路由、狀態碼與商業邏輯耦合於同一方法；後台若需第二個入口，必須複製整段邏輯 | Phase 4 的管理端點將重用同一批 Service 方法 |
| Service（查詢與商業邏輯） | 驗證邏輯需先啟動 HTTP 伺服器；更換資料來源（例如由 SQL 改為快取）需修改所有端點 | `AssetService` 的方法可供 Controller 以外的呼叫端使用 |
| Entity 與 DTO 未分離 | 資料表新增欄位將直接暴露給前端；前端列表實際僅需 id 與 name | `AssetSummaryDto` 僅含兩個屬性，`Asset` 含七個 |
| 未使用 EF Core、自行撰寫 SQL | 欄位名稱錯誤須至執行階段方能發現，無編譯期檢查 | `entity.Property(asset => asset.Name)` 若欄位名稱錯誤，編譯即失敗 |

分層的代價為檔案數量增加，以及每次 Entity 轉換為 DTO 的成本；小型專案可能顯得冗餘。以本專案的規模（多個端點、多種回傳形狀）而言，該成本合理。

## 名詞地圖

名詞依來源分為四類。每條寫到「這是什麼、為什麼需要」為止，機制細節見該條連結。閱讀建議：**D 類須能說明設計理由**，A 類使用時再查，B、C 類查閱文件或 IDE 提示即可。

### A. C# 語言

- **`namespace`**：名稱空間，把型別分組、避免撞名，相當於 TS 的模組路徑概念
- **屬性（Property）**：長起來像欄位，實際是帶讀寫方法的成員，見 [csharp-basics.md](./csharp-basics.md)
- **`readonly`**：唯讀欄位，只能在宣告或建構子賦值，之後不能改；用來保證 Controller 拿到的 Service 不會被換掉
- **可空性 string?**：尾端問號表示可為 null，見 [csharp-basics.md](./csharp-basics.md)
- **`null!`**：空值寬容運算子，告訴編譯器「我知道這裡可能是 null，但先不要警告」；`AppDbContext` 的 `DbSet` 用它因為框架啟動後才賦值
- **`[Attribute]`**：標記，掛在類別或成員上的宣告式註記，框架在執行時讀它，例如 `[Route]`、`[Required]`
- **`using` 指示詞**：檔案開頭的 `using X;`，引入名稱空間，相當於 `import`；與「`using` 區塊（保證資源釋放）」同名不同物
- **`async` / `Task<T>`**：見 [csharp-basics.md](./csharp-basics.md)
- **`throw` / `catch`**：見 [csharp-basics.md](./csharp-basics.md)
- **`var`**：型別推斷，見 [csharp-basics.md](./csharp-basics.md)

### B. ASP.NET Core 框架（框架規定）

- **建構器與服務集合**：`builder` 是程式啟動期的設定入口（讀設定、掛 middleware）；`builder.Services` 是一張「型別 → 怎麼建立」的登記表，DI 依這張表解析依賴
- **依賴注入容器（DI）**：物件不自己建立需要的東西，由框架依建構子參數型別建立後傳入。目的是更換實作或寫測試時只改 `Program.cs` 的註冊，不必動呼叫端；代價是依賴關係只出現在建構子，註冊漏了要到啟動或首次請求才發現。原理與壽命見 [aspnet-core.md](./aspnet-core.md)
- **middleware**：請求進來後依序經過的一串處理步驟。用途是把「每個端點都要做」的事（HTTPS 導向、認證、例外處理）集中在 `Program.cs`，不必在各 Action 重寫；代價是順序錯了行為就錯，且從 Action 看不出經過哪些步驟
- **routing**：路由，依網址決定由哪個方法處理
- **model binding**：模型繫結，HTTP 文字轉方法參數
- **model validation**：模型驗證，跑驗證屬性
- **ModelState**：以屬性名稱為 key 的錯誤清單
- **ProblemDetails**：微軟定義的錯誤格式
- **`[ApiController]`**：標記，啟動三個框架行為：驗證失敗直接回 400 不進方法、依參數型別推斷來源（本文／網址／查詢）、錯誤回應改用 ProblemDetails（第三點本專案已用 `InvalidModelStateResponseFactory` 換成契約形狀）
- **`ControllerBase`**：純 API 控制器的基底類別，相對於含 Views 的 `Controller`
- **`IActionResult`**：回應的介面，`Ok()`、`NotFound()`、`BadRequest()` 都是它的實作
- **序列化（serialization）**：物件轉 JSON，本專案預設屬性名轉 camelCase

### C. EF Core（半框架半工具）

- **`DbContext`**：EF Core 的進入點，每個 HTTP 請求由 DI 給一份
- **`DbSet`**：資料表的查詢入口，`_db.Assets` 就是 `assets` 這張表
- **`IQueryable`**：延遲執行的查詢描述，LINQ 組出它，`ToListAsync` 那一刻才翻成 SQL。好處是欄位名打錯編譯就失敗；代價是組裝階段看不出最後送出的 SQL，見 [ef-core.md](./ef-core.md)
- **Entity**：資料庫一行的形狀；與 DTO 分開的理由見〈為什麼要分層〉
- **變更追蹤（change tracking）**：EF 記住讀出的物件改了哪些欄位，`SaveChangesAsync` 只送改過的欄位。代價是每個被追蹤的物件都保留一份原始值快照，只讀不寫的查詢適合加 `AsNoTracking()`；本專案三種查詢的差別見 [ef-core.md](./ef-core.md)
- **migration**：資料表結構的版本管理；本專案的 migration 由 Node 版套用，C# 版不跑

### D. 本專案自訂

- **Controller**：只接請求、轉交、包回傳
- **Service**：查詢與商業邏輯
- **Entity**：資料庫一行的形狀
- **DTO**：給前端看的形狀

這四個不是框架規定的，是本專案選的切法；各自移除後的後果見〈為什麼要分層〉。

## 檔案地圖

| 檔案 | 執行時機 | 呼叫端 | 說明 |
| --- | --- | --- | --- |
| `Program.cs` | 啟動時一次 | 程式進入點 | 讀設定、註冊服務、掛 middleware 與路由 |
| `Controllers/AssetController.cs` | 每個請求一次 | 框架（routing 挑中後呼叫） | 接請求、呼叫 Service、把結果包成 `Ok()` / `NotFound()` |
| `Services/AssetService.cs` | 每個請求一次 | Controller | LINQ 查詢與 Entity 轉 DTO |
| `Data/AppDbContext.cs` | 每個請求一份（Scoped），請求結束釋放 | DI 傳給 Service | 描述類別對資料表；`DbSet` 是查詢入口 |
| `Models/Asset.cs` | 不執行（只有形狀） | EF Core 在查詢時使用 | 資料表一行的形狀（Entity） |
| `Models/*Dto.cs` | 不執行（只有形狀） | Controller 與 Service | 回給前端的形狀 |
| `appsettings*.json`、user-secrets | 啟動時一次 | `Program.cs` 讀取 | 連線字串等設定；覆寫順序見 [aspnet-core.md](./aspnet-core.md) |

注意 `AppDbContext` 的生命週期：同一 HTTP 請求內共用一份，請求結束即釋放（連線歸還至 Npgsql 連線池）。因此不應在 Service 之外保存其實例，亦不應將其註冊為 Singleton。

## 接下來讀哪

- 看到不懂的語法（`?`、`var`、`is null`、`using`、`Task`）→ [csharp-basics.md](./csharp-basics.md)
- 實作細節需查閱機制時 → [aspnet-core.md](./aspnet-core.md)
- 需修改資料庫（查詢、新增欄位、變更型別）時 → [ef-core.md](./ef-core.md)
- 需查閱實作背景時 → [LEARNING.md](../../LEARNING.md) 的按天日誌
