using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web3D.Backend.Data;

namespace Web3D.Backend.Controllers
{
    // HealthController 負責處理健康檢查相關的 HTTP 請求
    // ApiController 屬性表示這是一個 API 控制器，會自動處理模型驗證錯誤，並返回適當的 HTTP 狀態碼
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public HealthController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // GET /health：只確認服務活著，不碰資料庫。
        // 這支不用 async：只回固定字串，沒有非同步工作要等。
        // IActionResult 是 ASP.NET Core 中的介面，表示一個 HTTP 回應，方便前端使用
        [HttpGet]
        public IActionResult GetHealthStatus()
        {
            // 只回固定字串、不碰資料庫：給負載平衡器／冷啟動喚醒用的輕量探測。
            // Ok() 方法會返回一個 200 OK 的 HTTP 回應，並將 healthStatus 作為 JSON 格式的回應內容
            return Ok(new { status = "ok" });
        }
        // GET /health/db：實際跑一次 SELECT 1 並量耗時。durationMs 用來分辨 DB 慢還是網路慢。
        // 失敗讓例外往外拋，由 ExceptionMiddleware 記完整錯誤並回契約形狀的 500
        //（與 Node 版由 errorHandler 印出錯誤、回同一形狀一致）。
        // 不用 CanConnectAsync 的原因：它把失敗轉成 false、不拋例外，失敗原因不會留下任何日誌。
        [HttpGet("db")]
        public async Task<IActionResult> GetDatabaseStatus()
        {
            var startedAt = System.Diagnostics.Stopwatch.StartNew();
            await _dbContext.Database.ExecuteSqlAsync($"SELECT 1");
            return Ok(new
            {
                status = "ok",
                database = new { connected = true },
                durationMs = startedAt.ElapsedMilliseconds
            });
        }
    }
}