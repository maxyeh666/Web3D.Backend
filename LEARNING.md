# 學習筆記（C# 後端）

> 這裡記錄 C# 後端實作過程中搞懂的概念，一問一答：問題寫成脫離對話也看得懂的完整句子。
> 對應的程式變更與設計理由見 [CHANGELOG.md](./CHANGELOG.md)。

## 主題索引

---
- **後端分層架構與請求流程** — 11-12
- **本機使用方式與設定驗證** — 11-12
- **EF Core 查詢翻譯與型別對照** — 11-12
- **專案初始化與工具鏈** — 11-11
- **C# 語法對照 TypeScript** — 11-11
- **設定檔與啟動流程** — 11-11

## 2026-11-12 — 分層架構、使用方式與查詢翻譯

**學習筆記**

- 問：ASP.NET Core 的專案分成 Controller、Service、DbContext、Entity、DTO 這幾種角色，一個 GET /assets 請求會依序經過哪些檔案？

  答：請求先進 Controller（Controllers/AssetController.cs，只負責接請求、轉交、包回傳），再進 Service（Services/AssetService.cs，寫查詢邏輯），Service 透過 DbContext（Data/AppDbContext.cs，EF Core 的進入點，DbSet 對應 assets 資料表）讀出 Entity（Models/Asset.cs，對應一行資料的類別），Service 再轉成 DTO（Models/AssetSummaryDto.cs，只給前端看的形狀）回傳。對照 Node 版：routers/assets.ts 到 services/assetService.ts 到 db.ts，只是 C# 每一層都有具名型別，編譯時期就會檢查形狀對不對。

- 問：Entity（實體）是什麼？它跟資料表、跟 Node.js 版直接寫 SQL 查出來的 row 有什麼不同？

  答：Entity 是資料表的一行對應成的類別（Models/Asset.cs）。跟資料表的關係是一對一：屬性（Property：長得像欄位、實際帶讀寫方法的成員）對欄位，對應規則寫在 AppDbContext.OnModelCreating。跟 Node 版的差別：Node 寫 SQL 拿回的是 pg 驅動組出的普通物件，沒有型別約束，打錯欄位名要到執行才會發現；Entity 有型別，欄位名打錯編譯就失敗，查詢也寫成同一套 LINQ 語法（C# 內建的集合查詢語法，長得像 SQL 的方法鏈），不用拼 SQL 字串。

- 問：依賴注入（DI）是什麼？為什麼 Controller 不自己 new AssetService，而是寫在建構子參數裡？

  答：依賴注入（DI：物件不自己建立它需要的東西，而是在被建立時由框架傳進來）是把誰建立物件交給框架（Program.cs 的 builder.Services）。Controller 的建構子宣告需要 AssetService，框架收到請求時自動建立並傳入。好處是 Controller 不必知道 AssetService 怎麼建（它自己還需要 AppDbContext），要換實作或測試時只要換註冊、不改呼叫端。對照 Node 版：用 import 直接引用模組單例，沒有框架按參數型別自動組裝這一層。

- 問：DI 註冊的 Scoped、Transient、Singleton 差在哪？這個專案為什麼用 Scoped？

  答：三者是物件的壽命：Transient 每次要都建新的；Scoped 同一個 HTTP 請求內共用一份、請求結束釋放；Singleton 整個程式只建一份、用到的關機。AddDbContext 預設就是 Scoped，AssetService 也註冊為 Scoped：每個請求一份 DbContext，用完即丟（連線歸還到 Npgsql 的連線池），不會跨請求污染狀態。對照 Node 版沒有這種區分：模組載入一次就整個 process 共用；資料庫連線靠連線池手動借還（相當於本專案 Scoped 背後做的事）。

- 問：DTO 是什麼？為什麼不直接把 Entity 回傳給前端？

  答：DTO（Data Transfer Object，資料傳輸物件：只為了傳輸而定義的形狀）是給呼叫端看的形狀，Entity 是給資料庫看的形狀。兩邊需求不同：Entity 有全部欄位（含 config 整包、時間戳），前端列表只需要 id 和 name；直接回傳 Entity 會把不需要的欄位送出去，日後資料表加欄位還會直接牽動前端。用 Models/AssetSummaryDto.cs 隔開後，資料庫長什麼樣就不會影響前端契約。對照 Node 版：在 service 裡手寫挑欄位的回傳物件，這裡把挑什麼變成一個具名型別。

