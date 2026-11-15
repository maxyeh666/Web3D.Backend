# 統一錯誤處理：四個出口回同一形狀

錯誤契約固定為 `{"error": {"code", "message"}}`，錯誤碼與 Node 版一致（Node 版已上線，故 C# 版不更動契約）。但在 C# 這邊，一個請求的錯誤可能來自四個地方；本檔說明各出口之負責者，以及最終如何收斂為同一形狀。實作當天的問答仍留 `LEARNING.md` 的日期段，這裡只收可反覆查閱的說明。

## 出口一：模型驗證失敗 → 400 INVALID_REQUEST_BODY

模型驗證（model validation：繫結完成後、Action 執行前，檢查轉型錯誤與參數上的驗證屬性）失敗時，`[ApiController]` 預設自動回 400、本文是 ProblemDetails（微軟定義的錯誤格式，欄位為 title、status、errors）。本專案以 `Program.cs` 的 `InvalidModelStateResponseFactory` 將其替換為契約形狀的 `ApiErrors.InvalidRequestBody`。

順序限制：`Configure<ApiBehaviorOptions>` 必須寫在 `AddControllers()` 之後。`builder.Services` 收集的是延後執行的設定動作（選項物件依註冊順序疊加，後登記者覆寫先登記者）；`AddControllers()` 內部亦註冊一組框架預設值，先寫入的 `Configure` 會被覆寫。本次經由送出不合法 JSON 之實際請求發現——編譯與啟動皆正常，僅實際請求之回應形狀有誤。詳見 `LEARNING.md` 2026-11-15 段第一則。

## 出口二：Controller 明確回錯誤碼

`id` 格式不對、查無資料、密碼錯誤，這些是 Controller 自己判斷後回的：`BadRequest(ApiErrors.InvalidAssetId)`、`NotFound(ApiErrors.AssetNotFound)`、`Unauthorized(ApiErrors.InvalidCredentials)`。錯誤碼集中於 `Constants/ApiErrors.cs` 一處（8 個，與 Node 版 `errorDefinition.ts` 對齊），不散置各 Controller 書寫字串：修改時僅需變更一處。每次呼叫回傳新物件，呼叫端縱使修改內容，亦不影響其他請求。

## 出口三：未處理的例外 → 500 INTERNAL_SERVER_ERROR

`Middleware/ExceptionMiddleware.cs` 以 try/catch 涵蓋下游管線：其後任一站點拋出未處理之例外，catch 區塊記錄 log（完整堆疊，僅供維運查閱），再回應固定訊息之 500，不向呼叫端揭露內部細節。Node 版對應者為 `src/errorHandler.ts` 的最後一站。

位置必須在管線最前：中介層僅能攔截「下游」例外，置於後方將無法涵蓋前方站點（認證、路由、DB）。故置於 `UseHttpsRedirection` 之前。

## 出口四：無路由匹配 → 404 ROUTE_NOT_FOUND

`Program.cs` 尾端之 `MapFallback("{*path}")` 承接所有未被匹配之請求。`"{*path}"` 為 catch-all 路由參數（`*` 表示涵蓋整段剩餘路徑，含斜線），須顯式指定：預設之 `MapFallback` 會排除形如檔名的路徑（此類路徑屬靜態檔案中介層之處理範圍）；若未指定，`/nope.json` 之回應為空本文，而非統一的 404。指定 `"{*path}"` 後，未匹配之路由一律收斂為同一份 `ROUTE_NOT_FOUND` JSON。置於最後之原因：其為所有路由皆未匹配時方進入之站點；若置於前方，將攔截正常路由。詳見 `LEARNING.md` 2026-11-15 段第二則。

## 不收斂的例外：/health/db

`GET /health/db` 失敗回 `{ "status": "error" }`，刻意不套錯誤格式。此為維運端點（負載平衡器、冷啟動喚醒檢查），呼叫端為機器而非前端；Node 版形狀相同，兩邊一致即可。

## 與 Node 版的對照

| Node 版 | C# 版 | 說明 |
| --- | --- | --- |
| `src/errorHandler.ts` 的 `error` 包裝 | `Models/ApiErrorResponse.cs`（外層 `Error`） | 形狀 `{"error": {"code", "message"}}` |
| `src/errors/errorDefinition.ts` | `Constants/ApiErrors.cs` | 8 碼一字對齊；`MODEL_NOT_FOUND` 待 `GET /models/{modelKey}` 實作 |
| `errorHandler` 最後一站 | `Middleware/ExceptionMiddleware.cs` | 未處理例外 → 500 |
| `routers/*.ts` 的請求驗證失敗分支 | `InvalidModelStateResponseFactory` | 400 一律 `INVALID_REQUEST_BODY` |
| `Challenge` 空本文（Node 版自行撰寫之 unauthorized 中介層） | `OnChallenge` 加 `HandleResponse()` | 若未攔截，將收到框架預設之空本文 401；詳見 `LEARNING.md` 2026-11-15 段第三則 |

## 驗證方式

啟動後送出 11 個實際請求（PowerShell 傳送 JSON 須先寫入檔案，再以 `curl --data @檔` 傳送；單引號內之 `\"` 會被原樣送出）：

- `GET /health/db` 200（服務與資料庫皆正常運作）
- `GET /assets/abc` 400 `INVALID_ASSET_ID`
- `GET /assets/999999` 404 `ASSET_NOT_FOUND`
- `GET /nope` 與 `GET /nope.json` 皆為 404 `ROUTE_NOT_FOUND`（後者驗證 `{*path}`）
- `DELETE /assets/1` 404 `ROUTE_NOT_FOUND`（該路由不存在）
- 未攜帶 token 呼叫 PUT 回 401 `UNAUTHORIZED`；以錯誤密碼登入回 401 `INVALID_CREDENTIALS`
- `POST /assets/1` 傳送 `{}`（不合法 JSON）回 400 `INVALID_REQUEST_BODY`（驗證 `InvalidModelStateResponseFactory` 已生效）
- 上述回應皆為 `{"error": {"code", "message"}}` 同一形狀。
