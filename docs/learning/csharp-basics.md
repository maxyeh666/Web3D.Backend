# C# 語言基礎與 TypeScript 對照

記「只寫過 Node / TypeScript 的人會誤解或想問」的 C# 概念。實作當天的問答仍留 `LEARNING.md` 的日期段，這裡只收可反覆查閱的說明。

先看全貌（一次請求走過哪些地方）：[架構總覽](./architecture.md)。

## 型別、可空性與型別推斷

型別關鍵字是小寫（`string`、`int`、`bool`）；`string?` 尾端的 `?` 表示「可為 null」（相當於 TS 的 `string | null`），由專案檔的 `<Nullable>enable</Nullable>` 開啟檢查，編譯器會擋下未檢查就使用的可空值。

`var` 是型別推斷（編譯器從等號右邊推斷型別，相當於 TS 省略型別註記讓編譯器推斷），不是動態型別，之後不能換成別的型別。

## 命名慣例、字串插值與非同步

型別與方法用 PascalCase（`AssetService`、`GetAsset`），區域變數與參數用 camelCase。字串插值 `$"Hello {name}"` 相當於 TS 的樣板字串。

`async` / `await` 寫法與 TS 幾乎相同，只是非同步方法的回傳型別寫成 `Task<T>`（表示一個非同步工作，相當於 `Promise<T>`）。集合方面，`List<T>` 相當於 `T[]`，`Dictionary<string, string>` 相當於 `Record<string, string>`。

## using 區塊與資源釋放（TS 沒有）

`using` 區塊保證離開時自動釋放資源（例如資料庫連線「用完即歸還」），相當於 Node 版 `migrate.ts` 用 `try` / `finally` 歸還連線的那段邏輯。

## 屬性（Property，TS 沒有）

`public string Name { get; set; }` 看起來像欄位，實際是帶讀寫方法的成員，EF Core 靠它對應資料表欄位。

## 例外處理

`throw new ...Exception(...)` 相當於 `throw new Error(...)`；C# 習慣用多個 `catch` 按例外型別分流，相當於 Node 版用一個 errorHandler 按 `code` 分流，兩種做法在本專案效果相同。

## null 檢查：`is null` 與 `== null`

兩者都能判斷「沒有值」，但在 C# 裡語意不同。`==` 是運算子（operator：型別可以自己定義的符號行為），型別可以覆寫它；`is null` 是模式比對語法，由編譯器直接產生「比較參考是否為 null」的指令，不會經過任何自訂運算子。

| 比較項目 | `is null` | `== null` |
| --- | --- | --- |
| 是否受型別覆寫影響 | 否，永遠是參考比較 | 是，執行的是該型別定義的運算子 |
| 典型差異案例 | 一律判斷「沒有物件」 | `string` 覆寫成內容比較，`"abc" == "abc"` 為 true；特殊型別可能與 null 比較時拋例外 |
| 效能 | 一次比較指令，無方法呼叫 | 若被覆寫即為一次方法呼叫 |
| EF Core 查詢 | 可翻譯成 SQL 的 `IS NULL` | 同樣可翻譯成 `IS NULL`；但若型別覆寫了 `==`，運算式樹記錄的是自訂運算子，EF 翻譯不出來會拋 `InvalidOperationException` |
| 對結構型別 | 僅適用可為 null 的型別（參考型別、`int?`） | 型別必須有定義 `==`，否則編譯失敗 |
| 可讀性 | 與 `is not null`、`is { } value` 等模式比對語法一致 | 與 `!=` 對稱 |

專案內的慣例是一律使用 `is null` 與 `is not null`，理由是行為不受型別實作影響，且與 C# 7 之後的模式比對語法一致。`object.ReferenceEquals(x, null)` 效果相同但冗長，不使用。

跨語言對照：TypeScript 慣用 `x == null`（寬鬆比較）一次涵蓋 `null` 與 `undefined` 兩種空值；C# 沒有 `undefined`，空值只有 `null` 一種，因此不需要這種寫法。C# 對應 `?.`（空值傳播：遇到 null 就整段回傳 null）與 `??`（空值合併：左邊是 null 時取右邊）的用法與 TS 相同。

## out 參數與 TryParse 模式

`long.TryParse(id, out var assetId)` 用 out 參數（輸出參數：方法除了回傳值以外，額外把結果寫進呼叫端指定的變數）帶出解析結果，失敗時不拋例外。它預設允許數字前後有空白，也允許正負號，因此 `+1`、` 1` 都會解析成 1。

要完全符合「只接受以 1-9 開頭的正整數字串」的規則，需自行逐字元檢查或先比對正規表達式。
