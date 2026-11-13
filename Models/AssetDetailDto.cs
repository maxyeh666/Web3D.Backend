using System.Text.Json;

namespace Web3D.Backend.Models
{
    public class AssetDetailDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public JsonElement Config { get; set; }
        public string UpdatedAt { get; set; } = string.Empty;
        public string ModelKey { get; set; } = string.Empty;
    }
}