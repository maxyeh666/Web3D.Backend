namespace Web3D.Backend.Models
{
    public class Asset
    {
        public long Id { get; set; }
        // string.Empty 避免 null reference exception
        public string Name { get; set; } = string.Empty;
        // 暫時使用 string 儲存保留未知參數，避免限制之後擴充
        public string Config { get; set; } = "{}";
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string ModelKey { get; set; } = string.Empty;
    }
}