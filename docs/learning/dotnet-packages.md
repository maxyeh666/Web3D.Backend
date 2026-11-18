# .NET 套件：怎麼找、要不要裝、自己寫還是用現成的

> 對應的程式變更與設計理由見 [CHANGELOG.md](../CHANGELOG.md)；JWT 專屬的型別說明見 [JWT 登入與授權](./auth.md)。

先統一五個詞：BCL（.NET 內建的基礎類別庫：字串、集合、時間、檔案這些，不裝任何東西就有）；共享框架（shared framework：隨 SDK 附的大包，例如 ASP.NET Core 的 MVC、DI、`[Authorize]`；`Microsoft.NET.Sdk.Web` 專案預設引用它）；NuGet（.NET 的套件市集：像 npm registry）；`PackageReference`（csproj 裡宣告「我要用某套件」的行）；transitive（間接相依：你裝 A，A 自己又要 B，B 就是 transitive——你沒寫它，它還是會被拉進來）。

## 先分類：這件事是不是全世界都做過

- 通用能力（登入、JWT、CSV、排程、ORM、Excel／PDF、時區）：幾乎必有套件，別自製。
- 格式與安全規範（加密、token 簽驗、密碼雜湊、RFC 格式）：一定用套件，自己寫容易漏檢查（例如漏驗 `exp` 或沒鎖 `alg`，那是真的安全漏洞）。
- 業務邏輯（本專案的資產篩選、回應形狀、錯誤格式）：沒有套件，自己寫。

這一步就砍掉大半搜尋工作。

## 三層階梯：由低成本往高成本找

| 層 | 例子（本專案） | 要不要動 csproj |
|---|---|---|
| BCL（.NET 內建） | `Encoding`、`DateTimeOffset`、`CryptographicOperations`、`Stopwatch` | 不用 |
| 共享框架（`Microsoft.NET.Sdk.Web` 自帶 `Microsoft.AspNetCore.App`） | MVC、`ControllerBase`、DI、`AddAuthentication`、`AddAuthorization`、`[Authorize]` | 不用 |
| 獨立套件 | `Microsoft.AspNetCore.Authentication.JwtBearer`、`System.IdentityModel.Tokens.Jwt`、`Microsoft.EntityFrameworkCore`＋`Npgsql…` | 要加 `PackageReference` |

關鍵例子：`[Authorize]` 本體在共享框架裡，所以今天沒為它裝任何東西；但 Bearer 驗證處理器在 .NET Core 3.0 之後移出框架，`AddJwtBearer` 一定要加那個套件。

命名空間與套件名的對應：多數情況套件名等於命名空間前綴，但不是定律（例如 `Npgsql.EntityFrameworkCore.PostgreSQL` 提供的東西掛在 EF 的命名空間下）。不確定就查，不要猜。

## 程式碼用到型別，就自己宣告（不靠間接相依）

`Program.cs` 用到了 `NpgsqlConnectionStringBuilder`，csproj 就自己加 `Npgsql` 的 `PackageReference`，即使 provider 套件已經把它拉進來（間接相依只是 provider 的實作細節；它哪天換版本或換內建方式，程式編的還是舊簽名，之後出錯難以定位）。判斷準則：程式出現該套件的型別名稱就宣告；只出現在別的套件文件裡就不宣告。

## 五個實際查法

1. `dotnet package search <功能英文關鍵字> --take 5`：實測 `csv` → `CsvHelper` 7.3 億下載（事實標準）；`cron` → `NCrontab` 2.2 億、`Cronos` 8,000 萬。破億幾乎就是大家都在用；只有幾萬就要看維護者與最後更新日。
2. 官方文件：搜 `site:learn.microsoft.com how to <功能> aspnet core`；官方教學若只用內建 API，表示不用套件。
3. 看別人怎麼做：搜 `how to <功能> in .NET`，看熱門 GitHub repo 的 `.csproj`（產業慣例比文件更貼近實務）。
4. 領域清單：GitHub `awesome-dotnet` 分類清單，一次看到該領域所有選項。
5. 評估安全性：`dotnet list package --vulnerable`（有已知漏洞就別用）；`dotnet nuget why <專案> <套件>`（查某套件是被誰拉進來的）。

IDE 內建的查法：先不加套件直接寫，紅線 `... could not be found (are you missing a using directive or an assembly reference?)` 表示缺 using 或缺套件，按 `Ctrl+.` 常直接提議安裝對應的 NuGet 套件。

## 什麼時候該自己寫

- 業務規則（本專案的查詢、回應形狀、錯誤格式）。
- 20 行內寫得完、且不涉及安全與格式規範。
- 引入套件比問題本身大一個量級。
- 套件停更、有漏洞、授權不合。

## 五個需求走一遍

| 需求 | 分類 | 結論 |
|---|---|---|
| 驗請求帶來的 token | 通用＋安全 | 官方 `Microsoft.AspNetCore.Authentication.JwtBearer`，不自製 |
| 簽登入 token | 通用＋格式規範 | `System.IdentityModel.Tokens.Jwt` |
| 連 PostgreSQL | 通用＋安全（注入、連線池） | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| 量 DB 連線耗時 | BCL 就夠 | `Stopwatch`，不裝 |
| 依 id 找資產、回固定形狀 | 業務 | `AssetService` 自己寫 |

實務捷徑：先問「這件事是不是全世界都做過」，是就搜 NuGet 關鍵字看第 1 名下載數，不是就自己寫。多數新功能 30 秒內就能分類完。
