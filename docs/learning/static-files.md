# 模型檔託管：不用靜態檔中介層，改由 Controller 回檔

記「模型檔（.glb）怎麼送到瀏覽器」的整套機制：為什麼不用 `UseStaticFiles`、檔案放在哪、怎麼跟著部署走、回應要帶哪些標頭。

## 為什麼不用靜態檔中介層

ASP.NET Core 內建的 `UseStaticFiles` 會把 `wwwroot/`（預設的公開資料夾：放進去的檔案不經過程式就能直接下載，相當於前端專案的 `public/`）整個對外開放：任意檔名都能下載、目錄穿越（用 `..` 跳到上層目錄）還要另外擋。本專案要的是「只有登記過的模型能拿到，其他一律 404」，條件比目錄更嚴，所以不用資料夾權限，改用白名單查表：

- `GET /models/{modelKey}` 由 `Controllers/ModelController.cs` 處理（而不是掛靜態檔中介層：收到請求先看公開資料夾、有同名檔案就直接回傳、不進 Controller 的那一層）
- `ModelFiles` 字典把 `modelKey` 對到檔名：`materials-variants-shoe` → `MaterialsVariantsShoe.glb`
- 沒登記的 key 回 `MODEL_NOT_FOUND` 404；不列出可用清單，也不回 500

白名單還有一個好處：modelKey 與檔名解耦。前端與資料庫（`assets.model_key`）只認 key，換檔案（優化版 glb、不同解析度）不用改前端；在字典多加一行就是多一個模型，模型變多且需動態管理時再改由資料庫查表（與 `assets.model_key` 對齊）。

Node 版是同一個決策：`src/routers/models.ts` 的 `MODEL_FIELDS` 查表 + `res.sendFile`（Express 回檔案的方法），不用 `express.static`（Express 內建的靜態檔中介層：把指定資料夾對外開放）。

## 檔案放在哪：ContentRootPath 與三種啟動方式

模型檔放在後端根目錄的 `ModelFiles/`（授權標註同目錄），不是 `wwwroot/`。程式用 `IWebHostEnvironment.ContentRootPath`（應用程式根目錄：框架定義的「程式家在哪」）組路徑，而不用 `Directory.GetCurrentDirectory()`（目前工作目錄：啟動命令在哪個目錄執行）：

| 啟動方式 | ContentRootPath | 目前工作目錄 |
| --- | --- | --- |
| `dotnet run` | 專案目錄 | 執行命令的目錄 |
| 直接執行 `bin/` 內的執行檔 | 輸出目錄 | 執行命令的目錄 |
| 發佈後（Render） | 發佈目錄 | 啟動腳本決定的目錄 |

只有 ContentRootPath 在三種情況都指向「檔案實際的位置」。Node 版用 `path.resolve(process.cwd())`，等於假設「工作目錄就是後端根目錄」；換啟動方式時要檢查。C# 這邊沒有這個隱含假設。

## 檔案怎麼跟著部署走：csproj 的複製規則

原始檔在專案目錄，執行檔在輸出目錄（`bin/Debug/net10.0/`）：兩者不同，程式跑的是輸出目錄那份。`ModelFiles` 是新加的資料夾，預設不會被複製過去，所以 `Web3D.Backend.csproj` 補了一條複製規則（`None` 類別加 `CopyToOutputDirectory` 與 `CopyToPublishDirectory`：本機建置只複製比輸出新的檔案，發佈同樣帶上）。`None`／`Update`／增量複製／兩條流程的機制見 [.NET 專案設定](./dotnet-project.md) §複製規則。

少了這段的症狀：本機 `dotnet run` 看不出問題（讀的是專案目錄），但執行輸出目錄的執行檔或部署後，`/models/{modelKey}` 全部 404。判斷方式：`bin/Debug/net10.0/ModelFiles/` 內有沒有 glb，沒有就是這條規則沒生效。

Node 版的對應機制是「整個 `models/` 跟著 git 拉下來」而不是編譯複製：Render 從 git 拉程式碼才有檔案，所以 `models/*.glb` 必須 commit 進 repo（約 7.8MB），否則正式環境 `/models/...` 全部 404。兩邊教訓相同：模型檔不在編譯產物內，只能靠「檔案有跟著走」來保證。

## 回一個檔案要帶的標頭

回檔案不只是回 bytes，有四個標頭要處理：

1. `Content-Type`：`model/gltf-binary`。瀏覽器認這個 MIME（MIME：標示檔案種類的字串，例如 `text/html` 是網頁、`image/png` 是圖片）才知道是 glTF 二進位；由 `PhysicalFile`（從磁碟讀檔並串流回傳的輔助方法）的第二個參數指定。
2. `Cache-Control`：`public, max-age=3600`，告訴瀏覽器快取一小時（模型檔約 7.8MB 且內容固定，每次冷載重新下載成本很高）。語法注意：多個指令用逗號分隔，`public; max-age=3600` 的分號是錯的，整串會被當成無法辨識的指令而忽略，等於沒有快取。
3. `Last-Modified`（檔案最後修改時間） / `ETag`（檔案內容的指紋，內容一變就換）：`PhysicalFile` 依檔案資訊自動帶上。`max-age` 過期後，瀏覽器用 `If-Modified-Since` / `If-None-Match` 做條件請求（拿著手上的時間與指紋問伺服器「檔案有沒有變」；沒變就回 304（Not Modified：用手上那份，不重傳內容））。
4. `Accept-Ranges`（伺服器宣告「我支援分段下載」的回應標頭）：`enableRangeProcessing: true` 開啟後支援 Range 請求（分段下載）；Node 版的 `send`（`res.sendFile` 底層實際送檔的函式）預設就有。

順序限制：標頭要在回應開始送出前設定。`PhysicalFile` 開始串流後再寫 `Response.Headers` 會拋例外，所以標頭一律寫在 `return PhysicalFile` 之前。另外標頭只在檔案確定存在後才設：404 的 JSON 不能拿到 glTF 的 `Content-Type`，否則前端的錯誤解析會先壞掉（Node 版同樣由 `send` 在檔案存在後才設，404 走錯誤處理路徑）。

## 驗證方式

建置後先看檔案有沒有跟著走，再看狀態碼與標頭：

- `bin/Debug/net10.0/ModelFiles/` 內有 `MaterialsVariantsShoe.glb` 與 `ATTRIBUTION.md`
- `/models/materials-variants-shoe` → 200、`Content-Type: model/gltf-binary`、`Accept-Ranges: bytes`
- `/models/nope` → 404 `MODEL_NOT_FOUND`（本文為契約形狀的 JSON，不是 glb）
