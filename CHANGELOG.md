# Changelog

依日期記錄後端的變更與設計決策。

## 2026-11-18 — 啟動設定（PORT／CORS_ORIGIN）、API 存取日誌與 `/health` 連線韌性

**背景**
- `ADMIN_PASSWORD` 空值在第一次登入才回 500（Phase 4 遺留）；`PORT` 與 `CORS_ORIGIN` 兩項環境變數在 C# 版沒有對應實作（README 標註待實作），Node 版（`src/index.ts`、`src/env.ts`）兩者皆有。
- Phase 1 的「CORS、API 存取日誌」未勾選；前端一旦以 `VITE_API_BASE_URL` 指向 C# 版，所有請求都會先送 preflight。

**變更**
- `Program.cs`：新增 `PORT` 讀取（格式錯誤或超出 1–65535 即啟動失敗）並以 `builder.WebHost.UseUrls` 綁 `http://0.0.0.0:{port}`；`ADMIN_PASSWORD` 未設定即啟動失敗（原為第一次登入 500）；新增 `AddCors` 的 `Frontend` 政策（`CORS_ORIGIN` 逗號分隔白名單、未設定不限來源、有設卻拆不出來源即啟動失敗，方法與標頭全開），`UseCors` 掛在 `UseHttpsRedirection` 之前；新增 `ApiRequestLoggingMiddleware` 並掛管線最外層。
- 新增 `Middleware/ApiRequestLoggingMiddleware.cs`：每個請求印一行 `[API] 方法 路徑 狀態碼 耗時ms`（`Stopwatch` 計時、`finally` 收尾、結構化參數）。
- `README.md`：Phase 1 的 CORS 與 API 存取日誌打勾；「C# 版與 Node.js 版的差異」補 `PORT` 覆寫說明；Development 段的環境變數說明更新為已實作狀態。
- 驗證：`PORT=3000` 啟動印出 `Now listening on: http://0.0.0.0:3000`；白名單來源 preflight 回 `204` 並帶 `Allow-Methods: PUT`、`Allow-Headers: content-type,authorization`；惡意來源同回 `204` 但無 CORS 標頭；`GET /health` 回 `200` 且日誌記 `[API] GET /health 200 ...ms`。測試程序已停，`3000` 埠無殘留——以上皆為改池設定與 `/health/db` 實作之前的量測，見下方新驗證。
- `Controllers/HealthController.cs`：`/health/db` 的實作改為 `ExecuteSqlAsync("SELECT 1")` 真查詢（原 `Database.CanConnectAsync`），並移除不再使用的 `ApiErrors` 引用；失敗讓例外往外拋，由 `ExceptionMiddleware` 記錄並回契約形狀的 `ApiErrors.InternalServerError`（退役 `CanConnectAsync` 的理由見下）。驗證：正常連線回 `200`，`durationMs: 520`（客戶端計時 541 毫秒）；假連線字串回 `500` 統一本文（`{"error":{"code":"INTERNAL_SERVER_ERROR","message":"Internal server error"}}`），客戶端計時 15426 毫秒（連線逾時 `Timeout=15` 生效），日誌同時印出 `System.TimeoutException: Timeout during connection attempt`（含 Npgsql 堆疊）與 `[API] GET /health/db 500 15408ms` 兩行；`GET /assets` 回 2 筆、20 個併發 `/health/db` 全部 200（耗時 191 毫秒）；三次皆 `dotnet build` 0 警告 0 錯誤。測試程序已停，`3001`／`3002`／`3003` 埠無殘留。
- `Program.cs`：連線逾時 `Timeout=15`（原誤植為 5，見下方設計決策更正）、連線池 `MaxPoolSize=5` 與 `ConnectionIdleLifetime=30`（皆覆寫 `NpgsqlConnectionStringBuilder` 的同名鍵）；`MaxPoolSize` 是 builder 的屬性名（連線字串關鍵字卻是 `Maximum Pool Size`，以 NuGet 快取的 `Npgsql.xml` 為準），打錯會在編譯期 `CS0117` 失敗。查詢逾時 `CommandTimeout(5)`（繼承自 EF Core `RelationalDbContextOptionsBuilder`，套用到每個 `DbCommand`，以全量重建 0 警告 0 錯誤確認存在）；`Npgsql 10.0.3` 改為直接引用（程式碼用到該型別，不靠 provider 的傳遞依賴）。
- `Models/UpdateAssetDto.cs`：`ApplyDefault` 改名並擴充為 `NormalizeConfig`（補缺的 `model`、只留契約三鍵，與 Node 版驗證器重建物件一致）；呼叫端 `AssetController.UpdateAsset` 同步改名。此為本日改動（前段完成實作，本段僅複核程式、補註解與同步文件）；端到端驗證仍待補，見已知限制。
- `docs/learning/` 同步更新：`aspnet-core.md`（`NormalizeConfig` 須在 Action 內呼叫，見該檔模型驗證段）、`auth.md`（`/health/db` 改走統一錯誤格式，註明兩版差異已收斂）、`error-handling.md`（`ExecuteSqlAsync` 退役 `CanConnectAsync` 的理由與 15.4 秒實測）。


