
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace Web3D.Backend.Models
{
    // PUT /assets/{id} 的請求本文。Config 用 JsonElement 承接（結構隨資產類型變動，不拆成欄位），
    // 內容驗證由 JsonObjectConfig 負責；缺欄位補值在 Controller 呼叫 ApplyDefault。
    public class UpdateAssetDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [JsonObjectConfig]
        public JsonElement Config { get; set; }

        // 缺 model 時補成 { materialColorOverrides: {} }。
        // 驗證屬性只看得到、改不了請求內容，所以補值放在 action 內；
        // 不補的話，舊資料第一次存檔就會寫入不完整的 config。
        public void ApplyDefault()
        {
            // JsonElement 唯讀：要改內容得先轉成可變的 JsonNode（`AsObject()` 轉成物件節點才能增刪屬性），改完再序列化回來。
            var config = JsonNode.Parse(Config.GetRawText())!.AsObject();
            // model 沒有時補上預設結構
            if (config["model"] is null)
            {
                config["model"] = new JsonObject
                {
                    ["materialColorOverrides"] = new JsonObject()
                };
            }

            Config = JsonSerializer.SerializeToElement(config);
        }
    }
    // Config 內容驗證。內建 DataAnnotations 只能驗單一值（必填、長度），巢狀 JSON 的形狀表達不出來，
    // 因此逐層檢查；任一層不合都回 400 INVALID_REQUEST_BODY（不指出哪個欄位，與 Node 版相同）。
    public class JsonObjectConfigAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            // 檢查 value 是否為 JsonElement，並且 ValueKind 是否為 Object
            // element 是 JsonElement 的別名，ValueKind 是 JsonElement 的屬性，表示 JSON 值的類型
            if (value is not JsonElement config || config.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return IsValidCamera(config) && IsValidCube(config) && IsValidModel(config);
        }
        
        // 驗證 camera 物件
        private static bool IsValidCamera(JsonElement config)
        {
            // 驗證JSON內的 camera 是不是物件
            if (!config.TryGetProperty("camera", out var camera) ||
                camera.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            // 驗證 camera 內的 position 是不是陣列且是不是有3個內容(x,y,x座標)
            if (!camera.TryGetProperty("position", out var position) ||
                position.ValueKind != JsonValueKind.Array ||
                position.GetArrayLength() != 3)
            {
                return false;
            }
            // 驗證 position 內的數值是不是有限數字
            foreach (var element in position.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Number ||
                    !element.TryGetDouble(out var number) ||
                    !double.IsFinite(number))
                {
                    return false;
                }
            }
            // 驗證 camera 內的 fov 屬性是不是數字
            if (!camera.TryGetProperty("fov", out var fov) ||
                fov.ValueKind != JsonValueKind.Number ||
                !fov.TryGetDouble(out var fovNumber) ||
                !double.IsFinite(fovNumber))
            {
                return false;
            }

            return true;
        }
        // 驗證 cube 物件
        private static bool IsValidCube(JsonElement config)
        {
            // 驗證 JSON 內的 cube 是不是物件
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
        // 驗證 model 物件
        private static bool IsValidModel(JsonElement config)
        {
            // model 為選填：舊資料沒有這個欄位，缺就放行，由 ApplyDefault 補預設；
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
            // 檢查 overrides 內的屬性（取法與上同：先 ValueKind，再取值）
            foreach(var property in overrides.EnumerateObject())
            {
                // material ID 不可空白
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