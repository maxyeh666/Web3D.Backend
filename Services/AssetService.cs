using Microsoft.EntityFrameworkCore;
using Web3D.Backend.Data;
using Web3D.Backend.Models;
using System.Text.Json;
using System.Globalization;

namespace Web3D.Backend.Services
{
    public class AssetService
    {
        private readonly AppDbContext _dbContext;

        public AssetService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        // 查資產摘要。整段 LINQ 在 ToListAsync 之前都不打資料庫（叫延遲執行）；
        // 該呼叫時，EF Core 把整段翻譯成一條 SELECT 送出去。Id.ToString() 也會被翻譯成 SQL 的 CAST(id AS text)。
        public async Task<List<AssetSummaryDto>> GetAllAssetsAsync()
        {
            // Select 只取兩個欄位（不整筆查出再丟）。
            return await _dbContext.Assets
                .OrderBy(asset => asset.Id)
                .Select(asset => new AssetSummaryDto
                {
                    Id = asset.Id.ToString(),
                    Name = asset.Name
                })
                .ToListAsync();
        }
        // 根據資產 ID 查詳細資訊；找不到回 null。
        public async Task<AssetDetailDto?> GetAssetDetailByIdAsync(long id)
        {
            var asset = await _dbContext.Assets.FirstOrDefaultAsync(asset => asset.Id == id);
            if (asset is null) return null;

            return ToDetailDto(asset);
        }
        // 根據資產 ID 更新資訊。更新後回完整資料（給 PUT 回 200 用），找不到回 null。
        public async Task<AssetDetailDto?> UpdateAssetAsync(long id, UpdateAssetDto updateAssetDto)
        {
            var asset = await _dbContext.Assets.FirstOrDefaultAsync(asset => asset.Id == id);
            if (asset is null) return null;

            asset.Name = updateAssetDto.Name;
            // 將 JsonElement 轉換為 string 寫入 Config：傳來的已是 NormalizeConfig 整理後的三鍵，
            // 與 Node 版驗證器重建物件的寫入結果一致。
            asset.Config = updateAssetDto.Config.GetRawText();
            // updated_at 取應用程式時鐘；Node 版用資料庫的 CURRENT_TIMESTAMP。
            // 單一實例下差異可忽略（見 CHANGELOG 2026-11-11 則已知限制）。
            asset.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync();

            return ToDetailDto(asset);
        }
        // Asset 轉 AssetDetailDto。
        private static AssetDetailDto ToDetailDto(Asset asset)
        {
            // Entity 的 Config 屬性是 string，AssetDetailDto 的 Config 屬性是 JsonElement
            // 所以要先把 string 轉成 JsonDocument，再取 RootElement。
            using var jsonDoc = JsonDocument.Parse(asset.Config);
            return new AssetDetailDto
            {
                Id = asset.Id.ToString(),
                Name = asset.Name,
                Config = jsonDoc.RootElement.Clone(),
                UpdatedAt = asset.UpdatedAt
                    .ToUniversalTime()
                    .ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                ModelKey = asset.ModelKey
            };
        }
    }
}