- 問：Program.cs 沒有 Main 方法，程式從哪裡開始跑？middleware 的順序為什麼要照現在的排法？

  答：.NET 6 之後支援頂層陳述式（top-level statements：不用寫 class Main，直接寫流程式碼、編譯器自動包成進入點），Program.cs 從上到下就是啟動流程：讀設定、註冊服務、建出 app、掛 middleware、Run 啟動。middleware（請求進來後會依序經過的一串處理步驟）順序有意義：UseHttpsRedirection 先處理 http 轉 https（本機沒配 https 埠時直接放行）；UseAuthorization 必須在掛路由之前，否則請求還沒檢查身分就進 Controller；掛路由相當於 Node 版掛路由器的那幾行。

- 問：本機要怎麼把這個後端跑起來？連線字串放在哪裡、怎麼確認真的連上資料庫？

  答：三步。先確保資料表存在（目前由 Node.js 版套用 000 到 004 的 migration，C# 版不執行）。再用 dotnet user-secrets set 寫入 ConnectionStrings:DefaultConnection（存在本機個人資料夾、不進版控；key 名稱由程式碼決定）。最後 dotnet run（http://localhost:3000），另開終端機打 curl.exe http://localhost:3000/assets，預期回一串資產陣列，即代表設定、連線、查詢、路由全通。連不上時看訊息屬於哪一層：The ConnectionString property has not been initialized 是設定層（值是空的）；42P01 是資料表不存在。

- 問：dotnet user-secrets 的 key 名稱是怎麼決定的？打錯字會出現什麼錯誤，要怎麼從訊息反推原因？

  答：key 由程式碼決定，不是隨便取：GetConnectionString 一律去找 ConnectionStrings 加上括號裡的名字。打錯（例如少一個 s）時設定系統找不到值、回傳空字串；空字串傳進 UseNpgsql 在啟動時不會檢查，直到第一次真的查詢才由 Npgsql 拋出 The ConnectionString property has not been initialized。反推方法：訊息裡出現沒有被初始化代表 Npgsql 拿到的是空值，沿程式碼往回查設定來源（appsettings 到 user-secrets），用 dotnet user-secrets list 對照程式裡的字串，即可找到打錯的 key。

- 問：EF Core 的 LINQ 查詢什麼時候才會真的打資料庫？Id.ToString() 為什麼能變成 SQL 的 CAST？

  答：LINQ 方法（Where、OrderBy、Select）只是組裝查詢描述、不執行，叫延遲執行（先描述要什麼資料，真正需要時才執行；JS 的 map 與 filter 是立即執行，這裡相反）。ToListAsync 那一刻，EF Core 才把整段翻譯成一條 SQL 送出去。Id.ToString 能翻譯，是因為 Npgsql 的 provider 內建了一批 C# 方法對 SQL 寫法的對應（long 的 ToString 對應 CAST(id AS text)）；能翻譯的在資料庫端執行、不能翻譯的會拋錯，本次查詢已實測通過。

- 問：同一筆資料在 PostgreSQL、C#、JSON 三層的型別怎麼對照？

  答：對照點有四個。id 是 BIGINT（64 位元整數）到 C# 用 long 接（64 位元整數型別）到 JSON 給字串，因為 BIGINT 會超過 JS 的安全整數範圍（2 的 53 次方減 1，超過後數字失真），做法與 Node 版 pg 驅動預設一致；config 是 jsonb（PostgreSQL 的一種欄位型別，可以直接把 JSON 物件存進資料庫）到 C# 先用 string 接整包，之後需要強型別再換 JsonDocument；updated_at 是 timestamptz（含時區的時間戳）到 C# 用 DateTimeOffset 接；自動編號是 GENERATED ALWAYS AS IDENTITY 到 C# 用 UseIdentityAlwaysColumn 對應，新增時不要自己塞 Id（塞了會被忽略）。

## 2026-11-11 — 專案初始化、語法對照與設定載入

**學習筆記**