**設計決策**
- 來源白名單、方法與標頭全開（`AllowAnyMethod`／`AllowAnyHeader`），而不逐一列舉：前端每次請求都帶 `Content-Type`、寫入再帶 `Authorization`，兩者都會觸發 preflight，列舉制日後加一個標頭就會再擋一次；來源已由白名單控管，全開方法與標頭邊際風險低。與 Node 版 `cors({origin})` 的預設（方法全開、反射請求標頭）對齊。
- 驗證留在 `AddCors` 的政策委派內而不提前：`CorsOptions.AddPolicy` 收到委派即執行、`CorsService` 建構子即讀 `Options.Value`，而管線 `Build` 在 `Server.StartAsync` 之前——條件反轉的 throw（原 `origins.Length > 0`，現 `== 0`）本來就會在開始監聽前失敗，提前只是換錯誤訊息，不是正確性修正。
- `UseCors` 在 `UseHttpsRedirection` 之前：preflight（OPTIONS）若先被 307 轉去 https，瀏覽器直接判定 CORS 失敗。現況只綁 http、即使順序反了也不會重導，但部署加上 https 綁定後會出事，屬於防禦性排序。
- 日誌掛最外層而非 `ExceptionMiddleware` 之內：例外被內層轉成 500 之後才回到外層，`finally` 讀到的才是實際回給前端的狀態碼。
- `PORT` 無預設值（Node 版預設 3000）：沿用框架的預設監聽（本機 `launchSettings.json` 給 3000），只在有設定時覆寫；讀不到就不動，避免程式與啟動設定檔打架。
- `UseHttpsRedirection` 保留：http-only 綁定下它只做一次判斷就放行（`Lazy<int>`，警告只印一次），不影響任何請求；之後加上 https 綁定才會真正重導。
- `updated_at` 維持應用程式時鐘（見 11-11 則已知限制）：要拿 DB 時鐘只能 raw SQL 或 DB trigger；前者在 ORM 流程裡開洞，後者是 schema 變更。單一實例下時鐘偏差毫秒級，不值得。
- 逾時值寫死在程式碼而不要求各環境連線字串自帶參數：本機與 Render 行為一致。連線逾時沒有對應的 EF 選項，只能走連線字串；查詢逾時走 EF 的 `CommandTimeout`——兩處缺一不可，註解已說明分工。
- 連線逾時更正為 15 秒：本次收尾才發現 Node 版 `db.ts` 的 `connectionTimeoutMillis` 是 `15_000` 而非 5 秒，此前文件寫的「與 Node 對齊（連線 5 秒）」是事實錯誤。Node 刻意放寬是為了 Neon 免費方案（閒置會暫停，首次喚醒常超過 5 秒）；若 C# 只給 5 秒，`/health/db` 會把「DB 醒得慢」判成「服務不健康」。兩者計時器不完全相同（Node 是取池排隊上限、Npgsql 是建立連線），但數值對齊已足夠覆蓋喚醒情境。代價：DB 完全不可達時約 15 秒才回 500（已實測 15426 毫秒）。`Program.cs` 註解同步改寫。
- 連線池 `MaxPoolSize=5`、`ConnectionIdleLifetime=30`：與 Node 版的 `max: 5`、`idleTimeoutMillis: 30_000` 對齊，與連線逾時一併寫進 `NpgsqlConnectionStringBuilder`（會覆寫連線字串的同名設定）。兩版接同一顆資料庫，連線數要一起算（不能超過 `max_connections`）。Npgsql 的閒置回收只在總連線數超過 `MinPoolSize`（預設 0）時觸發、且搭 `ConnectionPruningInterval`（預設 10 秒）清理，實際 30～40 秒才關掉——與 Node 的精確 30 秒只是近似，文件不可寫成「完全一致」。
- `/health/db` 改 `ExecuteSqlAsync("SELECT 1")` 而非 `Database.CanConnectAsync`：`CanConnectAsync` 把失敗轉成 `false`、不拋例外，`ExceptionMiddleware` 的 `LogError` 不會觸發——失敗原因不進日誌，只有存取日誌的一行 500（Node 版同樣失敗會由 `errorHandler` 印完整錯誤）。`ExecuteSqlAsync` 的參數是 `FormattableString`，`$"SELECT 1"` 可隱式轉換；回傳值（影響列數）忽略即可。`ExecuteSqlRawAsync` 同等可用，兩者皆為 `Microsoft.EntityFrameworkCore` 命名空間的擴充方法（沒有這行 using 就是 CS1061；`CanConnectAsync` 是 `DatabaseFacade` 的實例方法才免 using）。改完之後失敗走 `ExceptionMiddleware`——已驗證日誌確實出現 `System.TimeoutException` 含完整堆疊。
- 只用 `CommandTimeout`、不另加 `statement_timeout`：Node 的 `statement_timeout` 是伺服器端（資料庫自己中斷查詢），`CommandTimeout` 是客戶端（Npgsql 放棄等待並送取消）。兩層都設容易互鬥；且／`health/db` 的 `SELECT 1` 已直接行使 `CommandTimeout`，增加伺服器端逾時是多出的複雜度，不值得。語意差異記入 `Program.cs` 註解。
- `/health/db` 併入統一錯誤格式：先前「維運端點刻意不套格式」建立在錯誤前提上（Node 版走 `errorHandler` 同樣統一）；多一種形狀只會讓前端多一份解析分支，統一後四個出口同一形狀。

