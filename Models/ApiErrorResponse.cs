namespace Web3D.Backend.Models
{
    // 錯誤回應的外層。Node 版把錯誤包在 error 裡（src/errorHandler.ts），
    // 所以這裡也包一層，送出去的形狀是 {"error":{"code","message"}}。
    public class ApiErrorResponse
    {
        public ApiErrorDetail Error { get; set; } = new();
    }

    // 錯誤本體：code 給程式分支用，message 給人看。
    public class ApiErrorDetail
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
