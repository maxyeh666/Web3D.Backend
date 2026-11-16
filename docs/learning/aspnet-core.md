# ASP.NET Core：啟動、請求管線、分層與 DI

記「請求從進來到出去」的框架概念。實作當天的問答仍留 `LEARNING.md` 的日期段，這裡只收可反覆查閱的說明。

先看全貌：一次請求依序經過哪些地方，見 [架構總覽](./architecture.md) 第 2 節。本檔按執行順序排列：程式怎麼起來 → 請求進來走哪幾關 → 那些物件誰提供 → 為什麼分成這些角色 → 跑不起來查哪裡。

## 啟動流程、middleware 順序與頂層陳述式

`Program.cs` 沒有 Main 方法：.NET 6 之後支援頂層陳述式（top-level statements：不用寫 class Main，直接寫流程式碼、編譯器自動包成進入點），`Program.cs` 從上到下就是啟動流程：讀設定、註冊服務、建出 app、掛 middleware、`Run` 啟動。`WebApplication.CreateBuilder(args)` 建立應用程式建構器，`builder.Services.AddControllers()` 把控制器註冊進依賴注入容器，`builder.Build()` 建出應用本體，`app.MapControllers()` 掛上路由，`app.Run()` 啟動監聽。

middleware（請求進來後會依序經過的一串處理步驟）順序有意義：`UseMiddleware<ExceptionMiddleware>` 掛最外層，才接得到下游任一站拋出的例外（見[統一錯誤處理](./error-handling.md)）；`UseHttpsRedirection` 先處理 http 轉 https（本機沒配 https 埠時直接放行）；`UseAuthentication` 驗 token、`UseAuthorization` 執行 `[Authorize]`，兩者都必須在掛路由之前，否則請求還沒檢查就進 Controller；掛路由相當於 Node 版掛路由器的那幾行。

## 請求管線：routing、model binding、model validation

ASP.NET Core 從收到請求到執行 Controller 方法，中間分成四個階段，順序固定。routing（路由：依網址決定由哪個方法處理，並把網址上的片段取出來）→ model binding（模型繫結：把取出的值與請求內容轉成方法參數的強型別值）→ model validation（模型驗證：執行參數上的驗證屬性）→ Action（動作方法：框架依路由挑中後實際執行的 Controller 方法）執行，最後才把回傳值序列化成 JSON。

這四層都包在 middleware 管線之內，所以例外處理 middleware 放在最外層就能接住任一階段拋出的例外。狀態碼的來源也因此可以定位：路由約束不符是 404（第一階段就沒有匹配的路由）、驗證失敗是 400（第三階段）、查無資料是 404（Action 自己判斷後回 NotFound）。

Express 只有第一階段與最後階段，中間的取值與驗證都寫在 handler 裡，因此沒有這兩個名詞。

## 屬性路由與路由約束

Controller 版的路由叫屬性路由（attribute routing）：類別上的 `[Route("assets")]` 與方法上的 `[HttpGet("{id}")]` 組合成樣板 `/assets/{id}`，大括號那段是路由參數，請求進來時其值會被放進 route values（路由值：框架依路由樣板從網址上擷取出的鍵值對）。

路由約束（route constraint）是寫在參數後面的條件，例如 `{id:long}` 要求該片段能轉成 64 位元整數；不符合就代表「這條路由不匹配」，若沒有其他路由能匹配，結果就是 404。這是設計上的結果而非錯誤：框架認為呼叫端請求了一個不存在的網址，而不是請求了一個參數不合法的方法。

要讓「參數不合法」能回 400，就必須讓路由無條件匹配（`{id}`）、把判斷留給程式碼。多條路由同時可用時，ASP.NET Core 會依樣板的具體程度評分（固定文字段優先於參數段）決定，不依宣告順序；Express 是依註冊順序線性比對，先符合者先贏。

## 模型繫結

模型繫結是把 HTTP 傳來的文字轉成方法參數的過程，決定「值從哪來」與「怎麼轉型」兩件事。在 `[ApiController]` 下，來源預設依型別推斷：簡單型別（`string`、`long`、`bool`、`DateTime` 等）先找 route values、再找查詢字串；複雜型別（例如 `UpdateAssetDto`）預設從請求本文的 JSON 反序列化；`IFormFile`（檔案上傳的型別）從表單。

