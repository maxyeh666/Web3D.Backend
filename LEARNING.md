# 學習筆記（C# 後端）

> 大主題（跨日累積的概念）說明拆到 `docs/learning/`，按天日誌、問答與小型主題留在這裡。
> 對應的程式變更與設計理由見 [CHANGELOG.md](./CHANGELOG.md)。

## 大主題（說明在主題檔）

- [C# 語言基礎與 TypeScript 對照](./docs/learning/csharp-basics.md) — 型別、可空性、`is null`、out 與 TryParse、例外處理
- [EF Core：查詢翻譯、型別對應與預設值](./docs/learning/ef-core.md) — 查詢翻譯、延遲執行、三層型別對照、欄位預設值
- [ASP.NET Core：啟動、請求管線、分層與 DI](./docs/learning/aspnet-core.md) — 啟動流程、請求管線、路由、繫結、驗證、DI、分層、設定來源
- [JWT 登入與授權](./docs/learning/auth.md) — token 結構、HS256、claim、認證與授權、MapInboundClaims、ClockSkew、密碼比對、簽發型別鏈
- [.NET 套件：找、裝、自己寫](./docs/learning/dotnet-packages.md) — 需求分類、三層階梯（BCL／共享框架／獨立套件）、查法、自製界線 — 11-14
- [統一錯誤處理](./docs/learning/error-handling.md) — 四個錯誤出口、收斂形狀、選項註冊順序坑、與 Node 版對照 — 11-15
- [模型檔託管](./docs/learning/static-files.md) — 白名單路由、ContentRootPath、檔案複製與回應標頭 — 11-16
- [.NET 專案設定：csproj 與建置](./docs/learning/dotnet-project.md) — package.json 對照、檔案分類、複製規則、增量建置 — 11-16

## 本檔主題

- 專案骨架：`dotnet new webapi` 產生哪些檔案 — 11-11
- JSON 讀取與時間輸出（`JsonDocument`、`DateTimeOffset` 與格式字串） — 11-13
- 認證與授權（JWT、HS256、claim、MapInboundClaims） — 11-14
- 啟動設定與 CORS（PORT 讀取、啟動期檢查、政策白名單制） — 11-18
- OpenAPI 文件產生（document／operation 轉換器分工、規格快照） — 11-19

## 本檔小主題

### 專案骨架

樣板產生六類檔案。`Web3D.Backend.csproj`（XML 格式的專案檔）列出目標框架（`net10.0`，即 .NET 10）與 NuGet 套件（.NET 的套件來源，相當於 npm registry），地位等同 Node 版的 `package.json`；`dotnet add package` 相當於 `npm install <套件>`，`dotnet restore` 相當於 `npm install`（下載全部套件）。隨著功能增加套件已擴充到 6 個（清單以 `Web3D.Backend.csproj` 的 `PackageReference` 為準）：`Microsoft.AspNetCore.Authentication.JwtBearer`（驗證請求帶來的 token；「驗證你是誰」）、`Microsoft.AspNetCore.OpenApi`（開發環境匯出 API 規格文件，11-19 已使用）、`Microsoft.EntityFrameworkCore` 與 `Npgsql.EntityFrameworkCore.PostgreSQL`（資料存取）、`Npgsql`（程式碼直接用到連線字串型別，故自行宣告）、`System.IdentityModel.Tokens.Jwt`（簽發與驗證 token 共用）。

`Program.cs` 是啟動檔，對應 Node 版的 `src/index.ts`：`WebApplication.CreateBuilder(args)` 建立應用程式建構器，`builder.Services.AddControllers()` 把控制器註冊進依賴注入容器（框架內建的物件工廠，負責建立並提供各類別的實例），`builder.Build()` 建出應用本體，`app.MapControllers()` 掛上路由（相當於 `app.use('/assets', assetsRouter)` 那三行），`app.Run()` 啟動監聽（相當於 `app.listen(PORT)`）。

`appsettings.json` / `appsettings.Development.json` 是 JSON 設定檔，對應 Node 版的 `.env` 加讀取邏輯。`Properties/launchSettings.json` 只有用 `dotnet run` 或 IDE 按 F5 啟動時有效（決定埠與環境變數），部署後無效，可視為本機啟動參數。`Web3D.Backend.http` 是樣板附的手動測試檔，因指向專案沒有的 `/weatherforecast` 已刪除，待 Phase 1 有真端點後重建。`.gitignore` 由 `dotnet new gitignore` 產生，`bin/` 與 `obj/`（編譯產物，對應 Node 版的 `dist/`）已排除在版控外。

### JSON 讀取與時間輸出

`JsonDocument.Parse` 把 JSON 字串解析成一份唯讀文件，`RootElement` 是進入點，型別為 `JsonElement`（指向文件內部資料的檢視，本身不擁有資料）。文件被釋放（離開 `using` 範圍）之後，裡面的 `JsonElement` 就失效，之後存取會拋 `ObjectDisposedException`。`Clone()` 把該節點複製成獨立、不依賴文件的資料，所以「解析、取節點、Clone、釋放文件、回傳複本」是固定順序。若需要修改內容，則改用 `JsonNode`（可修改的 JSON 物件樹）。

`DateTimeOffset` 除了時間也帶時區位移（例如 +08:00），不同時區的同一瞬間可以正確比較；`DateTime` 只有時間，時區靠額外約定。資料庫的 timestamptz（含時區的時間戳）對應到 `DateTimeOffset`。輸出前先呼叫 `ToUniversalTime()`（把時間點換算成 UTC）：資料庫讀出的值可能帶本地位移，直接格式化會把本地時間標上代表 UTC 的 `Z`，等於回報錯誤的時間點。

格式化使用 `CultureInfo`（地區設定：記錄語言、數字寫法、日期寫法與採用的曆法）。`.ToString()` 沒指定時使用執行緒當下的文化，而伺服器的文化設定不在程式控制範圍內。泰國文化預設採佛曆（年分加 543 年）、沙烏地阿拉伯採伊斯蘭曆；`InvariantCulture`（不屬於任何國家的中性文化，固定西曆與西文數字）輸出因此可預測。這是 .NET 才有的概念：JavaScript 的 `Date.toISOString()` 一律輸出 UTC 與西曆，沒有等價選項。

### 認證與授權

登入流程：`POST /auth/login` 用 `FixedTimeEquals` 比對管理員密碼，通過就簽一張 HS256 的 JWT（payload 只有 `role: admin`，15 分鐘有效）；之後的寫入請求帶 `Authorization: Bearer <token>`，由 `UseAuthentication` 驗簽、`UseAuthorization` 執行 `[Authorize(Roles = "admin")]`。`MapInboundClaims` 必須關掉，否則框架會把 `role` 改名，角色就比不到了；`ClockSkew` 設 0，與 Node 版的過期判斷一致。各概念的完整說明見主題檔 [JWT 登入與授權](./docs/learning/auth.md)。

## 2026-11-19 — OpenAPI 文件產生與規格快照

- 問：為什麼要拆成 document 與 operation 兩個 OpenAPI 轉換器，而不是在每個 Action 上加屬性？

  答：ASP.NET Core 產生 OpenAPI 文件時提供兩種掛勾（由框架在特定時機呼叫的擴充點）：document 轉換器對整份文件執行一次，operation 轉換器對每個「方法＋路徑」各執行一次。JWT 相關資訊有兩種性質：驗證方式本身（`components.securitySchemes` 裡的 `Bearer` 定義）整份文件只需要一份；「這個端點要不要帶 token」則是逐端點的資訊。因此 scheme 寫在 document 轉換器、requirement 寫在 operation 轉換器，各自只處理自己那一層；之後新增端點只要掛 `[Authorize]`，文件就會自動跟上，不必回頭改文件程式碼。若改成在每個 Action 貼文件屬性，同一份資訊等於寫兩次（`[Authorize]` 一次、文件屬性一次），兩邊遲早不一致。與 Node 版對照：Node 沒有這種掛勾，只能手寫 yaml 或在每個 route 上加 JSDoc 註解；差別在 ASP.NET Core 讀的是執行期的 metadata（隨型別與屬性編譯進組件的描述資料），Node 版讀的是註解文字，但兩者都是「讓驗證需求跟著路由定義走」。

- 問：`/openapi/v1.json` 執行起來就有，為什麼還要把文件匯出成快照放進版控？

  答：端點提供的內容是當下算出來的：只在 Development 環境掛載，內容由程式與 metadata 即時產生，服務沒開就拿不到。匯出成 `docs/openapi.json` 後有兩件事變得可能：版本控制看得出端點或結構的增減（diff 本身就是契約變更紀錄），外部讀者也不必啟動服務就能讀（在 GitHub 上可直接點開）。代價是快照會過期——改端點後要手動重新匯出，而且沒有任何機制提醒；這點已列在 CHANGELOG 的已知限制。

## 2026-11-18 — 啟動設定（PORT／CORS_ORIGIN）與 API 存取日誌

- 問：ASP.NET Core 的 CORS 政策只寫了來源（`WithOrigins`／`AllowAnyOrigin`），為什麼前端的請求會被 preflight 擋下，而 Node 版的 `cors({origin})` 卻不用多寫什麼？

  答：ASP.NET Core 的政策是白名單制（預設什麼都不放行）：方法清單（`Methods`）與標頭清單（`Headers`）預設是空的，而回應的 `Access-Control-Allow-Methods`／`Allow-Headers` 正是從這兩個清單產生，空清單就不送這兩個標頭，瀏覽器判定 preflight 失敗。`AllowAnyMethod`／`AllowAnyHeader` 的做法只是把 `*` 塞進清單（`AllowAnyMethod` 屬性的成立條件就是「清單只有一個 `*`」）。Node 的 `cors` 套件預設方法全開、且未設定 `allowedHeaders` 時反射請求帶來的標頭，所以兩邊的等效設定長得不一樣。前端（`client.ts`）每次請求都帶 `Content-Type`（JSON 內容類型，非簡單標頭）、寫入再帶 `Authorization`，兩者都會觸發 preflight（瀏覽器在真正請求前先送 OPTIONS 試探）。本次以實際請求確認：補上兩行後 preflight 回 `Allow-Methods: PUT`、`Allow-Headers: content-type,authorization`——反射的是請求值，不是 `*`。

- 問：`AddCors` 政策委派裡的 `throw`，是在服務啟動時就失敗，還是等第一次請求才失敗？

  答：啟動時，三環證據：`CorsOptions.AddPolicy` 收到委派就立即執行並建出政策；`CorsService`（CORS 服務本體）的建構子讀取設定值；管線的 `Build` 在 `Server.StartAsync`（開始監聽）之前。條件反轉的 throw（`origins.Length > 0` 誤寫，正常設定也會拋）因此在 `Now listening on` 印出前就失敗。本次另一個證據：`--no-launch-profile` 讓環境變成 Production、缺連線字串，同樣在啟動期拋出（`Program.cs:126`）而非等請求。驗證留在政策委派內只是整理，提前搬出只是換錯誤訊息。

- 問：`UseHttpsRedirection` 找不到 https 埠時，是每個請求都印一次警告嗎？

  答：不是，只印一次。埠號存在 `Lazy<int>`（延遲求值：第一次用到時才計算、之後記住結果）裡，`TryGetHttpsPort` 只求值一次。本次實測（只綁 http）也只看到一行 `Failed to determine the https port for redirect`。目前 http-only 綁定下這行中介層每次都直接放行，不影響任何請求；`UseCors` 排在它之前屬於防禦性排序，之後加上 https 綁定才有意義。

- 問：只搜 Npgsql provider 套件檔找不到 `CommandTimeout`，能斷定這個 API 不存在嗎？

  答：不能。`npgsql => npgsql.CommandTimeout(5)` 來自 EF Core 關聯式基底類別 `RelationalDbContextOptionsBuilder`，`NpgsqlDbContextOptionsBuilder` 只是繼承它；子套件的文件與 DLL 自然不會收錄父類別成員，「查不到」被誤判成「不存在」。準則是編譯器：`dotnet build -t:Rebuild`（全量重建，排除增量跳過）0 警告 0 錯誤即存在。本次教訓：宣告位置要從型別繼承鏈找，不要只搜用到的套件。

- 問：`updated_at` 用應用程式時鐘、PUT 先查再存，為何不與 Node 版（DB 時鐘、單句 UPDATE）統一？

  答：要拿 DB 時鐘只有兩條路：手寫 raw SQL（在 EF 實體更新流程裡開洞）或 DB trigger（schema 變更，要 migration）。觀測條件只有「應用程式與 DB 時鐘不同步」時才成立（兩邊都對時，偏差毫秒級）。先查再存的競態（中間被刪會 500 而非 404）只在併發刪除成立，單管理員寫入不會發生。兩者代價都大於收益，維持現狀、記 CHANGELOG 已知限制即可。

- 問：PUT 的多餘鍵為何從保留改為丟掉？

  答：PUT 是整筆取代，不是部分更新，DB 內容應該等於契約。Node 版驗證器重建 `{camera, cube, model}`，多餘鍵本來就不寫入；C# 版若保留，等於存進無法驗證的欄位。用 `NormalizeConfig` 在補 `model` 之外只留契約三鍵（`DeepClone` 避開同一節點不能有兩個父節點的限制），與 Node 寫入結果一致。

- 問：`ExecuteSqlRawAsync` 明明存在，為什麼會報 `CS1061: 'DatabaseFacade' does not contain a definition`？

  答：它是擴充方法（extension method：掛在別的型別上的靜態方法，寫起來像成員），本體在 `Microsoft.EntityFrameworkCore` 命名空間的 `RelationalDatabaseFacadeExtensions`；沒有 `using Microsoft.EntityFrameworkCore;` 就找不到。而之前用的 `CanConnectAsync` 是 `DatabaseFacade` 的實例方法，本來就不必那行 using——同一檔案先前編得過，反而讓人誤以為「已經有 EF 的 using 了」。斷定流程：先用套件的 XML 文件（`Microsoft.EntityFrameworkCore.Relational.xml` 有 `ExecuteSqlRawAsync` 簽名）確認存在，再用錯誤碼定位（`CS1061` 是缺 using 或缺組件）；`dotnet list package --include-transitive` 確認 `Relational` 在圖裡，排除組件問題，只剩缺 using。本次教訓與同日 `CommandTimeout` 同類：查不到 ≠ 不存在——擴充方法不在型別本身，IntelliSense 沒提示很容易誤判。

- 問：連線字串裡寫 `Maximum Pool Size`，程式碼裡卻是 `MaxPoolSize`，到底哪個名字才對？

  答：兩個都對，只是用在不同地方。`Maximum Pool Size` 是連線字串裡的文字關鍵字；`NpgsqlConnectionStringBuilder`（程式裡用來組連線字串的類別）上的屬性（property：類別對外暴露的可讀寫欄位）叫 `MaxPoolSize`。寫成 `MaximumPoolSize` 會在編譯期報 `CS0117`（型別裡沒有這個成員）——錯誤碼直接告訴你是名字錯了，不是功能不存在。查證位置是 NuGet 快取裡的 `Npgsql.xml` 文件。與同日另外兩次教訓同類：名稱要核對實際用法，不要憑字面拼。

- 問：Node 版的連線逾時到底是幾秒？

  答：15 秒。`web3D_proto_backend/src/db.ts` 寫的是 `connectionTimeoutMillis: 15_000`（毫秒；底線只是千分位分隔，方便讀數）。本次一度憑記憶寫成「與 Node 對齊（連線 5 秒）」，直到讀原始碼才發現是事實錯誤。教訓：要對齊參數，先打開對照組的原始碼確認，不要憑記憶或舊文件。代價見 CHANGELOG 設計決策：資料庫完全不可達時，約 15 秒才回 500。


## 2026-11-16 — 模型端點 GET /models/{modelKey} 與設定驗證拆分

新增 `Controllers/ModelController.cs`（modelKey 白名單查表 → glb 回傳）、`ModelFiles/`（模型檔與授權標註）與 csproj 複製規則（見主題檔 [.NET 專案設定](./docs/learning/dotnet-project.md) §複製規則）；`Models/UpdateAssetDto.cs` 把驗證從 `JsonObjectOnly` 換成逐層檢查的 `JsonObjectConfig`，缺 `model` 由 `ApplyDefault()` 補預設。機制說明見新主題檔[模型檔託管](./docs/learning/static-files.md)；驗證順序的完整說明見主題檔 [ASP.NET Core](./docs/learning/aspnet-core.md) §模型驗證。以下兩則是當天實際踩到的坑。

- 問：`IsValidCube` 裡 `TryGetProperty` 的 `!` 漏掉，為什麼所有 PUT 都回 400，而不是只有 cube 有錯的才回？

  答：驗證屬性是全有全無（三支 `IsValid*` 以 `&&` 串接，任一為 false 整個本文判不合法），而少了 `!` 的那個條件「color 存在或不存在都回 false」——color 存在時左邊為 true，color 不存在時 `color` 是 `default(JsonElement)`（型別的預設值：`JsonElement` 是實值型別，預設狀態的種類標記 `ValueKind` 為 Undefined，即「還沒指向任何 JSON」）不等於 String 而右邊為 true。兩條路都通往 false，所以沒有任何本文能通過，症狀是「全部 PUT 400」，與欄位內容無關。本次經由逐條 `||` 短路（短路：`||` 左邊為 true 就不再看右邊，`&&` 左邊為 false 就不再看右邊）分析找到，修法為補上 `!`（先確認取得到屬性，再確認型別、再取值，取值方法如 `GetString()` 在型別不對時會拋例外，驗證失敗應回 400 而非讓例外變成 500）。

- 問：`Cache-Control` 寫成 `public; max-age=3600`，為什麼瀏覽器完全不快取？

  答：`Cache-Control` 是逗號分隔的指令清單，分號不是分隔符。`public; max-age=3600` 會被視為一個無法辨識的單一指令而整串忽略，等於沒有設快取。HTTP 標頭的分隔符要看規格：逗號列舉與分號參數意義不同（`Content-Type` 用分號接參數，例如 `text/html; charset=utf-8` 的分號後面是該文件的參數），不能跨標頭類推。本次經 review 發現，改為 `public, max-age=3600`；快取的完整機制見主題檔[模型檔託管](./docs/learning/static-files.md)。

## 2026-11-15 — 錯誤格式與 Node 版對齊

錯誤契約收斂為 `{"error":{"code","message"}}`：新增 `Models/ApiErrorResponse.cs`、`Constants/ApiErrors.cs`、`Middleware/ExceptionMiddleware.cs`，並為 `MapFallback` 補上 `"{*path}"`。完整說明見主題檔[統一錯誤處理](./docs/learning/error-handling.md)；程式側的變更理由見 [CHANGELOG.md](./CHANGELOG.md) 的 2026-11-15 段。

- 問：為什麼 `Configure<ApiBehaviorOptions>` 寫在 `AddControllers()` 前面，400 就回不到統一形狀？

  答：`builder.Services` 收集的是延後執行的設定動作（框架於 `Build()` 之後依註冊順序逐一疊加，後登記者覆寫先登記者）。`AddControllers()` 內部亦註冊一組框架預設值，故先前寫入的自訂值會被覆寫。此問題不易察覺：編譯與啟動皆正常，僅於實際送出不合法的 JSON 時方能察覺回應形狀有誤。本次經由實際請求發現，已將該設定移至 `AddControllers()` 之後，並加註解說明順序限制。選項物件（Options 物件：框架用以收納某區塊設定的具名容器，本例為管理 `[ApiController]` 預設行為的 `ApiBehaviorOptions`）之註冊順序限制，後續新增其他 `Configure<...>` 時同樣適用。

- 問：`MapFallback("{*path}")` 的 `"{*path}"` 是什麼？不寫會怎樣？

  答：`{*path}` 為 catch-all 路由參數（`*` 表示涵蓋整段剩餘路徑，含斜線）。預設之 `MapFallback` 會排除形如檔名的路徑（此類路徑屬靜態檔案中介層之處理範圍）；若未指定 `{*path}`，`/nope.json` 之類的請求不會進入 fallback，回應為空本文而非統一的 404。指定 `"{*path}"` 後，未匹配之路由一律收斂為同一份 `ROUTE_NOT_FOUND` JSON。本次經由 `/nope` 與 `/nope.json` 兩個實際請求確認此差異。

- 問：`OnChallenge` 裡的 `HandleResponse()` 是做什麼的？不寫會怎樣？

  答：未攜帶 token 或 token 無效時，JwtBearer 之預設行為為直接回應空本文 401（Challenge：認證失敗時框架發出之要求出示身分回應）。`context.HandleResponse()` 用以告知框架「回應由呼叫端自行處理，框架不再介入」，後續程式碼方可寫入統一形狀的 `ApiErrors.Unauthorized` JSON。`OnForbidden`（角色不符）亦同，本專案回應 `ApiErrors.Forbidden`。Node 版對照：`src/index.ts` 自行撰寫之 unauthorized 中介層，兩者用途相同。

- 問：`ApiErrors` 的每個錯誤屬性為什麼寫成 `=> Build(...)`？用 `static readonly` 存一份共用的不行嗎？

  答：`=>` 為運算式主體屬性（讀取該屬性時執行右側運算式並回傳結果，相當於唯讀屬性的簡寫）：每次讀取 `ApiErrors.InvalidAssetId`，`Build()` 就執行一次。

  誠實地說，`Build()` 在本專案的必要性低：目前沒有任何程式碼會修改回傳的物件；若改寫為 `static readonly` 存放單一實例，執行結果相同。

  仍保留 `Build()` 的理由有二。其一為防禦性：將來若新增修改 `Message` 的程式碼（例如附加除錯資訊），共用實例會使其他處理中之請求亦讀到修改後的值（ASP.NET Core 同時處理多個請求，並行：同一時間有多個請求在執行）；每次 new 則無此風險。其二為與 Node 版結構對應：Node 版以共用之 `errorDefinition` 查表、每次 `throw new AppError` 建新物件；C# 這邊沒有對應的例外類別，直接在讀取點建新物件，兩邊「定義集中一處、物件每次建新」的結構相同。

  結論為：兩種寫法在本專案結果等價，本次取風險較低者；背後適用的規則是「可變物件不共用」，並非「必須每次 new」。

- 問：修改 `ApiErrors.cs` 的錯誤訊息後，為什麼重啟服務沒有用，要執行 `dotnet build`（或 `dotnet run`）才生效？

  答：C# 為編譯語言，實際執行的是編譯產物（`bin/` 目錄下之組件），而非 `.cs` 原始檔。修改原始檔僅變更輸入，須經編譯將變更寫入產物，執行中的程式方能採用。`dotnet build` 僅編譯不執行；`dotnet run` 先編譯再執行。Node 版無此步驟：修改 TypeScript 原始檔後重啟服務即生效，不經獨立編譯階段。本次實務意義：僅修改錯誤訊息等靜態內容而未重新編譯時，回應仍為舊內容，易被誤判為回傳邏輯有誤；除錯時先確認是否已重新編譯。

- 問：換語言重寫後端時，資料規格（錯誤形狀）通常都會跟原本不一樣嗎？

  答：分三層看。傳輸層（HTTP 方法、狀態碼、標頭、JSON 編碼）為網路標準，與語言無關，兩邊相同。成功回應的形狀由產品需求決定（資產須有 id、name、config），兩邊長一樣是因為同一份需求，並非框架規定。

  真正不同的是錯誤信封：Express 無內建錯誤形狀（送什麼就是什麼）；ASP.NET Core 內建 ProblemDetails（微軟定義之錯誤格式，欄位為 type、title、status、detail、traceId；網路標準 RFC 9457 定義之通用錯誤信封），不覆寫則 400 自動長那樣。ProblemDetails 為統一此層之嘗試，但僅屬建議而非強制；舊專案在其普及前已定好自身形狀，改動等於改契約。

  實務結論為對契約、不對巧合：契約由產品定義（前端依其解析，改動即損壞前端），框架預設形狀僅為起點，必要時覆寫。本次驗證方式亦據此制訂：同一請求兩邊回同一內容，而非檢查 C# 是否依微軟文件撰寫。

## 2026-11-14 — 登入、JWT 與寫入權限

新增 POST /auth/login（簽發 15 分鐘有效的 JWT）、PUT /assets/{id} 加管理員驗證、GET /health 與 /health/db。完整概念說明見主題檔 [JWT 登入與授權](./docs/learning/auth.md)。

- 問：登入簽章為什麼用 `HmacSha256`？`Sha256` 看起來也是 SHA256，不行嗎？

  答：不行。`Sha256` 是雜湊演算法常數，`HmacSha256`（值 HS256）才是簽章演算法常數。簽章用錯常數，簽 token 時框架找不到對應實作就丟例外，`POST /auth/login` 直接回 500——本次就是這樣修好的。Node 版 `constants/jwt.ts` 的 `HS256` 是同一個東西。

- 問：`RoleClaimType` 已經設成 `role`，token 裡也有 `role: admin`，為什麼 `[Authorize(Roles = "admin")]` 還是 403？

  答：框架預設的 inbound claim mapping 會把 `role` 改寫成 `ClaimTypes.Role` 的長 URI，改寫後就沒有名叫 `role` 的 claim 可比。解法是 `options.MapInboundClaims = false`，讓 claim 保持簽發時的原名；這樣也跟 Node 版的 payload 一字相同。完整說明見主題檔「MapInboundClaims 與 RoleClaimType」段。

- 問：`JwtSecurityToken`、`SymmetricSecurityKey` 那些型別是哪來的，怎麼知道要裝哪個套件？

  答：型別的逐行對照在主題檔 [JWT 登入與授權](./docs/learning/auth.md)「`CreateToken` 逐行對照」段（例如 `SymmetricSecurityKey` 是金鑰物件、`WriteToken` 才產出真正回給前端的字串）；「怎麼知道要裝哪個套件」的完整流程在新主題檔 [.NET 套件：找、裝、自己寫](./docs/learning/dotnet-packages.md)。一句話版本：`[Authorize]` 本體在共享框架內，不用裝；`AddJwtBearer` 的驗證處理器被移出框架，一定要裝獨立套件。

## 2026-11-13 — 實作單筆讀取與更新

**問答**
- 問：`JsonElement` 是什麼？為什麼 `AssetDetailDto.Config` 用它，而 `Asset.Config`（Entity）用 `string`？

  答：`JsonElement` 是 JSON 值在記憶體裡的檢視（指向文件內部資料，本身不擁有資料；文件釋放後再用會拋 `ObjectDisposedException`）。DTO 用它是因為前端要收到真正的 JSON 物件（本檔小主題「JSON 讀取」段有完整說明）。Entity 用 `string` 是因為 config 內容隨資產變化，先整包存取；以後要強型別再換 `JsonDocument`，到時只改這個屬性型別。

- 問：`JsonObjectOnlyAttribute` 是什麼？跟 `[Required]` 有什麼關係？

  答：它是自訂的驗證屬性，繼承 `ValidationAttribute` 並覆寫 `IsValid`（檢查 `JsonElement.ValueKind` 是不是物件），寫法照抄 `aspnet-core.md`「模型驗證」段說的固定模式。`[Required]` 只保證 Name 有值，保證不了「config 內部是物件」，所以要再寫一個；兩個都通過才會進入 Action，這是框架層的保證。


## 2026-11-12 — 骨架與唯讀 API

建出可跑的骨架與第一支唯讀端點 GET /assets。概念說明已搬到主題檔：分層、DI、啟動流程見 aspnet-core，查詢翻譯與型別對照見 ef-core。

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


## 2026-11-11 — 專案初始化

專案初始化：dotnet new webapi 骨架。C# 語法對照見 csharp-basics，設定來源與覆寫順序見 aspnet-core；專案骨架段留在本檔（一次性、不會再長）。

**問答**
- 問：`dotnet new webapi` 產生了哪些檔案，各自對應 Node.js 版的什麼東西？

  答：樣板產生六類檔案。`Web3D.Backend.csproj`（XML 格式的專案檔）列出目標框架（`net10.0`，即 .NET 10）與 NuGet 套件（.NET 的套件來源，相當於 npm registry），地位等同 Node 版的 `package.json`；`dotnet add package` 相當於 `npm install <套件>`，`dotnet restore` 相當於 `npm install`（下載全部套件）。隨著功能增加套件已擴充到 6 個（清單以 `Web3D.Backend.csproj` 的 `PackageReference` 為準）：`Microsoft.AspNetCore.Authentication.JwtBearer`（驗證請求帶來的 token；「驗證你是誰」）、`Microsoft.AspNetCore.OpenApi`（開發環境匯出 API 規格文件，11-19 已使用）、`Microsoft.EntityFrameworkCore` 與 `Npgsql.EntityFrameworkCore.PostgreSQL`（資料存取）、`Npgsql`（程式碼直接用到連線字串型別，故自行宣告）、`System.IdentityModel.Tokens.Jwt`（簽發與驗證 token 共用）。

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

