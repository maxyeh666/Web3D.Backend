# Changelog

依日期記錄後端的變更與設計決策。

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
- 10 則，見 LEARNING.md 的 2026-11-12 段（架構、Entity、DI、DTO、使用方式、查詢翻譯、型別對照）。
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