要打破推斷就明寫來源標記：`[FromRoute]`、`[FromQuery]`、`[FromBody]`、`[FromHeader]`、`[FromForm]`。反序列化使用 System.Text.Json：DTO 的屬性必須是可寫的（有 setter 或由建構子給值）、JSON 裡多出來的欄位預設被忽略、缺少的欄位則保持型別預設值且不報錯——這也是為什麼「沒帶 name」不會自動失敗，必須靠驗證屬性。轉型失敗不會拋例外，而是把錯誤記進 ModelState，交給下一個階段處理。

請求本文本質是只能往前的資料流，所以 body 只能讀一次，讀第二次會得到空內容。

## 模型驗證

模型驗證在模型繫結完成後、Action 執行前進行，檢查兩件事：繫結過程的轉型錯誤，以及參數物件上所有驗證屬性（attribute）。驗證屬性繼承 `ValidationAttribute`，`IsValid` 收到的是「該屬性自己的值」，不是整個物件，所以像 `config` 這種「內部還要逐欄檢查」的規則必須寫成自訂屬性，在 `IsValid` 裡自行拆解 JSON。

驗證結果集中在 ModelState（以屬性名稱為 key 的錯誤清單）。`[ApiController]` 的行為是：ModelState 有錯就自動回 400、不進入 Action，回應本文預設為 ProblemDetails（微軟定義的錯誤格式，欄位為 title、status、errors）。因為驗證發生在 Action 之前，Action 沒被執行就不可能呼叫 Service 寫入資料庫，這是框架層的保證，不需要在 Action 內再檢查一次。

驗證屬性只能讀、不能改請求內容：`IsValid` 回的是布林值，框架沒有「把驗證後的物件寫回去」的通道。所以「缺欄位補預設」這類寫入邏輯必須放在 Action 內（例如 `UpdateAssetDto.ApplyDefault` 在 `AssetController.UpdateAsset` 開頭呼叫），寫進屬性裡也沒用，回傳值丟不出去。順序因此固定：驗證在前、補值在後，補值後的物件不再被驗一次——補值只能補「驗證已放行」的形狀，不能拿來繞過驗證。

本專案已覆寫 `ApiBehaviorOptions.InvalidModelStateResponseFactory`，驗證失敗改回契約形狀的 `INVALID_REQUEST_BODY`（不是預設的 ProblemDetails）。注意註冊順序：`Configure<ApiBehaviorOptions>` 必須寫在 `AddControllers()` 之後，否則將被框架內建的預設值覆寫——本次曾因此設定失效（詳見 LEARNING.md 2026-11-15 段第一則）。四個錯誤出口的完整說明見[統一錯誤處理](./error-handling.md)。

## 依賴注入與物件生命週期

依賴注入（DI：物件不自己建立它需要的東西，而是在被建立時由框架傳進來）是把誰建立物件交給框架（`Program.cs` 的 `builder.Services`）。Controller 的建構子宣告需要 `AssetService`，框架收到請求時自動建立並傳入；Controller 不必知道 `AssetService` 怎麼建（它自己還需要 `AppDbContext`），要換實作或測試時只要換註冊、不改呼叫端。對照 Node 版：用 `import` 直接引用模組單例，沒有框架按參數型別自動組裝這一層。

為什麼需要 DI：Controller 要 `AssetService`、`AssetService` 要 `AppDbContext`；若各自 `new`，連線字串讀取、物件生命週期、釋放時機要在每個類別重寫一次，換實作還要改所有呼叫端。集中給框架依建構子型別建立後，這些事情只寫在 `Program.cs` 一處，也才能在請求結束時統一釋放。

註冊的壽命分三種：Transient 每次要都建新的；Scoped 同一個 HTTP 請求內共用一份、請求結束釋放；Singleton 整個程式只建一份、用到關機。`AddDbContext` 預設就是 Scoped，`AssetService` 也註冊為 Scoped：每個請求一份 DbContext，用完即丟（連線歸還到 Npgsql 的連線池），不會跨請求污染狀態。

對照 Node 版沒有這種區分：模組載入一次就整個 process 共用；資料庫連線靠連線池手動借還（相當於本專案 Scoped 背後做的事）。

