# EF Core：查詢翻譯、型別對應與預設值

記「資料表怎麼變成 C# 物件」的概念。實作當天的問答仍留 `LEARNING.md` 的日期段，這裡只收可反覆查閱的說明。

先看全貌（一次請求走過哪些地方）：[架構總覽](./architecture.md)。

## 查詢翻譯與延遲執行

LINQ 方法（`Where`、`OrderBy`、`Select`）只是組裝查詢描述、不執行，叫延遲執行（先描述要什麼資料，真正需要時才執行；JS 的 `map` 與 `filter` 是立即執行，這裡相反）。

`ToListAsync` 那一刻，EF Core 才把整段翻譯成一條 SQL 送出去。`Id.ToString` 能翻譯，是因為 Npgsql 的 provider 內建了一批 C# 方法對 SQL 寫法的對應（`long` 的 `ToString` 對應 `CAST(id AS text)`）；能翻譯的在資料庫端執行、不能翻譯的會拋錯。

## 變更追蹤與 AsNoTracking

變更追蹤（change tracking：EF 把讀出的物件記住，`SaveChangesAsync` 只送改過的欄位）只有在查出 Entity 並打算修改時才有用，代價是每個被追蹤的物件都保留一份原始值快照。

本專案三種查詢的對照：`GetAllAssetsAsync` 用 `Select` 投影成 DTO（回傳的不是 Entity，本來就不追蹤）；`GetAssetDetailByIdAsync` 用 `FirstOrDefaultAsync` 查出 Entity 但只讀不寫（追蹤是多餘成本，是 `AsNoTracking()` 的適用位置）；`UpdateAssetAsync` 查出 Entity、改欄位、再 `SaveChangesAsync`（必須追蹤）。

目前程式尚未使用 `AsNoTracking`，屬可最佳化項目，不是錯誤。

## PostgreSQL、C#、JSON 三層的型別對照

同一筆資料的型別對照點有四處：

- `id` 是 BIGINT（64 位元整數）到 C# 用 `long` 接（64 位元整數型別）到 JSON 給字串，因為 BIGINT 會超過 JS 的安全整數範圍（2 的 53 次方減 1，超過後數字失真），做法與 Node 版 pg 驅動預設一致
- `config` 是 jsonb（PostgreSQL 的一種欄位型別，可以直接把 JSON 物件存進資料庫）到 C# 先用 `string` 接整包，之後需要強型別再換 `JsonDocument`
- `updated_at` 是 timestamptz（含時區的時間戳）到 C# 用 `DateTimeOffset` 接
- 自動編號是 `GENERATED ALWAYS AS IDENTITY` 到 C# 用 `UseIdentityAlwaysColumn` 對應，新增時不要自己塞 Id（塞了會被忽略）

## 欄位預設值：HasDefaultValueSql 與 ValueGeneratedOnAdd

兩者都在描述「這個欄位的值由誰產生」。`HasDefaultValueSql("CURRENT_TIMESTAMP")` 告訴 EF Core 這一欄在資料庫有預設值，並把該屬性標記為「新增時由資料庫產生」；`ValueGeneratedOnAdd()` 是再明確宣告一次。新增時若程式沒有指定值，EF 就不送這個欄位、由資料庫補上，並在 INSERT 之後讀回。

## 連線字串、逾時與連線池（Npgsql）

三個設定分屬不同層：建立連線的逾時（`Timeout`）與池的行為（`MaxPoolSize`、`MinPoolSize`、`ConnectionIdleLifetime`）是連線字串的一部分，本專案用 `NpgsqlConnectionStringBuilder` 在程式裡覆寫——不要求各環境的連線字串自帶參數，本機與部署行為一致。SQL 執行逾時（`CommandTimeout`）是 EF 的選項，跟著 `DbContext` 走，對每個 `DbCommand`（送往資料庫的單一指令）生效。

逾時分客戶端與伺服器端，不要混用：Npgsql 的 `Timeout` 是客戶端等連線建立的上限（本專案 15 秒，因為免費雲端資料庫閒置喚醒常超過 5 秒）；`CommandTimeout` 是客戶端等查詢回來的上限（本專案 5 秒）。伺服器端另有 `statement_timeout`（資料庫自己中斷查詢，本專案刻意不用——兩層都設容易互鬥，且 `/health/db` 的 `SELECT 1` 已直接行使 `CommandTimeout`）。

池的回收節奏容易高估：`ConnectionIdleLifetime` 只在總連線數超過 `MinPoolSize`（本專案沒設，即預設 0）時才回收，且搭 `ConnectionPruningInterval`（清理間隔，預設 10 秒）分批關閉，實際約 30～40 秒才關掉——與 Node 版 `pg` 的 `idleTimeoutMillis: 30_000` 只是近似，不是相等。

與 Node 版的對照：`connectionTimeoutMillis` 對 `Timeout`（同為 15 秒）、`max` 對 `MaxPoolSize`（同為 5）、`idleTimeoutMillis` 對 `ConnectionIdleLifetime`（同為 30，但回收節奏只是近似）。
