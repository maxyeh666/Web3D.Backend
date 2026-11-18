using System.Diagnostics;

namespace Web3D.Backend.Middleware
{
    // 每個請求印一行存取日誌（格式與 Node 版一致：[API] 方法 路徑 狀態碼 耗時ms）。
    // 放在管線最外層（ExceptionMiddleware 之前）：例外被內層轉成回應之後才回到這裡，
    // 記到的狀態碼才是實際回給前端的。/health 也照記，與 Node 版行為一致。
    public class ApiRequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiRequestLoggingMiddleware> _logger;

        public ApiRequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<ApiRequestLoggingMiddleware> logger
        ) {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Stopwatch 是單調時鐘（不受系統時間被調整影響），適合量耗時；用 DateTime 相減則不行。
            var stopwatch = Stopwatch.StartNew();

            // 用 finally 而不用 catch：例外照樣往外拋、交給 ExceptionMiddleware 統一處理，這裡只負責記錄。
            try
            {
                await _next(context);
            }
            finally
            {
                // finally 保證連拋出例外時也會停錶並記錄：例外發生後，回應已由內層決定，這裡讀到的是最終狀態。
                stopwatch.Stop();

                // 路徑加查詢字串，等同 Node 的 req.originalUrl（不含 scheme 與 host）。
                var method = context.Request.Method;
                var url = context.Request.Path + context.Request.QueryString;
                var statusCode = context.Response.StatusCode;
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                // 參數化寫模板而不用字串插值：日誌系統能把 Method、StatusCode 拆成可檢索的欄位。
                _logger.LogInformation(
                    "[API] {Method} {Url} {StatusCode} {ElapsedMs}ms",
                    method,
                    url,
                    statusCode,
                    elapsedMs
                );
            }
        }
    }
}