**學習筆記**
- 9 則，見 LEARNING.md 的 2026-11-18 段（CORS 白名單制陷阱、啟動期檢查的三環證據、`Lazy` 警告頻率、`CommandTimeout` 宣告位置、時鐘與寫入方式不統一的理由、多餘鍵丟掉的理由、CS1061 實例方法與擴充方法的區別、`MaxPoolSize` 屬性與 `Maximum Pool Size` 關鍵字的名稱陷阱、讀對照組原始碼再落文件的教訓）。

**已知限制 / 後續**
- 嚴格版替代方案（`WithMethods`／`WithHeaders` 明列）未採用，見設計決策；日後若要收緊，先確認前端送出的標頭清單。
- 政策名稱 `"Frontend"` 字串在 `AddPolicy` 與 `UseCors` 各出現一次，打錯會在第一次請求時才拋；常數化留待下次動該段時處理。
- 200 回應與標頭已以實際請求確認（見變更段）；`PUT /assets/{id}` 的端到端寫入（帶 token、寫 DB，涵蓋 `NormalizeConfig` 的補 `model`、三鍵過濾、`updatedAt` 推進）尚未以 C# 版執行，留待 Phase 5 前端指向本服務時驗。
- `id` 超過 BIGINT 範圍：C# 版先擋回 400（刻意較嚴，註解已說明），Node 版會送進 DB 變 500；統一方向是修 Node（加 BIGINT 上限檢查），記為 backlog。
- PUT 為先 SELECT 再 UPDATE（兩次往返）：中間若該列被刪除會回 500 而非 404；單管理員寫入情境不會發生，不改。
- 數字格式極細微差異：`75.0` Node 回 `75`、C# 回 `75.0`；前端 `JSON.parse` 後等值，不處理。
- 池上限未精確驗證：20 併發 `/health/db` 全部 200 只證明「不報錯」，沒證明上限真是 5；併發 5 個請求本來就會排隊，`Test plan` 無法從回應時間反推出池大小。要精確需壓到 `max_connections` 或觀察 `pg_stat_activity`，成本不划算，記為已知限制，部署後若連線數異常再驗。
- `ConnectionIdleLifetime=30` 實際約 30～40 秒回收：以清理間隔（預設 10 秒）為拍子，與 Node 的精確 30 秒只是近似。
- 本則早期的兩個數字已不代表現況：變更段的 `GET /health 200` 與 preflight 量測、`Timeout=5` 時量的 5.57 秒，均寫於改池設定與 `/health/db` 實作之前。現況數字見變更段 `/health/db` 那條（假連線字串客戶端計時 15426 毫秒）。
- `/health/db` 成功形狀刻意維持 `database: { connected: true }`（Node 版同樣寫死 true，僅在成功時出現）：失敗改走例外、不走 `connected: false`，與 Node 的 `pool.query('SELECT 1')` 失敗直接 500 同構。

