# JWT 登入與授權

> 對應的程式變更與設計理由見 [CHANGELOG.md](../CHANGELOG.md)；按天紀錄見 [LEARNING.md](../LEARNING.md)。

## 怎麼讀這份筆記

每節盡量照四段寫：這是什麼（白話一句）／長相或例子（給你看真的長相）／誰用它（簽發端、驗證端、還是兩邊）／本專案怎麼用（對應到哪行程式）。縮寫第一次出現一律寫全名加中文。

## token 實際長相

一張 token 就是三段，以點連接：

`<標頭 base64>.<payload base64>.<簽章 base64>`

把前兩段解碼（base64url 解碼，例如貼到 jwt.io），會看到：

標頭：`{"alg":"HS256","typ":"JWT"}`
payload：`{"role":"admin","iat":1763104000,"exp":1763104900,"nbf":1763104000}`

（解碼後形狀示意；數字是 Unix 秒。）`alg` 說「用 HS256 驗我」；payload 就是 claims：自訂的 `role` 加上三個時間（簽發／到期／生效，下節縮寫表有全名）。簽章段看不懂是正常的，它是給程式驗的，不是給人看的。

## JWT 是什麼

JWT（JSON Web Token：前後端傳遞登入狀態的一種 token 格式）由三段組成，以點連接：標頭（用哪種演算法簽）、承載（payload：放的資料，例如角色與過期時間）、簽章（用密鑰對前兩段算出的驗證碼）。後端簽發後交給前端，之後前端每個寫入請求都帶上它，後端驗簽章確認是自己發的、沒被改過。

重點：簽章只保證沒被竄改，不保證看不懂——payload 只是 base64 編碼，任何人都能解開看。所以 token 裡只放角色與效期，不放密碼這類機密。

## 縮寫與專有名詞表

| 縮寫 | 全名 | 白話 |
|---|---|---|
| `iat` | issued at | 簽發於：這張 token 什麼時候發的 |
| `exp` | expiration time | 到期時間：過了就拒收 |
| `nbf` | not before | 生效時間：在此之前不算數 |
| `iss` | issuer | 發行者：誰發的（本專案沒用） |
| `aud` | audience | 對象：發給誰用的（本專案沒用） |
| `sub` | subject | 主體：這張票屬於誰（本專案沒用） |
| `jti` | JWT ID | token 編號：防重放用（本專案沒用） |
| `HS256` | HMAC-SHA256 | 用金鑰參與的 SHA-256 簽章（見下節） |
| `HMAC` | Hash-based Message Authentication Code | 金鑰雜湊訊息驗證碼：一種把金鑰混進雜湊計算的做法 |
| base64url | — | base64 的 URL 安全版：`+`／`/` 換成 `-`／`_`，所以 token 可放標頭或 URL |
| `Bearer` | — | 持票人：標頭寫 `Bearer <token>` 就是「憑這張票辦事」 |

## HS256 與 SHA256

這是什麼：`SHA256` 是雜湊演算法（把任意資料壓成固定長度的亂碼指紋；誰都能算，不需要金鑰）。簽 token 要的是 `HMAC-SHA256`（簡寫 HS256）：做法是把金鑰混進 SHA-256 的計算，所以只有拿著同一把金鑰的人能重算出金鑰混合指紋——這就是簽章能防偽造的原理。

長相：`SecurityAlgorithms.HmacSha256` 的值就是字串 `HS256`，會寫進 token 標頭的 `alg`（見「token 實際長相」節）。Node 版 `constants/jwt.ts` 的 `HS256` 是同一個東西。

誰用它：簽發端用它算簽章；驗證端照 `alg` 選同一種演算法重算，比對簽章。

本專案怎麼用：`SigningCredentials(key, SecurityAlgorithms.HmacSha256)`。今天修掉的 bug：之前用 `Sha256`（純雜湊名稱，值 `SHA256`），框架在簽章演算法表裡找不到它，就在簽 token 的那一刻丟例外，`POST /auth/login` 直接回 500。對稱式金鑰（簽和驗共用同一把 `JWT_SECRET`）是兩版對得上的前提：任何人都能算 SHA256，但只有持鑰者能算 HS256。

## claim、`role` 與 `[Authorize(Roles)]`

