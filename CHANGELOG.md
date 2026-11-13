# Changelog

依日期記錄後端的變更與設計決策。

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
