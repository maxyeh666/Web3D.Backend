# 學習筆記（C# 後端）

> 這裡記錄 C# 後端實作過程中搞懂的概念，一問一答：問題寫成脫離對話也看得懂的完整句子。
> 對應的程式變更與設計理由見 [CHANGELOG.md](./CHANGELOG.md)。

## 主題索引

- **專案初始化與工具鏈** — 11-11
- **C# 語法對照 TypeScript** — 11-11
- **設定檔與啟動流程** — 11-11

---

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