這是什麼：claim 是 token 的 payload 裡的一個鍵值（上面「token 實際長相」節的 `role`、`iat`、`exp`、`nbf` 都是 claims）。名字分兩種：規格登記的（`iat`、`exp` 等，見縮寫表，任何拿到 token 的程式都認得）、專案自訂的（`role`，只有簽發和驗證兩邊約好才有效）。

誰用它：簽發端寫入，驗證端讀出。本專案的三處名字必須一致：簽發 `new Claim("role", "admin")` → 驗證 `RoleClaimType = "role"` → 宣告 `[Authorize(Roles = "admin")]`。錯一個（例如寫成 `rule`）授權就永遠不通過。本專案只有寫入（PUT）掛這個規則，讀取（GET）公開。

本專案怎麼驗：登入時簽一張只有 `role: admin` 的 token（15 分鐘有效）。PUT 帶 `Authorization: Bearer <token>` 來時，框架驗簽章、讀 `role`、比對 `admin`，三關都過才放行。

## 認證與授權：兩支 middleware 與順序

認證（Authentication：確認你是誰）由 `UseAuthentication` 做：從 `Authorization` 標頭取出 Bearer token，按 `TokenValidationParameters` 驗。授權（Authorization：判斷有沒有權限）由 `UseAuthorization` 做：執行 `[Authorize]` 的規則。順序必須是認證在前、授權在後，最後才掛路由；反了或少了認證，規則就沒有身分可比，寫入保護等於沒開。

## `MapInboundClaims` 與 `RoleClaimType` 的配對

這是什麼：inbound claim mapping 是 .NET 框架的預設改寫規則——舊時代（WS-Federation／企業單一登入）的 claim 名就是長 URI（例如 `role` 叫 `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`），框架為了相容，把收到的短名自動翻譯成長名。

誰被影響：`RoleClaimType = "role"` 的意思是「去找名叫 `role` 的 claim 做角色判斷」。改寫後根本沒有叫 `role` 的 claim，授權永遠不通過（會是 403，不會有任何錯誤訊息告訴你名字被改了）。

本專案怎麼用：`options.MapInboundClaims = false` 把改寫關掉，claim 保持簽發時的原名。這同時達成第二個效果：與 Node 版的 payload 一字相同，兩邊才對得上。關掉的代價：如果哪天要接只認長 URI 的外部系統，要自己處理對應——本專案沒有這種系統。

## `ClockSkew`（過期容忍時間）

這是什麼：`ClockSkew`（時鐘偏移容忍）是 token 過期後還願意接受的緩衝時間，預設 5 分鐘。為什麼需要緩衝：分散式系統裡，簽 token 的機器和驗 token 的機器時鐘不一定同步（虛擬機時鐘漂移、NTP 對時延遲都是常態），驗證端就用這個寬限避免「其實剛過期幾秒、時鐘又差幾秒」被誤殺。

本專案怎麼用：設成 `TimeSpan.Zero`（零寬限）。Node 版 `jsonwebtoken` 沒有這個寬限，過期即失效；要讓同一個 token 在兩版的可使用時間一致（都是 15 分鐘整），這裡就不能多給 5 分鐘。

## 密碼比對：為何用 `FixedTimeEquals`

這是什麼：時序攻擊（timing attack）是用「回應快慢」偷資訊。想像密碼是 8 碼，程式用 `==` 從左比到右：第一個字就錯，0.001 秒回；前 7 個都對、第 8 個錯，0.008 秒回。攻擊者一個位置試 62 種字元，挑回應最慢的那個，逐字拼出密碼——每多對一個字就慢一點，這就是洩漏的線索。

解法：`FixedTimeEquals`（固定時間比對：不管對錯都比完完整段）每次耗時相同，慢不慢跟對幾個字無關，線索消失。長度不同它直接回 false：長度本來就藏不住（字串長度不是秘密），真正要保護的是內容；做法與 Node 版先比長度再 `timingSafeEqual` 同一意思。

本專案怎麼用：`ValidatePassword` 裡比 `ADMIN_PASSWORD`。登入是全站唯一「拿未知字串跟秘密比」的地方，別處沒有這種比對需求。

## 健康檢查：`/health` 與 `/health/db`