代價與限制：依賴關係只寫在建構子，要讀建構子才知道這個類別用了什麼；註冊漏了不會編譯失敗，要到啟動或首次請求才發現。另一個限制是壽命方向：Scoped 的 `AppDbContext` 不能注入 Singleton——否則等於把只屬於單一請求的連線帶到整個程式的壽命。

## 後端分層與請求流程

一個 `GET /assets` 請求會依序經過 Controller（`Controllers/AssetController.cs`，只負責接請求、轉交、包回傳）、Service（`Services/AssetService.cs`，寫查詢邏輯）、DbContext（`Data/AppDbContext.cs`，EF Core 的進入點，`DbSet` 對應 assets 資料表），最後回到前端。對照 Node 版：`routers/assets.ts` 到 `services/assetService.ts` 到 `db.ts`，只是 C# 每一層都有具名型別，編譯時期就會檢查形狀對不對。

Entity（實體：資料表的一行對應成的類別，`Models/Asset.cs`）負責「資料庫長什麼樣」，跟資料表的關係是一對一，屬性（Property：長得像欄位、實際帶讀寫方法的成員）對欄位，對應規則寫在 `AppDbContext.OnModelCreating`。

DTO（Data Transfer Object，資料傳輸物件：只給前端看的形狀，`Models/AssetSummaryDto.cs`）負責「呼叫端需要什麼形狀」；兩邊需求不同（Entity 有全部欄位，前端列表只需要 id 和 name），直接回傳 Entity 會把不需要的欄位送出去，日後資料表加欄位還會直接牽動前端，所以用 DTO 隔開。對照 Node 版：在 service 裡手寫挑欄位的回傳物件，這裡把挑什麼變成一個具名型別。

Entity 有型別，欄位名打錯編譯就失敗；Node 寫 SQL 拿回的是 pg 驅動組出的普通物件，沒有型別約束，打錯欄位名要到執行才會發現。

## 設定來源、覆寫順序與本機執行

來源按覆寫順序（後者贏）是：`appsettings.json` → `appsettings.{環境}.json`（例如 Development 用 `appsettings.Development.json`，只放與正式不同的值）→ user-secrets（開發機私密值）→ 環境變數 → 命令列參數，由框架在啟動時自動疊加，程式只讀最終結果。

`dotnet user-secrets set "Jwt:Key" "..."` 把私密值存在本機個人資料夾的 JSON 檔，只在 Development 環境載入、不進版控；它取代的是 `.env` 的位置，但粒度不同——`.env` 是整個檔案一份，user-secrets 是按 key 一筆一筆存。部署時改由平台環境變數提供，與 Node 版相同（`DATABASE_URL` 等名稱不變），所以正式環境不需要任何本機檔案。

`dotnet user-secrets` 的 key 名稱由程式碼決定，不是隨便取：`GetConnectionString` 一律去找 `ConnectionStrings` 加上括號裡的名字。打錯（例如少一個 s）時設定系統找不到值、回傳空字串；空字串傳進 `UseNpgsql` 在啟動時不會檢查，直到第一次真的查詢才由 Npgsql 拋出 `The ConnectionString property has not been initialized`。

反推方法：訊息裡出現「沒有被初始化」代表 Npgsql 拿到的是空值，沿程式碼往回查設定來源（appsettings 到 user-secrets），用 `dotnet user-secrets list` 對照程式裡的字串，即可找到打錯的 key。

本機三步跑起來：先確保資料表存在（目前由 Node.js 版套用 000 到 004 的 migration，C# 版不執行）。再用 `dotnet user-secrets set` 寫入 `ConnectionStrings:DefaultConnection`（key 名稱由程式碼決定）。最後 `dotnet run`（`http://localhost:3000`），另開終端機打 `curl.exe http://localhost:3000/assets`，預期回一串資產陣列，即代表設定、連線、查詢、路由全通。

連不上時看訊息屬於哪一層：`The ConnectionString property has not been initialized` 是設定層（值是空的）；`42P01` 是資料表不存在。本文寫於專案初期，當時開發埠為 `http://localhost:5154`，之後已改為 `3000` 與前端預設一致。
