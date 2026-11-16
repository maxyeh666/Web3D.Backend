using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Web3D.Backend.Constants;

namespace Web3D.Backend.Controllers
{
    // GET /models/{modelKey}：依 modelKey 回傳模型檔。
    // 白名單查表（只認登記過的 key），不把整個資料夾設成靜態檔：目錄穿越不需另外擋，
    // 且 modelKey 與檔名解耦——換檔或加模型只改下面這張表。
    [ApiController]
    [Route("models")]
    public class ModelController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        // key 是給前端的契約（資料庫 assets.model_key 存的就是它），value 是資料夾內的檔名。
        // Ordinal：modelKey 大小寫敏感，不看地區語言規則（例如土耳其語的 `i` 大寫不是 `I`；契約字串不用這套）。
        private static readonly Dictionary<string, string> ModelFiles =
            new(StringComparer.Ordinal)
            {
                ["materials-variants-shoe"] = "MaterialsVariantsShoe.glb"
            };

        public ModelController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpGet("{modelKey}")]
        public IActionResult GetModel(string modelKey)
        {
            // 未登記的 key 一律 404：不列可用清單，也不回 500。
            if (!ModelFiles.TryGetValue(modelKey, out var fileName))
            {
                return NotFound(ApiErrors.ModelNotFound);
            }

            // ContentRootPath（應用程式根目錄）：dotnet run 是專案目錄，
            // 直接執行 bin 內或發佈後則是輸出目錄；csproj 的複製規則讓兩邊都找得到檔案。
            var filePath = Path.Combine(_environment.ContentRootPath, "ModelFiles", fileName);

            // 登記了卻找不到檔案，代表部署時沒把 ModelFiles 帶上去；對外仍回 404，不洩漏伺服器路徑。
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(ApiErrors.ModelNotFound);
            }

            // 標頭要在回應開始送出前設定；進入 PhysicalFile 串流後再寫就來不及。
            // 模型檔約 7.8MB 且內容固定：max-age=3600 讓瀏覽器快取一小時，
            // 過期後靠 `Last-Modified`（檔案最後修改時間）做條件請求：沒變就回 304（用手上那份），不必重抓。
            Response.Headers["Cache-Control"] = "public, max-age=3600";

            // enableRangeProcessing：允許 Range 請求（分段下載，伺服器會多回 `Accept-Ranges` 宣告支援），與 Node 版 `send`（`res.sendFile` 底層實際送檔的函式）的行為一致。
            return PhysicalFile(filePath, "model/gltf-binary", enableRangeProcessing: true);
        }
    }
}