## 2026-11-16 — 模型端點 GET /models/{modelKey} 與設定驗證拆分

**背景**
- Phase 4 缺 `GET /models/{modelKey}`：前端切到 remote 後載入模型需要後端提供 glb；Node 版已有 `src/routers/models.ts` 的 `MODEL_FIELDS` 白名單查表與 `res.sendFile`，C# 版缺。
- 11-15 則刻意保留而不加入的 `MODEL_NOT_FOUND` 錯誤碼，此時才有使用端。
- `UpdateAssetDto` 的驗證在 `model` 改為選填後，`JsonObjectOnly`（只檢查「是 JSON 物件」）表達不了逐層可選的語義；且缺欄位要能補預設值，驗證屬性只能讀不能寫。

**變更**
- 新增 `Controllers/ModelController.cs`：`GET /models/{modelKey}`，白名單字典（`materials-variants-shoe` → `MaterialsVariantsShoe.glb`，大小寫敏感）查表後以 `PhysicalFile` 回檔；回 `Content-Type: model/gltf-binary`、`Cache-Control: public, max-age=3600`，開啟 `enableRangeProcessing`。未登記的 key 與磁碟上無檔案都回 404 `MODEL_NOT_FOUND`（白名單命中後仍檢查檔案存在，部署漏檔不回 500）。
- 新增 `ModelFiles/`：`MaterialsVariantsShoe.glb`（7,833,592 bytes）與 `ATTRIBUTION.md`（CC BY 4.0 標註，© 2021 Shopify，與 Node 版 `models/ATTRIBUTION.md` 同義）。
- `Web3D.Backend.csproj`：`None Update="ModelFiles\**\*"` 加 `CopyToOutputDirectory` 與 `CopyToPublishDirectory`（皆 `PreserveNewest`）。
- `Models/UpdateAssetDto.cs`：`JsonObjectOnly` 換成 `JsonObjectConfig`（逐層檢查 `camera`、`cube`、`model`，先確認屬性存在、再確認型別、再取值）；`model` 改為選填，缺欄位由 `ApplyDefault()` 補 `{"model":{"materialColorOverrides":{}}}`。
- `Controllers/AssetController.cs`：`UpdateAsset` 開頭呼叫 `ApplyDefault()`；`id` 檢查改為 `TryParseAssetId(string?, out long)`——正規式修正為 `^[1-9]\d*$`（原 `^[1-9]\d\d*$` 誤寫成須兩位數以上，`GET /assets/1`、`GET /assets/2` 會回 400），並把兩段 `TryParse` 收斂成一次解析。
- `Constants/ApiErrors.cs`：`MODEL_NOT_FOUND` 轉為實際使用，移除「暫未列入」的過期註解。

**設計決策**
- 白名單查表而非 `UseStaticFiles` 開放整個目錄：需求是「只有登記過的模型能取得，其他一律 404」，靜態檔中介層開放整個 `wwwroot/`、條件比目錄更嚴的都得另外擋；且白名單把 modelKey 與檔名解耦，換檔案不用改前端。
- 模型檔放 `ModelFiles/` 而非 `wwwroot/`：同上，`wwwroot/` 放進去就直接可下載，白名單形同虛設。
- 用 `ContentRootPath` 組路徑而非 `Directory.GetCurrentDirectory()`：三種啟動（`dotnet run`、直接執行 `bin/`、發佈後）只有前者在各種情況下都指向檔案實際位置。
- `CopyToOutputDirectory` 與 `CopyToPublishDirectory` 兩條都寫：build 與 publish 是各自獨立的複製流程，只寫前者本機跑得起來、部署後 `/models/` 全部 404。語義是增量（來源較新才複製），不是每次全拷。
- 驗證用自訂 `ValidationAttribute` 逐層檢查而非 `[Required]`：`camera`（position 三軸有限數字、fov 數字）、`cube.color`（非空白字串）、`model.materialColorOverrides`（key 非空白、值符合 `#rrggbb`）三組規則各不相同，宣告式屬性表達不了；且屬性回傳值沒有寫回通道，補值必須放在 Action。
- `model` 缺欄位由 `ApplyDefault()` 補，但顯式 `model: null` 仍 400：缺欄位是「沿用」，顯式 null 是「清空模型關聯」，語義不同，與 Node 版契約一致。
- 404 雙分支（未登記／檔案遺失）都用同一個碼：呼叫端只關心「拿不到」，不需要區分是沒登記還是部署漏檔；後者屬部署問題，可由 404 觀測，不另記 log。
- `id` 先正規式、再 `TryParse`：正規式負責契約，與 Node 版 `^[1-9]\d*$` 一字相同；`TryParse` 只負責擋掉超過 `long` 範圍的長數字，此點 C# 版刻意比 Node 版嚴。兩段收斂成 `TryParseAssetId`，解析只做一次，呼叫端一處判斷即取值。

