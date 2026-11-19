# C# 語言基礎與 TypeScript 對照

記「只寫過 Node / TypeScript 的人會誤解或想問」的 C# 概念。實作當天的問答仍留 `LEARNING.md` 的日期段，這裡只收可反覆查閱的說明。

先看全貌（一次請求走過哪些地方）：[架構總覽](./architecture.md)。

## 型別、可空性與型別推斷

型別關鍵字是小寫（`string`、`int`、`bool`）；`string?` 尾端的 `?` 表示「可為 null」（相當於 TS 的 `string | null`），由專案檔的 `<Nullable>enable</Nullable>` 開啟檢查，編譯器會擋下未檢查就使用的可空值。

`var` 是型別推斷（編譯器從等號右邊推斷型別，相當於 TS 省略型別註記讓編譯器推斷），不是動態型別，之後不能換成別的型別。

## 命名慣例、字串插值與非同步

型別與方法用 PascalCase（`AssetService`、`GetAsset`），區域變數與參數用 camelCase。字串插值 `$"Hello {name}"` 相當於 TS 的樣板字串。

`async` / `await` 寫法與 TS 幾乎相同，只是非同步方法的回傳型別寫成 `Task<T>`（表示一個非同步工作，相當於 `Promise<T>`）。集合方面，`List<T>` 相當於 `T[]`，`Dictionary<string, string>` 相當於 `Record<string, string>`。

字典的比較子：預設用大小寫敏感、不看地區語言規則的字串比較（`StringComparer.Ordinal`，寫在建構子參數，可換成 `OrdinalIgnoreCase` 等；地區語言規則例如土耳其語的 `i` 大寫不是 `I`，契約字串不用這套）。JS 的物件查表走原型鏈（原型鏈：找不到屬性時往上找的鏈——自己的屬性 → 建構函式的 `prototype` → `Object.prototype`），`table["constructor"]` 會查到 `Object` 的建構子，Node 版因此用 `typeof` 擋掉；C# 的 `Dictionary` 是獨立的雜湊表（用 key 算出存放位置的資料結構，沒有往上找的鏈），沒有原型鏈這一層。所以 C# 這邊不需要擋原型名稱，但 key 必須是同一比較規則下的相等字串——契約裡的 `modelKey` 大小寫不同就是不同的 key。

## using 區塊與資源釋放（TS 沒有）

`using` 區塊保證離開時自動釋放資源（例如資料庫連線「用完即歸還」），相當於 Node 版 `migrate.ts` 用 `try` / `finally` 歸還連線的那段邏輯。

## 屬性（Property，TS 沒有）

`public string Name { get; set; }` 看起來像欄位，實際是帶讀寫方法的成員，EF Core 靠它對應資料表欄位。

## 例外處理

`throw new ...Exception(...)` 相當於 `throw new Error(...)`；C# 習慣用多個 `catch` 按例外型別分流，相當於 Node 版用一個 errorHandler 按 `code` 分流，兩種做法在本專案效果相同。

## null 檢查：`is null` 與 `== null`

兩者都能判斷「沒有值」，但在 C# 裡語意不同。`==` 是運算子（operator：型別可以自己定義的符號行為），型別可以覆寫它；`is null` 是模式比對語法，由編譯器直接產生「比較參考是否為 null」的指令，不會經過任何自訂運算子。

| 比較項目 | `is null` | `== null` |
| --- | --- | --- |
| 是否受型別覆寫影響 | 否，永遠是參考比較 | 是，執行的是該型別定義的運算子 |
| 典型差異案例 | 一律判斷「沒有物件」 | `string` 覆寫成內容比較，`"abc" == "abc"` 為 true；特殊型別可能與 null 比較時拋例外 |
| 效能 | 一次比較指令，無方法呼叫 | 若被覆寫即為一次方法呼叫 |
| EF Core 查詢 | 可翻譯成 SQL 的 `IS NULL` | 同樣可翻譯成 `IS NULL`；但若型別覆寫了 `==`，運算式樹記錄的是自訂運算子，EF 翻譯不出來會拋 `InvalidOperationException` |
| 對結構型別 | 僅適用可為 null 的型別（參考型別、`int?`） | 型別必須有定義 `==`，否則編譯失敗 |
| 可讀性 | 與 `is not null`、`is { } value` 等模式比對語法一致 | 與 `!=` 對稱 |

