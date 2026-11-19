using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web3D.Backend.Data;

namespace Web3D.Backend.Controllers
{
    // 健康檢查相關端點（路徑與行為見各方法）。
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public HealthController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // GET /health：只確認服務活著、不碰資料庫，給平台探測與冷啟動喚醒用。
        [HttpGet]
        public IActionResult GetHealthStatus()
        {
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