**學習筆記**
- 2 則，見 LEARNING.md 的 2026-11-16 段（`TryGetProperty` 缺 `!` 導致全部 PUT 400、`Cache-Control` 分號導致不快取）；主題寫入 `docs/learning/static-files.md` 與 `docs/learning/dotnet-project.md`，並補 `aspnet-core.md` §模型驗證、`csharp-basics.md` 集合段。

**已知限制 / 後續**
- 200 回應與標頭尚未以實際請求確認：僅建置確認檔案跟著走（`bin/Debug/net10.0/ModelFiles/` 有 glb），`dotnet run` 後的三條路徑驗證待補。
- 白名單寫死在程式內：新增模型需改程式重啟；與 `assets.model_key` 對齊的資料庫查表待有需求再做。
- 11-15 則「`GET /models/{modelKey}` 未實作、`MODEL_NOT_FOUND` 待補」已解決（歷史則不回改）。

## 2026-11-15 — 錯誤格式與 Node 版對齊

**背景**
- Node 版已上線，錯誤契約寫死為 `{"error":{"code","message"}}`；C# 版先前實作的端點有 5 處偏離：400 回 `{"error":"字串"}`、404 回空本文、未帶 token 的 401 回空本文、未知路由回空本文、未處理例外沒有統一本文。
- 改畢以 11 個實際請求逐一確認，途中發現一處啟動順序問題（見設計決策第 4 點）。

**變更**
- 新增 Models/ApiErrorResponse.cs：錯誤回應之外層（`Error` 包裝 `ApiErrorDetail`，內含 `code` 與 `message`）。
- 新增 Constants/ApiErrors.cs：8 個錯誤碼常數，與 Node 版 errorDefinition.ts 對齊。
- 新增 Middleware/ExceptionMiddleware.cs：攔截下游例外，統一回 500 `INTERNAL_SERVER_ERROR`；完整堆疊只寫 log，回應不帶細節；掛在管線最前。
- Program.cs：`OnChallenge`／`OnForbidden` 改寫統一的 401／403 JSON（攔下框架預設空本文）；`InvalidModelStateResponseFactory` 改回 `ApiErrors.InvalidRequestBody`；新增 `MapFallback("{*path}")`，未知路徑一律回同一份 404；`JWT_SECRET` 未設定即啟動失敗。
- Controllers/AssetController.cs：400／404 改用 `ApiErrors.InvalidAssetId`／`AssetNotFound`，移除手寫匿名物件與舊 TODO。
- Controllers/AuthController.cs：密碼錯誤改用 `ApiErrors.InvalidCredentials`；檔尾多餘之 `;` 一併移除。
- 同步修正過期與誤導的程式註解（不影響行為，純風格）。

**設計決策**
- 錯誤碼集中於 ApiErrors，不散置各 Controller：8 個錯誤碼若散置五處，修改時須同步變更五處；每次呼叫回傳新物件，呼叫端縱使修改內容，亦不影響其他請求。
- ExceptionMiddleware 掛管線最前：中介層僅能攔截下游例外；置於後方將無法涵蓋前方站點。
- MapFallback 顯式指定 `"{*path}"`：預設 fallback 排除形如檔名的路徑；若未指定，`/nope.json` 之回應為空本文（此差異經由實際請求確認）。
- `Configure<ApiBehaviorOptions>` 必須在 `AddControllers()` 之後：選項依註冊順序疊加，先寫入者會被框架內建預設值覆寫。本次唯一的實質錯誤：編譯與靜態檢查皆無法發現，經由送出不合法 JSON 之實際請求方予確認。
- 400 一律 `INVALID_REQUEST_BODY`：Node 版 JSON 解析失敗與內容不合規回同一個碼。
- `/health/db` 刻意不套錯誤格式：維運端點（負載平衡器、喚醒檢查），非供前端解析；與 Node 版形狀相同。
- `MODEL_NOT_FOUND` 暫未列入：對應之 `GET /models/{modelKey}` 尚未實作，現階段新增將無使用端。