專案內的慣例是一律使用 `is null` 與 `is not null`，理由是行為不受型別實作影響，且與 C# 7 之後的模式比對語法一致。`object.ReferenceEquals(x, null)` 效果相同但冗長，不使用。

跨語言對照：TypeScript 慣用 `x == null`（寬鬆比較）一次涵蓋 `null` 與 `undefined` 兩種空值；C# 沒有 `undefined`，空值只有 `null` 一種，因此不需要這種寫法。C# 對應 `?.`（空值傳播：遇到 null 就整段回傳 null）與 `??`（空值合併：左邊是 null 時取右邊）的用法與 TS 相同。

## out 參數與 TryParse 模式

`long.TryParse(id, out var assetId)` 用 out 參數（輸出參數：方法除了回傳值以外，額外把結果寫進呼叫端指定的變數）帶出解析結果，失敗時不拋例外。它預設允許數字前後有空白，也允許正負號，因此 `+1`、` 1` 都會解析成 1。

要完全符合「只接受以 1-9 開頭的正整數字串」的規則，需自行逐字元檢查或先比對正規表達式。

專案的 id 檢查是「先正規表達式、再 `TryParse`」兩段：正規表達式負責契約（1-9 開頭、其餘數字），`TryParse` 只負責擋掉超過 `long` 範圍的長數字，避免送出必定失敗的查詢。樣式寫成 `^[1-9]\d*$`；若誤寫成 `^[1-9]\d\d*$`，其中 `\d\d*` 等於 `\d+`（至少再一位數字），會把 `1`、`2` 這種個位數 id 判成不合法——正向的最小案例（合法輸入中最短的那個）必須一起測，只驗「不合法的被擋掉」看不出這種錯。
## 型別選擇：同一件事挑哪個型別

同一份資料在不同位置用不同型別存放，下表記「本專案用哪個、換另一個會怎樣」。

| 場景 | 本專案用 | 替代選項 | 為什麼 |
| --- | --- | --- | --- |
| id 回給前端 | `string`（`Id.ToString()`） | `long` | BIGINT 超過 JS 安全整數（2 的 53 次方減 1），前端會失真；與 Node 版 pg 驅動輸出一致 |
| config 存在 Entity | `string` | `JsonDocument`／`JsonElement`／強型別 class | 整包存整包取，內容隨資產類型變動，不拆欄位；`JsonDocument` 需釋放，不適合當長期屬性。以後要強型別再換型別，屆時只要改這個屬性 |
| config 在 DTO | `JsonElement` | `string`／`JsonNode` | 序列化即成 JSON 物件（`string` 會被再轉義成字串）；只讀不改 |
| 整理 config | `JsonNode` | `JsonElement` | 要加鍵、刪鍵、重建，`JsonElement` 唯讀；`DeepClone` 避開「一個節點只能有一個父節點」的限制 |
| 查詢結果 | `List<T>`（`ToListAsync` 收成） | `IQueryable<T>`／`IEnumerable<T>` | `IQueryable<T>` 是查詢描述，還沒打資料庫；回傳它會讓呼叫端意外再查一次 |
| action 回傳 | `IActionResult` | `ActionResult<T>` | 200／400／404 多出口時它最直觀；`ActionResult<T>` 能讓 OpenAPI 推出回應型別（目前列為已知限制） |
| 時間 | `DateTimeOffset`（Entity 與輸出）、`DateTime`（簽 token 取現在） | 全用 `DateTime` | 前者帶時區位移，對應資料庫 timestamptz；JWT 的 `iat` 只需要「現在」這個瞬間，`DateTime.UtcNow` 即可 |
| 逾時 | `TimeSpan.Zero`（`ClockSkew`） | 秒數 `int` | API 要求 `TimeSpan` 型別，`5` 不會被當成 5 秒 |
| 查無資料 | 回 `null` | 全拋例外 | 可預期的業務結果回 `null`（Controller 轉 404）；環境問題（設定缺失、DB 失敗）才拋給 `ExceptionMiddleware` |

## 介面（interface）與框架要求的型別

介面只宣告成員簽章、不給實作；類別宣告實作它，就必須提供那些成員。框架用介面表示「規格」，實作可以換（DI 註冊的對象、測試替身），呼叫端只認介面。

與 TypeScript 的 `interface` 不同的三點：TypeScript 是結構型別（形狀對就符合），C# 是名目型別（必須明確宣告實作）；TypeScript 的介面編譯後消失，C# 的介面是執行期存在的型別；C# 的介面常用在 DI 與框架擴充點，TypeScript 的介面主要做靜態檢查。

