
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Web3D.Backend.Models
{
    public class UpdateAssetDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [JsonObjectOnly]
        public JsonElement Config { get; set; }
    }
    // 自訂驗證屬性，確保 Config 屬性是 JSON 物件
    public class JsonObjectOnlyAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            // 檢查 value 是否為 JsonElement，並且 ValueKind 是否為 Object
            // element 是 JsonElement 的別名，ValueKind 是 JsonElement 的屬性，表示 JSON 值的類型
            return value is JsonElement element
            && element.ValueKind == JsonValueKind.Object;
        }
    }
}