**學習筆記**
- 6 則，見 LEARNING.md 的 2026-11-15 段（選項註冊順序、`MapFallback` 的 `{*path}`、`OnChallenge` 與空本文 401、`Build()` 每次建新物件、靜態內容修改後須重新編譯、換語言重寫時規格是否一致）；錯誤四出口寫成主題檔 `docs/learning/error-handling.md`。

**已知限制 / 後續**
- 新發現：`long.TryParse` 仍接受 `+1`、` 1` 等寬鬆輸入，與 Node 版「1-9 開頭」有差；要完全對齊需逐字元檢查。
- 刻意保留：`/health/db` 失敗回 `{ "status": "error" }`，不列入錯誤格式（維運端點；與 Node 版一致）。
- 新缺口：`GET /models/{modelKey}` 未實作，`MODEL_NOT_FOUND` 待那時補。
- 11-13 則「400 本文形狀待 Phase 3」已解決（歷史則不回改）。

## 2026-11-14 — 登入、JWT 與寫入權限

**背景**
- Phase 4 缺 `POST /auth/login`；`PUT /assets/{id}` 自 11-13 起無任何驗證（Node 版有 `requireAdmin`），權限缺口需一併補上。
- 部署環境需要健康檢查端點：負載平衡器與冷啟動喚醒須有一支不查資料庫的輕量探測，另須一支確認資料庫連線狀況。

**變更**
- 新增 `Controllers/AuthController.cs`：`POST /auth/login`，密碼正確回 `{ token }`，錯誤回 401（回應本文於 11-15 統一為契約形狀）。
- 新增 `Services/AuthService.cs`：驗證 `ADMIN_PASSWORD` 並簽發 HS256 token（claims 為 `role=admin` 與 `iat`，效期 15 分鐘）。
- 新增 `Models/LoginDto.cs`：登入請求本文僅 `password` 一個欄位；缺欄位與空字串皆於模型驗證階段回 400。
- 新增 `Controllers/HealthController.cs`：`GET /health` 回 `{status:"ok"}`、不查資料庫；`GET /health/db` 以 `CanConnectAsync` 實際連線並回 `durationMs`，失敗回 500 `{status:"error"}`。
- `Controllers/AssetController.cs`：`PUT /assets/{id}` 加 `[Authorize(Roles = "admin")]`。
- `Program.cs`：`AddAuthentication` 加 `AddJwtBearer`（`MapInboundClaims = false`、`RoleClaimType = "role"`），並加入 `UseAuthentication`／`UseAuthorization`。
- `Web3D.Backend.csproj`：新增 `Microsoft.AspNetCore.Authentication.JwtBearer`。
- README 更新 Tech Stack（JWT HS256）與 Roadmap；新增主題檔 `docs/learning/auth.md`、`docs/learning/dotnet-packages.md`。

**設計決策**
- 簽章演算法採 `HmacSha256`（HS256）而非 `Sha256`：後者為雜湊演算法常數，簽章階段找不到對應實作即拋例外（本次實際發生，登入端點回 500）。與 Node 版 `JWT_ALGORITHM` 同值。
- `MapInboundClaims = false`：框架預設將 `role` 改寫為 `ClaimTypes.Role` 的長 URI，改寫後 `[Authorize(Roles = "admin")]` 找不到名為 `role` 的 claim，回應為 403；關閉改寫後 claim 保持簽發時原名，亦與 Node 版 payload 一致。
- 效期 15 分鐘、claims 僅 `role` 與 `iat`：與 Node 版 `TOKEN_EXPIRES_IN = "15m"` 一致；`iat` 以 `ClaimValueTypes.Integer64` 宣告為整數，否則將存為字串而與 Node 版簽出之 payload 不同。
- 密碼比對採 `CryptographicOperations.FixedTimeEquals`：一般相等判斷於首個相異字元即回傳，可依回應時間差反推密碼；固定時間比對每次皆比完整段。長度不同直接回 false，對應 Node 版先比長度再 `timingSafeEqual`。
- `LoginDto` 以 `[Required(AllowEmptyStrings = true)]` 搭配 `[MinLength(1)]`：缺欄位與空字串兩種情況皆落在模型驗證、均回 400，Action 內不需另寫判斷。
- `/health` 與 `/health/db` 分兩支：前者供負載平衡器與冷啟動喚醒高頻探測，不應因資料庫短暫緩慢而誤判服務失效；後者方實際連線，並回 `durationMs` 以區分資料庫緩慢與網路延遲。
- JWT 驗證處理器以獨立套件提供：`[Authorize]` 本體位於共享框架內，`AddJwtBearer` 所需之處理器已移出框架，須另行安裝。