### 本專案用到的介面與框架型別

種類欄只為分辨寫法（介面／抽象類別／基底類別／委派），不是難度分級。範圍只收程式碼真的有出現的。

| 成員 | 種類 | 誰提供 | 本專案在哪用 | 為什麼是它 |
| --- | --- | --- | --- | --- |
| `IConfiguration` | 介面 | 共享框架（`Microsoft.Extensions.Configuration`） | `AuthService` 建構子注入，讀 `JWT_SECRET`、`ADMIN_PASSWORD` | 讀哪個來源（appsettings／環境變數／user-secrets）由框架疊加，程式只讀最終結果；換來源不改程式。來源順序見 [aspnet-core.md](./aspnet-core.md) |
| `ILogger<T>` | 泛型介面 | 共享框架 | 兩個 middleware | `T` 是分類標籤，輸出附類別名；換輸出目的地（主控台／檔案／雲端）只改啟動設定，不動程式 |
| `IActionResult` | 介面 | ASP.NET Core | 四個 Controller 的 5 個 action | 同一個 action 可能回 200／400／404；`Ok()`、`NotFound()`、`BadRequest()`、`Unauthorized()` 都是它的實作 |
| `IWebHostEnvironment` | 介面 | ASP.NET Core | `ModelController` 取 `ContentRootPath` 組模型檔路徑 | 啟動方式不同，根目錄不同；測試可換假實作。見 [static-files.md](./static-files.md) |
| `IQueryable<T>` | 介面 | BCL（LINQ） | `AssetService` 的 LINQ 鏈（程式碼未明寫型別，鏈式呼叫推出） | 查詢描述、延遲執行，EF 才能翻譯成 SQL。見 [ef-core.md](./ef-core.md) |
| `IDisposable` | 介面 | BCL | `using var jsonDoc = JsonDocument.Parse(...)` | `using` 區塊的依據；`JsonDocument` 持有租用來的緩衝區，離開時自動歸還 |
| `ValidationAttribute` | 抽象類別 | DataAnnotations | `JsonObjectConfigAttribute` | 驗證屬性的固定基底，覆寫 `IsValid` 即可接入模型驗證管線 |
| `ControllerBase` | 基底類別 | ASP.NET Core | 四個 Controller | 只給回應方法與 `Request`／`Response`，不含 Views |
| `DbContext`／`DbContextOptions<T>` | 基底／泛型類別 | EF Core | `AppDbContext` | 前者是所有查詢的進入點，後者承接連線字串等啟動設定 |
| `RequestDelegate` | 委派（委派：指向方法的型別，可像變數一樣傳遞呼叫） | ASP.NET Core | 兩個 middleware 建構子的 `next` | 代表「管線的下一站」；呼叫它才往後走 |

## System.Text.Json 常用 method

範圍只收程式碼真的有用到、名字看不出行為的；`TryParse`、`IsNullOrWhiteSpace` 這類標準寫法不列。

| method | 所屬型別 | 做什麼 | 本專案用在哪 |
| --- | --- | --- | --- |
| `JsonDocument.Parse` | `JsonDocument` | 把 JSON 字串解析成唯讀文件 | `AssetService.ToDetailDto` |
| `RootElement` | `JsonDocument` | 文件的進入點 | 同上 |
| `Clone` | `JsonElement` | 複製成不依賴原文件的獨立節點 | 同上（文件離開 `using` 即失效，先 Clone 再回傳） |
| `GetRawText` | `JsonElement` | 取出原始 JSON 文字，不重新序列化 | `UpdateAssetAsync` 寫入 `Config` |
| `JsonNode.Parse` | `JsonNode` | 解析成可修改的物件樹 | `NormalizeConfig` |
| `DeepClone` | `JsonNode` | 深複製；避開「一個節點只能有一個父節點」 | 同上（三鍵重建） |
| `SerializeToElement` | `JsonSerializer` | 把物件轉成 `JsonElement` | 同上（重建後寫回 `Config`） |
| `TryGetProperty`／`TryGetDouble` | `JsonElement` | 取不到時回 false，不拋例外 | `IsValidCamera` 等逐層驗證 |
| `EnumerateArray`／`EnumerateObject` | `JsonElement` | 逐一走訪陣列元素／物件屬性 | position 逐值檢查、overrides 逐項檢查 |
| `GetArrayLength` | `JsonElement` | 陣列長度 | position 長度須為 3 |
