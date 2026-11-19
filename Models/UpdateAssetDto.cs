
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace Web3D.Backend.Models
{
    // PUT /assets/{id} 的請求本文。Config 用 JsonElement 承接（結構隨資產類型變動，不拆成欄位），
    // 內容驗證由 JsonObjectConfig 負責；缺欄位補值與多餘鍵過濾在 Controller 呼叫 NormalizeConfig。
    public class UpdateAssetDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [JsonObjectConfig]
        public JsonElement Config { get; set; }

        // 整理 config：補上缺的 model，並只留下契約定義的三個鍵。
        // Node 版在驗證器裡重建物件，多餘的鍵本來就不會被寫入；這裡做同一件事，
        // 讓 DB 內容永遠等於契約（PUT 是整筆取代，不保留無法驗證的欄位）。
        public void NormalizeConfig()
        {
            var config = JsonNode.Parse(Config.GetRawText())!.AsObject();

            if (config["model"] is null)
            {
                config["model"] = new JsonObject { ["materialColorOverrides"] = new JsonObject() };
            }

            // DeepClone：一個 JsonNode 只能有一個父節點，直接指派會拋例外。
            Config = JsonSerializer.SerializeToElement(new JsonObject
            {
                ["camera"] = config["camera"]!.DeepClone(),
                ["cube"] = config["cube"]!.DeepClone(),
                ["model"] = config["model"]!.DeepClone()
            });
        }
    }
    // Config 內容驗證。內建 DataAnnotations 只能驗單一值（必填、長度），巢狀 JSON 的形狀表達不出來，
    // 因此逐層檢查；任一層不合都回 400 INVALID_REQUEST_BODY（不指出哪個欄位，與 Node 版相同）。
    public class JsonObjectConfigAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is not JsonElement config || config.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return IsValidCamera(config) && IsValidCube(config) && IsValidModel(config);
        }
        
        // 檢查 camera：position 為長度 3 的有限數字陣列、fov 為有限數字。
        private static bool IsValidCamera(JsonElement config)
        {
            if (!config.TryGetProperty("camera", out var camera) ||
                camera.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            // 驗證 camera 內的 position 是長度 3 的有限數字陣列
            if (!camera.TryGetProperty("position", out var position) ||
                position.ValueKind != JsonValueKind.Array ||
                position.GetArrayLength() != 3)
            {
                return false;
            }
            // 其餘位置與 fov 必須是有限數字，與上段同理
            foreach (var element in position.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Number ||
                    !element.TryGetDouble(out var number) ||
                    !double.IsFinite(number))
                {
                    return false;
                }
            }
            if (!camera.TryGetProperty("fov", out var fov) ||
                fov.ValueKind != JsonValueKind.Number ||
                !fov.TryGetDouble(out var fovNumber) ||
                !double.IsFinite(fovNumber))
            {
                return false;
            }

            return true;
        }
        // 檢查 cube：color 為非空白字串。
        private static bool IsValidCube(JsonElement config)
        {
            if (!config.TryGetProperty("cube", out var cube) ||
                cube.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            // 先確認型別再取值：`ValueKind`（JsonElement 記錄的「這段 JSON 是什麼種類」：物件／陣列／字串／數字等）不是 String 時，
            // 呼叫 `GetString()`（取出字串內容的方法）會拋例外，驗證失敗應該回 400，不該讓例外變成 500。
            if (!cube.TryGetProperty("color", out var color) ||
                color.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(color.GetString()))
            {
                return false;
            }

            return true;
        }
        // 檢查 model（選填）：materialColorOverrides 的值須為 #RRGGBB。
        private static bool IsValidModel(JsonElement config)
        {
            // model 為選填：舊資料沒有這個欄位，缺就放行，由 NormalizeConfig 補預設；
            // 出現時嚴格驗，明寫 null 視為不合法（與 Node 版 isAssetConfig 相同）
            if (!config.TryGetProperty("model", out var model))
            {
                return true;
            }
            
            if (model.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!model.TryGetProperty("materialColorOverrides", out var overrides) ||
                overrides.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            foreach(var property in overrides.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    return false;
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return false;
                }
                var color = property.Value.GetString();

                // 格式須符合 #RRGGBB
                if (color is null || !Regex.IsMatch(color, @"^#[0-9a-fA-F]{6}$"))
                {
                    return false;
                }
            }

            return true;
        }
    }
}