`/health` 只確認服務活著，不碰資料庫（給平台探測與冷啟動喚醒用，最輕量）；`/health/db` 實際連一次資料庫並量耗時（`durationMs`）。分開的原因：服務活著不代表資料庫連得上，兩支分開才能定位。`durationMs` 用來分辨變慢是 DB 慢還是網路慢。失敗回 500，本文為契約形狀的 `INTERNAL_SERVER_ERROR`（與 Node 版的狀態碼與形狀一致，統一已完成）。

## 簽發 token 的型別鏈

簽一個 HS256 的 token 要經過五步，每一步都是一個型別，不能跳過，也不能自己把字串拼起來：

1. 密鑰字串（`JWT_SECRET`）→ 用 `Encoding.UTF8.GetBytes` 轉成位元組：金鑰本質是二進位資料（簽章算的是位元組，不是字），但設定檔只能存文字，所以平常以字串保管、用時轉回位元組。`UTF8` 只是字元編碼的約定：簽和驗用同一種編碼轉，位元組才會一樣。
2. 位元組 → `SymmetricSecurityKey`（對稱式金鑰：簽和驗用同一把）：「對稱」是相對於「非對稱」（公鑰／私鑰各一把）而言的分類——對稱做法簡單，適合本專案「簽和驗是同一台服務」；非對稱適合「簽的跟驗的是不同單位」。物件化之後，簽與驗都指同一把，框架才能確認 token 是自己發的。
3. 金鑰物件＋`SecurityAlgorithms.HmacSha256` → `SigningCredentials`（簽章設定：把「用哪把鑰匙、用哪種演算法」綁在一起傳給簽章步驟）。
4. 簽章設定＋claims＋`notBefore`／`expires` → `JwtSecurityToken`（token 的物件描述：此時還在記憶體裡，不是字串）。
5. `new JwtSecurityTokenHandler().WriteToken(token)` → `xxx.yyy.zzz` 字串：base64 標頭、payload、簽章三段；這才是回給前端、之後每個請求放在 `Authorization` 標頭裡的東西。

為什麼不自己把三段字串起來：簽章步驟涉及正確的 base64url 編碼與簽章計算，自己組容易漏細節（今天 `Sha256`／`HmacSha256` 用錯常數就是同一類錯誤）。用框架的 API 等於把這些細節交給測過的實作。

## `CreateToken` 逐行對照

（行號以 `Services/AuthService.cs` 為準）

- `IConfiguration` 欄位與建構注入：ASP.NET Core 提供的設定讀取介面（設定從哪來見 `aspnet-core.md`「設定來源」段），由 DI 在啟動時傳入；對照 Node 版就是 `process.env`。
- `_configuration["JWT_SECRET"]` 與空值檢查：讀不到就丟 `InvalidOperationException`（設定缺失是啟動問題，不是使用者錯誤，所以用例外而不是回 401）。
- `Encoding.UTF8.GetBytes(jwtSecret)`：見型別鏈第 1 步。
- `new SymmetricSecurityKey(...)`：見第 2 步。驗證端（`Program.cs` 的 `IssuerSigningKey`）用同一個類別包同一把密鑰，簽與驗才對得上。
- `new SigningCredentials(key, SecurityAlgorithms.HmacSha256)`：見第 3 步。`HmacSha256` 的值就是 `HS256`，與 Node 版 `constants/jwt.ts` 是同一個東西。
- `new Claim("role", "admin")`：claim（payload 的一個鍵值）。簽發端寫什麼名字，驗證端 `RoleClaimType` 就要讀什麼名字，三處（簽發／`RoleClaimType`／`[Authorize(Roles)]`）必須一致。
- `JwtRegisteredClaimNames.Iat`：`Iat` 是常數（值 `iat`，全名 issued at，簽發於），用常數避免手打拼錯（`iat`、`exp`、`nbf` 見縮寫表）。為什麼手動填：Node 版用 `jsonwebtoken.sign()` 時套件會自己塞 `iat`，.NET 的 `JwtSecurityToken` 不會。不填功能上仍可驗（效期靠 `exp`／`nbf`），但兩版 payload 形狀就不同——拿兩張 token 解碼比對、或前端讀 `iat` 顯示時就會看到差異；補上純粹為了契約一致。
- `new DateTimeOffset(now).ToUnixTimeSeconds().ToString()`：JWT 的時間單位是 Unix 秒（從 1970-01-01 起算的秒數），必須先換算。
- `ClaimValueTypes.Integer64`：宣告這個 claim 是整數，序列化時 payload 寫成 `"iat": 1763104000`（數字）。不寫會寫成 `"iat": "1763104000"`（字串）：簽章算的是 payload 的位元組，兩種寫法位元組不同，兩版簽章就對不上了。
- `new JwtSecurityToken(claims:notBefore:expires:signingCredentials:)`：見第 4 步。`notBefore` 是生效時間（現在），框架會把它寫成 payload 的 `nbf`；`expires` 是 `now.AddMinutes(15)`（15 分鐘有效），寫成 `exp`。這兩個時間與 `Program.cs` 的驗證（`ValidateLifetime`、`ClockSkew = Zero`）成對：簽的時候承諾 15 分鐘，驗的時候用同一把尺量。
- `new JwtSecurityTokenHandler().WriteToken(token)`：見第 5 步。

