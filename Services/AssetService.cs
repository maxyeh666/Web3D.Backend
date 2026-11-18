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
        // 非同步方法同ts用法 async/await，回傳 Task<List<AssetSummaryDto>>，方便前端使用
        // Task<List<AssetSummaryDto>> 與ts的 Promise<List<AssetSummaryDto>> 類似，都是非同步回傳結果
        // DB查詢取得所有資產的摘要資訊，並轉換為 AssetSummaryDto 的列表
        public async Task<List<AssetSummaryDto>> GetAllAssetsAsync()
        {
            // 使用 LINQ 查詢資料庫，將 Asset 轉換為 AssetSummaryDto
            // LINQ 查詢語法與 SQL 類似，使用 OrderBy 排序，使用 Select 投影，最後使用 ToListAsync 轉換為 List
            // Select 只取需要的兩個欄位，而不是把整筆 Entity 查出來再丟掉。
            // 這整段 LINQ 在 ToListAsync 之前都不會真的打資料庫（叫延遲執行）；
            // ToListAsync 那一刻，EF Core 才把整段翻譯成一條 SELECT 語句送出去。
            // Id.ToString() 也會被翻譯成 SQL 的 CAST(id AS text)。
            return await _dbContext.Assets
                .OrderBy(asset => asset.Id)
                .Select(asset => new AssetSummaryDto
                {
                    Id = asset.Id.ToString(),
                    Name = asset.Name
                })
                .ToListAsync();
        }
        // 根據資產 ID 查詢資產詳細資訊，並轉換為 AssetDetailDto
        // 如果找不到資產，回傳 null
        public async Task<AssetDetailDto?> GetAssetDetailByIdAsync(long id)
        {
            // 使用 LINQ 查詢資料庫，根據 Id 查找 Asset
            // FirstOrDefaultAsync 會回傳第一筆符合條件的資料，如果沒有符合條件的資料，則回傳 null
            var asset = await _dbContext.Assets.FirstOrDefaultAsync(asset => asset.Id == id);
            if (asset is null) return null;

            return ToDetailDto(asset);
        }
        // 根據資產 ID 更新資產資訊。更新後回傳完整資料（給 PUT 回 200 用），
        // 找不到資產回 null。
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
            // SaveChangesAsync() 會將變更保存到資料庫，並回傳受影響的資料列數量
            await _dbContext.SaveChangesAsync();

            return ToDetailDto(asset);
        }
        // 將 Asset 轉換為 AssetDetailDto 的靜態方法
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