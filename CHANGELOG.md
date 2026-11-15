# Changelog

依日期記錄後端的變更與設計決策。

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