## 型別速查表

| 型別 | 所在的命名空間（來自哪個套件） | 做什麼 | 本專案為何用它 |
|---|---|---|---|
| `IConfiguration` | `Microsoft.Extensions.Configuration`（共享框架自帶，不用裝） | 讀取設定值的介面（概念見 [csharp-basics.md](./csharp-basics.md)「介面」節） | 讀 `JWT_SECRET`、`ADMIN_PASSWORD`、連線字串 |
| `SymmetricSecurityKey` | `Microsoft.IdentityModel.Tokens`（`System.IdentityModel.Tokens.Jwt`） | 對稱式金鑰物件 | 包裝 `JWT_SECRET`，簽與驗共用 |
| `SigningCredentials` | 同上 | 金鑰＋演算法的簽章設定 | 告訴簽章步驟用 HS256 |
| `SecurityAlgorithms.HmacSha256` | 同上 | 簽章演算法常數（值 `HS256`） | 與 Node 版同一種演算法 |
| `Claim` | `System.Security.Claims`（BCL，不用裝） | payload 的一個鍵值 | 攜帶 `role: admin` |
| `JwtRegisteredClaimNames` | `System.IdentityModel.Tokens.Jwt` | 標準 claim 名的常數表 | `Iat` 即 `iat` |
| `ClaimValueTypes` | `System.Security.Claims`（BCL） | claim 值的型別常數表 | `Integer64` 宣告整數 |
| `JwtSecurityToken` | `System.IdentityModel.Tokens.Jwt` | token 的物件描述 | 組出 claims＋效期＋簽章 |
| `JwtSecurityTokenHandler` | 同上 | token 的序列化工具 | `WriteToken` 產出三段字串 |
| `CryptographicOperations` | `System.Security.Cryptography`（BCL） | 密碼學工具 | `FixedTimeEquals` 防時序攻擊 |
| `JwtBearerDefaults` | `Microsoft.AspNetCore.Authentication.JwtBearer`（要裝） | 認證 scheme 名稱常數 | `AuthenticationScheme` 即 `Bearer` |
| `TokenValidationParameters` | `Microsoft.IdentityModel.Tokens` | 「怎麼驗 token」的設定物件 | 簽名／效期／演算法白名單／角色來源 |
| `Stopwatch` | `System.Diagnostics`（BCL） | 高精度計時 | `/health/db` 量 `durationMs` |

## `JwtSecurityToken` 與 `JsonWebTokenHandler`

.NET 有兩代 JWT API：`JwtSecurityToken`＋`JwtSecurityTokenHandler`（舊，範例與文件最多）、`JsonWebToken`＋`JsonWebTokenHandler`（新，.NET 6 之後加入，效能較好、記憶體用量较少）。兩代都能簽與驗 HS256 token，產出的字串可以互通。

這裡選舊的理由：與 Node 版 `jsonwebtoken` 的結構對照最直觀（`sign(payload, secret)` 約等於 `new JwtSecurityToken(...)`＋`WriteToken`），且所需套件已在相依中；新 API 的寫法與觀念（例如 claim 的處理方式）略有不同，日後若有效能需求再換，換時要注意 `MapInboundClaims` 的對應行為，並用測試確認兩版 token 互通。
