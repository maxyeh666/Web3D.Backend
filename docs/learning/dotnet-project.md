# .NET 專案設定：csproj 的角色與建置行為

收「專案設定檔怎麼影響建置」：`Web3D.Backend.csproj` 管什麼、指令與目錄和 Node 世界的對照、檔案分類與複製規則、沒有 `scripts` 該怎麼辦。
選型問題（該用哪個套件、怎麼查）是另一件事，見 [.NET 套件](./dotnet-packages.md)。
將來 `appsettings.json`（執行期設定值）、`launchSettings.json`（本機啟動參數）、`Directory.Build.props`（多專案共用的版本設定）這類設定也要了解時，收進同一篇的章節，不另開檔。

## csproj 是什麼：與 package.json 的職責對照

`package.json`（Node 專案的設定：寫依賴、腳本與專案資訊）、`tsconfig.json`（TypeScript 編譯設定）、bundler 設定（如 `vite.config`，決定打包方式）是三個檔案各管一件事；
.NET 把這三類職責收進同一個專案檔，由 MSBuild（.NET 的建置引擎：讀 csproj 決定要跑哪些步驟）解讀：

| package.json 側的職責 | csproj 側 | 說明 |
| --- | --- | --- |
| `name` / `version` | 無欄位，檔名即專案名 | .NET 跑起來不需要這兩個欄位 |
| `dependencies` | `<PackageReference>` | 本專案 5 個套件寫在 `<ItemGroup>` 內；`Version="10.0.14"` 是「至少這個版本」，習慣上直接依賴就用這個版，傳遞依賴要求更高時才往上解析 |
| `type` / `engines` | `<TargetFramework>net10.0</TargetFramework>` | 用哪一版 .NET 編譯與執行 |
| `scripts` | 無對應 | `dotnet build` / `dotnet run` 是工具內建動詞，不是自訂腳本 |
| `tsconfig.json` 的編譯器開關 | `<Nullable>` / `<ImplicitUsings>` | 編譯器行為 |
| bundler 的產物規則 | `<None Update=...>` 複製規則 | 本篇 §複製規則，也是 11-16 加的那一段 |

定位一句話：Node 是多檔各管一件事，.NET 是一檔管全部。所以 csproj 短不代表功能少——大部分規則由 SDK 給預設值，只在要**改預設**時才寫進來。

## 指令與目錄的對照

| Node | .NET | 備註 |
| --- | --- | --- |
| `npm install` | `dotnet restore` | 從套件來源下載依賴；`build` / `run` 會自動先跑這步 |
| `npm install axios` | `dotnet add package Xxx` | 直接改 csproj |
| `npm run build` | `dotnet build` | |
| `npm start` | `dotnet run` | 內部先 build 再啟動 |
| `dist/` | `bin/` | 建置輸出 |
| `node_modules/`（「已安裝」的概念） | `obj/` 內的還原產物 | 位置與形式不同，都是不進版控的中間產物 |
| `package-lock.json` | `obj/project.assets.json` | 實際解析出的版本，同樣不進版控 |

鎖版機制：NuGet 有 `packages.lock.json` 開關（`RestorePackagesWithLockFile`，開了會產生鎖定檔並進版控），本專案沒開，所以版本來源以 csproj 為準。

## 檔案分類：None、Compile，以及 Update 還是 Include

SDK 預設會把專案目錄下所有檔案自動收進來：`.cs` 歸 `Compile`（參與編譯），`.resx` 歸 `EmbeddedResource`（內嵌資源，打包進 dll），其餘歸 `None`（不參與編譯的檔案）。這由 `EnableDefaultNoneItems` 等開關控制。

`ModelFiles` 下的 glb 與 `.md` 已經自動在 `None` 名單裡（用 `dotnet msbuild -getItem:None` 查過，兩檔都在）。所以改它屬性用 `Update`（修改既有項目）；用 `Include`（新增項目）會變成重複項目而報錯（`NETSDK1022`）。這是第一次手寫這類規則最常踩的點。

查法：

```powershell
dotnet msbuild Web3D.Backend.csproj -getItem:None | Select-String -Pattern 'ModelFiles'
```

## 複製規則：CopyToOutputDirectory 與 CopyToPublishDirectory

2026-11-16 為 `ModelFiles/` 加的規則：

```xml
<None Update="ModelFiles\**\*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</None>
```

這是「檔案屬性」，不是指令：標註「這個檔案要複製到哪、用什麼策略」，MSBuild 自己決定動作。

- `PreserveNewest` 是增量：只在來源較新或目的不存在時複製。每次不問都覆蓋要用 `Always`。
- 兩個屬性走各自獨立的流程：`CopyToOutputDirectory` 管 `bin/`，生效於 SDK `Microsoft.Common.CurrentVersion.targets` 的 `CopyFilesToOutputDirectory`（L4895）→ `_CopyOutOfDateSourceItemsToOutputDirectory`（L5137，定義在 L5446–5450，用 `Inputs`／`Outputs` 做增量判斷，L5453 的註解明說傳進來的只剩時間較新的）；`CopyToPublishDirectory` 管 `dotnet publish` 的產物，生效於 `Microsoft.NET.Sdk/targets/Microsoft.NET.Publish.targets` 的 `CopyFilesToPublishDirectory`（L267）。以上行號以本機 SDK 10.0.404 為準。
- 所以三種指令行為不同：`build` 經過前者（沒變就跳過）；`run` 內部先 build；`publish` 走後者。實際案例見[模型檔託管](./static-files.md)。
- 只寫前者不寫後者：本機跑得起來、部署後 404。

## 沒有 scripts：要掛建置步驟怎麼寫

真要「建置時跑一段指令」，.NET 的寫法是加一個 MSBuild target：

```xml
<Target Name="MyPostBuild" AfterTargets="Build">
  <Exec Command="echo done" />
</Target>
```

`AfterTargets`（掛在某個 target 之後執行）加 `Exec`（執行命令列的 task，就是這段）。實務上多數情況不需要，交給 CI 步驟或 `dotnet tool`；本專案目前沒有這類需求，先不寫。

## 與其他主題的關係

- [.NET 套件](./dotnet-packages.md)：套件選型與安裝。
- [模型檔託管](./static-files.md)：`ModelFiles` 白名單的實際案例。路徑用 `ContentRootPath` 組、檔案用本篇的複製規則跟著走，兩段合起來才是完整機制。

## 驗證方式

建置設定改完，先看設定有沒有被讀到，再看檔案有沒有跟著走：

- `dotnet msbuild Web3D.Backend.csproj -getItem:None | Select-String -Pattern 'ModelFiles'`
- `dotnet build Web3D.Backend` 後，`bin/Debug/net10.0/ModelFiles/` 內有 `MaterialsVariantsShoe.glb`（約 7.8MB）與 `ATTRIBUTION.md`
