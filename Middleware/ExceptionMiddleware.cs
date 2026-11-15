using Web3D.Backend.Constants;

namespace Web3D.Backend.Middleware
{
    public class ExceptionMiddleware
    {
        // 下一個管線/步驟
        private readonly RequestDelegate _next;
        // 內建logger機制
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger
        )
        {
            _next = next;
            _logger = logger;
        }
        // 攔截下游管線拋出的例外，統一回 500 JSON。
        // context是傳進來的整個請求(Request+Response)
        // try...catch 機制：正常情況把請求交給下一個 middleware（_next），拋例外才進 catch。
        // catch到錯誤就寫入logger並進行錯誤處理
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "An unhandled exception occurred.");

                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                await context.Response.WriteAsJsonAsync(ApiErrors.InternalServerError);
            }
        }
    }
}