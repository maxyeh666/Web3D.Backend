namespace Web3D.Backend.Models
{
    // DTO（Data Transfer Object，資料傳輸物件）：只給前端看的形狀。
    // 為什麼不直接回傳 Entity：Entity 的形狀是給資料庫看的（有哪些欄位），DTO 的形狀是給呼叫端看的（需要哪些欄位）。
    // 兩邊不一樣時（例如 Entity 有密碼、或前端不需要整包），就用 DTO 隔開，資料庫長什麼樣就不會直接影響前端。
    public class AssetSummaryDto
    {
        // 格式同node後端的assetSummaryDto，方便前端使用
        // id 用 string：對應資料庫的 BIGINT。BIGINT 會超過 JS 的安全整數範圍（2 的 53 次方 - 1），
        // 超過之後數字會失真，所以 JSON 傳給前端時一律用字串，跟 Node.js 版的 pg 驅動預設行為一致。
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

    }
}