**學習筆記**
- 3 則，見 LEARNING.md 的 2026-11-14 段（`HmacSha256` 與 `Sha256` 之差異、`MapInboundClaims` 與 `Roles` 授權、JWT 型別所屬套件）；主題寫入 `docs/learning/auth.md` 與 `docs/learning/dotnet-packages.md`。

**已知限制 / 後續**
- 11-13 則「PUT 沒有管理者 token 驗證」已解決（歷史則不回改）。
- 統一錯誤格式仍待 Phase 3：未帶 token 的 401 與未知路由回空本文（11-15 已完成）。
- `ADMIN_PASSWORD`、`JWT_SECRET` 未設定時於呼叫當下才拋例外，啟動階段不檢查（11-15 已改為啟動即失敗）。
- token 無撤銷機制：登出僅由前端丟棄 token，效期內仍可使用；現階段以 15 分鐘短效期為唯一控制手段。
- `GET /models/{modelKey}` 尚未實作，為 Phase 4 剩項。
- 前端尚未以 `VITE_API_BASE_URL` 指向本服務實測登入（Phase 5）。

## 2026-11-13 — 單筆讀取、更新回傳與筆記拆檔

**背景**
- GET /assets/{id} 與 PUT /assets/{id} 實作完成，單筆讀取的契約形狀（id 字串、config 讀取、時間格式）需要一支端點真正行使。
- 學習筆記（LEARNING.md）三天累積到約 1.1 萬字，與已完工的 Node 版（1.4 萬字歷經 9 天）相比成長約 2.4 倍：C# 是新語言，每個概念多一層語言說明與 Node 對照。

**變更**
- 新增 GET /assets/{id}：Controllers/AssetController.cs 加路由，id 以 long.TryParse 解析、失敗回 400。
- PUT /assets/{id} 改回 200 加完整內容（原 NoContent 前端拿不到更新後的資源）；新增 Models/AssetDetailDto.cs 與 Models/UpdateAssetDto.cs，重複轉換抽成共用 static 方法。
- 學習筆記新增第 4 個主題檔 docs/learning/architecture.md（架構總覽：整體圖、一次請求 7 站、名詞地圖、檔案地圖、讀碼順序）；aspnet-core.md 章節改按執行順序排列（啟動→管線→路由→繫結→驗證→DI→分層→設定）並修正標題層級；LEARNING 索引標為先讀。

**設計決策**
- id 驗證放在 Controller 用 long.TryParse：路由無條件匹配（`{id}`），「不合法」在程式碼判斷後回 400；若用路由約束（`{id:long}`），不合法會變成 404（框架視為不存在的網址）。
- PUT 回 200 加完整內容而非 204 NoContent：與 Node.js 版一致，前端可直接用回應更新畫面，省一次 GET。
- 詳細實體與摘要 DTO 分兩個型別：列表不需要 config 與時間，形狀不同；轉換抽成共用方法避免兩端點漂移。
- updated_at 輸出格式固定為 `yyyy-MM-ddTHH:mm:ss.fffZ`（ToUniversalTime 後格式化），與 Node 版 toISOString 一字不差。
- config 整包以 JsonDocument 解析、Clone 後回傳：JsonElement 依附文件生命，離開 using 即失效，Clone 才能安全帶出。

**學習筆記**
- 2 則，見 LEARNING.md 的 2026-11-13 段（JsonElement 入口、自訂驗證屬性）；三個大主題已分別搬入 docs/learning 的對應檔案。GET by id、PUT、DTO 拆分的其餘問答本文因工具中斷遺失未收錄，結論保留在本則設計決策。

**已知限制 / 後續**
- PUT 沒有管理者 token 驗證（Node 版有 requireAdmin）：留待登入端點（Phase 4 POST /auth/login）實作時一起補。
- 400 回應本文形狀與 Node 版不同（Node 版是 INVALID_ASSET_ID）：統一錯誤格式留待 Phase 3。
- long.TryParse 會接受 `+1`、` 1` 等寬鬆輸入，與 Node 版「只接受 1-9 開頭」略有差異；要完全對齊需逐字元檢查。
- config 欄位的內容驗證（逐欄檢查）尚未實作，今天只確認「整包可讀取回傳」。


