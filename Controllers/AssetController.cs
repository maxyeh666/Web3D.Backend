using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Web3D.Backend.Services;
using Web3D.Backend.Models;
using Web3D.Backend.Constants;
using System.Text.RegularExpressions;

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
            if (!TryParseAssetId(id, out var assetId))
            {
                return BadRequest(ApiErrors.InvalidAssetId);
            }
            var asset = await _assetService.GetAssetDetailByIdAsync(assetId);
            if (asset is null)
            {
                return NotFound(ApiErrors.AssetNotFound);
            }
            return Ok(asset);
        }

        [HttpPut("{id}")]
        // 只有寫入要管理員：讀取（GET）公開；更新必須帶合法 token，且 token 的 role 為 admin。
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateAsset(string id, UpdateAssetDto updateAssetDto)
        {
            if (!TryParseAssetId(id, out var assetId))
            {
                return BadRequest(ApiErrors.InvalidAssetId);
            }
            // 補上預設值
            updateAssetDto.ApplyDefault();

            var asset = await _assetService.UpdateAssetAsync(assetId, updateAssetDto);
            if (asset is null)
            {
                return NotFound(ApiErrors.AssetNotFound);
            }
            return Ok(asset);
        }
        // 契約：1-9 開頭、其餘為數字（與 Node 版 ^[1-9]\d*$ 一致）；
        // 再以 TryParse 擋掉超過 BIGINT 範圍的長數字（這點 C# 版刻意比 Node 版嚴，避免送出必定失敗的查詢）。
        private static bool TryParseAssetId(string? id, out long assetId)
        {
            assetId = 0;
            return id is not null
                && Regex.IsMatch(id, @"^[1-9]\d*$")
                && long.TryParse(id, out assetId);
        }
    }
}