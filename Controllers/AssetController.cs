using Microsoft.AspNetCore.Mvc;
using Web3D.Backend.Services;
using Web3D.Backend.Models;

namespace Web3D.Backend.Controllers
{
    // AssetController 負責處理與資產相關的 HTTP 請求，並使用 AssetService 進行資料操作
    // ApiController 屬性表示這是一個 API 控制器，會自動處理模型驗證錯誤，並返回適當的 HTTP 狀態碼
    [ApiController]
    [Route("assets")]
    public class AssetController : ControllerBase
    {
        private readonly AssetService _assetService;

        public AssetController(AssetService assetService)
        {
            _assetService = assetService;
        }
        // GET /assets
        // 非同步方法同ts用法 async/await，回傳 Task<IActionResult>
        // IActionResult 是 ASP.NET Core 中的介面，表示一個 HTTP 回應，方便前端使用
        [HttpGet]
        public async Task<IActionResult> GetAllAssets()
        {
            var assets = await _assetService.GetAllAssetsAsync();
            // Ok() 方法會返回一個 200 OK 的 HTTP 回應，並將 assets 作為 JSON 格式的回應內容
            return Ok(assets);
        }

        // GET /assets/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAssetDetail(string id)
        {
            // TODO: 暫時驗證，之後error handling要統一處理，這裡先簡單判斷id是否為正整數
            if (!long.TryParse(id, out var assetId) || assetId <= 0)
            {
                return BadRequest(new { error = "Invalid asset ID format." });
            }
            var asset = await _assetService.GetAssetDetailByIdAsync(assetId);
            if (asset is null)
            {
                return NotFound();
            }
            return Ok(asset);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAsset(string id, UpdateAssetDto updateAssetDto)
        {
            // TODO: 暫時驗證，之後error handling要統一處理，這裡先簡單判斷id是否為正整數
            if (!long.TryParse(id, out var assetId) || assetId <= 0)
            {
                return BadRequest(new { error = "Invalid asset ID format." });
            }
            var asset = await _assetService.UpdateAssetAsync(assetId, updateAssetDto);
            if (asset is null)
            {
                return NotFound();
            }
            return Ok(asset);
        }
    }
}