## 2026-11-12 — 骨架與唯讀 API
**背景**
- 建出可跑的 C# 後端骨架：分層資料夾（Controllers、Services、Data、Models）與第一支唯讀端點 GET /assets。
- 沿用 Node.js 版的資料庫（同一顆 PostgreSQL、同一套 000-004 migration）；C# 版不執行 migration、只讀寫資料。
**變更**
- 新增 Controllers/AssetController.cs（GET /assets）、Services/AssetService.cs、Data/AppDbContext.cs、Models/Asset.cs 與 Models/AssetSummaryDto.cs。
- Program.cs 註冊 AppDbContext 與 AssetService，讀取 ConnectionStrings:DefaultConnection，讀不到即啟動失敗。
- 連線字串改走 dotnet user-secrets，launchSettings.json 開發埠改為 3000。
- README.md 修正設定指令並新增 Progress Log、Project Structure；LEARNING.md 新增 2026-11-12 一則。
- Properties/launchSettings.json 兩個 profile 一致指向 3000。
**設計決策**
- 連線字串用 ASP.NET Core 的 ConnectionStrings 慣例，不沿用 Node 版的 DATABASE_URL：後者是 Node 生態的約定，.NET 這邊硬套要多寫一段網址轉格式的程式碼；設定名稱屬實作層，不列入跨版本契約。
- API 回傳 id 用字串（Id.ToString() 在 EF Core 翻譯成 CAST(id AS text)），與 Node 版 pg 驅動預設一致，避開 BIGINT 超過 JS 安全整數範圍的問題。
- updated_at 與 created_at 不選邊：目前沒有寫入端點，不預設由誰提供時間；等有寫入端點時再依契約決定。
**學習筆記**
- 10 則，見 LEARNING.md 的 2026-11-12 段。
**已知限制 / 後續**
- CORS 尚未設定：前端切換後端時瀏覽器會擋請求，留待對應階段處理。
- GET /assets/{id} 與 PUT /assets/{id} 尚未實作，config 與 updated_at 的讀取尚未被任何端點行使。
- 3000 與 Node.js 版同埠，兩版不可同時啟動；要對照驗證時先後啟動。

## 2026-11-11 — 專案初始化

**背景**
- 3D Viewer 的後端原本是 Node.js 版（web3D_proto_backend），已完成並部署。
- 改以 C#（ASP.NET Core）重建同一個後端：端點、錯誤碼、資料形狀都沿用，前端不需改程式。

**變更**
- 新增 README.md：規格沿用 Node.js 版，標明共用且不可變動的部分（API 契約、資料庫結構、migration 內容），
  並只補 C# 版需要調整的實作方式。
- 新增 CHANGELOG.md（本檔）。

**設計決策**
- API 契約與資料庫結構視為共用且不可變動：兩個實作接同一顆資料庫、同一套端點，前端只認這份契約。
  任一版自行改動，另一版與前端會立刻對不上，因此 C# 版只換實作方式，不改契約與 schema。
  日後真的要改，得兩個版本一起改、一起上線。
- 規格仍以 Node.js 版為準，不重寫一份：同一套契約由兩份文件各自維護，改一邊就會漂移；
  C# 版只記錄實作上的差異（資料存取、設定來源、序列化格式）。
- 資料存取用 EF Core，資料表結構用自己寫的 SQL migration：EF Core 不啟用自己的 migration，
  避免兩套版本工具互相覆蓋；代價是新增欄位要同時改 SQL 與 C# 實體。
- 先寫契約、後補結構：資料夾分層與檔案對照等實作完成後再寫，
  避免文件描述不存在的檔案，日後改動還要回頭修文件。

**學習筆記**
- 3 則，見 LEARNING.md 的 2026-11-11 段。

**已知限制 / 後續**
- updated_at 由應用程式提供（DateTimeOffset.UtcNow），Node 版用資料庫的 CURRENT_TIMESTAMP；
  單一實例下差異可忽略，多實例或需要嚴格一致時要改回資料庫時間。
- 新增資料表欄位要改兩處（SQL migration 與 C# 實體），EF Core 不會主動檢查兩邊是否一致。
- 模型檔的存放位置與提供方式留待實作時決定，屆時再補進 README；端點本身已定在 API 表格。
