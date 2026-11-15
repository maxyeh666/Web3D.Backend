using Web3D.Backend.Models;

namespace Web3D.Backend.Constants
{
    // 錯誤碼與訊息的唯一來源。與 Node 版 src/errors/errorDefinition.ts 相同
    // MODEL_NOT_FOUND 等 GET /models/{modelKey} 實作時再加。
    public static class ApiErrors
    {
        public static ApiErrorResponse InvalidRequestBody => Build("INVALID_REQUEST_BODY", "Invalid JSON body");
        public static ApiErrorResponse InvalidAssetId => Build("INVALID_ASSET_ID", "Invalid asset ID");
        public static ApiErrorResponse InvalidCredentials => Build("INVALID_CREDENTIALS", "Invalid credentials");
        public static ApiErrorResponse Unauthorized => Build("UNAUTHORIZED", "Authorization required");
        public static ApiErrorResponse Forbidden => Build("FORBIDDEN", "Forbidden");
        public static ApiErrorResponse AssetNotFound => Build("ASSET_NOT_FOUND", "Asset not found");
        public static ApiErrorResponse RouteNotFound => Build("ROUTE_NOT_FOUND", "Route not found");
        public static ApiErrorResponse InternalServerError => Build("INTERNAL_SERVER_ERROR", "Internal server error");

        // 每次呼叫回一個新物件：呼叫端若改內容也不會污染其他請求。
        private static ApiErrorResponse Build(string code, string message) => new()
        {
            Error = new ApiErrorDetail { Code = code, Message = message }
        };
    }
}
