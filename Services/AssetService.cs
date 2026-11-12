using Microsoft.EntityFrameworkCore;
using Web3D.Backend.Data;
using Web3D.Backend.Models;

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
        public async Task<List<AssetSummaryDto>> GetAllAssetsAsync()
        {
            // 使用 LINQ 查詢資料庫，將 Asset 轉換為 AssetSummaryDto
            // LINQ 查詢語法與 SQL 類似，使用 OrderBy 排序，使用 Select 投影，最後使用 ToListAsync 轉換為 List
            // Select 只取需要的兩個欄位，而不是把整筆 Entity 查出來再丟掉。
            // 這整段 LINQ 在 ToListAsync 之前都不會真的打資料庫（叫延遲執行）；
            // ToListAsync 那一刻，EF Core 才把整段翻譯成一條 SELECT 語句送出去。
            // Id.ToString() 也會被翻譯成 SQL 的 CAST(id AS text)，今天已實測驗證過、不用先撈出來再轉字串。
            return await _dbContext.Assets
                .OrderBy(asset => asset.Id)
                .Select(asset => new AssetSummaryDto
                {
                    Id = asset.Id.ToString(),
                    Name = asset.Name
                })
                .ToListAsync();
        }
    }
}