- 問：`dotnet new webapi` 產生了哪些檔案，各自對應 Node.js 版的什麼東西？

  答：樣板產生六類檔案。`Web3D.Backend.csproj`（XML 格式的專案檔）列出目標框架（`net10.0`，即 .NET 10）與 NuGet 套件（.NET 的套件來源，相當於 npm registry），地位等同 Node 版的 `package.json`；`dotnet add package` 相當於 `npm install <套件>`，`dotnet restore` 相當於 `npm install`（下載全部套件）。目前唯一的套件是 `Microsoft.AspNetCore.OpenApi`（自動產生 API 規格文件的官方套件，實作階段才會用到）。

  `Program.cs` 是啟動檔，對應 Node 版的 `src/index.ts`：`WebApplication.CreateBuilder(args)` 建立應用程式建構器，`builder.Services.AddControllers()` 把控制器註冊進依賴注入容器（框架內建的物件工廠，負責建立並提供各類別的實例），`builder.Build()` 建出應用本體，`app.MapControllers()` 掛上路由（相當於 `app.use('/assets', assetsRouter)` 那三行），`app.Run()` 啟動監聽（相當於 `app.listen(PORT)`）。

  `appsettings.json` / `appsettings.Development.json` 是 JSON 設定檔，對應 Node 版的 `.env` 加讀取邏輯。`Properties/launchSettings.json` 只有用 `dotnet run` 或 IDE 按 F5 啟動時有效（決定埠與環境變數），部署後無效，可視為本機啟動參數。`Web3D.Backend.http` 是樣板附的手動測試檔，因指向專案沒有的 `/weatherforecast` 已刪除，待 Phase 1 有真端點後重建。`.gitignore` 由 `dotnet new gitignore` 產生，`bin/` 與 `obj/`（編譯產物，對應 Node 版的 `dist/`）已排除在版控外。

- 問：C# 的基本語法與 TypeScript 有哪些可以直接對照的地方？

  答：多數概念一一對應，差異主要在大小寫與預設值。型別關鍵字是小寫（`string`、`int`、`bool`）；`string?` 尾端的 `?` 表示「可為 null」（相當於 TS 的 `string | null`），由專案檔的 `<Nullable>enable</Nullable>` 開啟檢查，編譯器會擋下未檢查就使用的可空值。`var` 是型別推斷（編譯器從等號右邊推斷型別，相當於 TS 省略型別註記讓編譯器推斷），不是動態型別，之後不能換成別的型別。

  命名慣例：型別與方法用 PascalCase（`AssetService`、`GetAsset`），區域變數與參數用 camelCase。字串插值 `$"Hello {name}"` 相當於 TS 的樣板字串。`async` / `await` 寫法與 TS 幾乎相同，只是非同步方法的回傳型別寫成 `Task<T>`（表示一個非同步工作，相當於 `Promise<T>`）。集合方面，`List<T>` 相當於 `T[]`，`Dictionary<string, string>` 相當於 `Record<string, string>`。

  兩個 TS 沒有的東西：`using` 區塊保證離開時自動釋放資源（例如資料庫連線「用完即歸還」），相當於 Node 版 `migrate.ts` 用 `try` / `finally` 歸還連線的那段邏輯；屬性（Property）`public string Name { get; set; }` 看起來像欄位，實際是帶讀寫方法的成員，EF Core（.NET 的資料庫對應工具，把資料表對應成物件）靠它對應資料表欄位。

  錯誤處理：`throw new ...Exception(...)` 相當於 `throw new Error(...)`；C# 習慣用多個 `catch` 按例外型別分流，相當於 Node 版用一個 errorHandler 按 `code` 分流，兩種做法在本專案效果相同。

- 問：C# 版的設定值從哪裡來？多個來源同時存在時誰贏？

  答：來源按覆寫順序（後者贏）是：`appsettings.json` → `appsettings.{環境}.json`（例如 Development 用 `appsettings.Development.json`，只放與正式不同的值）→ user-secrets（開發機私密值）→ 環境變數 → 命令列參數，由框架在啟動時自動疊加，程式只讀最終結果。

  `dotnet user-secrets set "Jwt:Key" "..."` 把私密值存在本機個人資料夾的 JSON 檔，只在 Development 環境載入、不進版控；它取代的是 `.env` 的位置，但粒度不同——`.env` 是整個檔案一份，user-secrets 是按 key 一筆一筆存。部署時改由平台環境變數提供，與 Node 版相同（`DATABASE_URL` 等名稱不變），所以正式環境不需要任何本機檔案。

  指令對照：`dotnet run` 是編譯並執行（對應 `npm run dev`），`dotnet build` 是只編譯不執行（對應 `npm run build`），產物放在 `bin/` 與 `obj/`（對應 `dist/`，皆不進版控）。`launchSettings.json` 的 `applicationUrl`（目前 `http://localhost:5154`）只影響 `dotnet run`，Phase 1 會改為 `3000` 與前端預設一致。

---
