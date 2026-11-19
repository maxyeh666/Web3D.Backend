using Web3D.Backend.Constants;

namespace Web3D.Backend.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger
        )
        {
            _next = next;
            _logger = logger;
        }
        // 攔截下游未處理例外：記完整堆疊、回契約形狀的 500；回應已開始送出時重拋，不吞例外。
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "An unhandled exception occurred.");

                // 回應已開始送出就無法回頭改寫：重拋中止，讓伺服器關掉連線。
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