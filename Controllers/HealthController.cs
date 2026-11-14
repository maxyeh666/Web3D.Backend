using Microsoft.AspNetCore.Mvc;
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
            // 模擬健康檢查邏輯，例如檢查資料庫連線、外部服務狀態等
            // Ok() 方法會返回一個 200 OK 的 HTTP 回應，並將 healthStatus 作為 JSON 格式的回應內容
            return Ok(new { status = "ok" });
        }
        // GET /health/db：實際連一次資料庫並量耗時。durationMs 用來分辨 DB 慢還是網路慢。
        // 失敗回 500（與 Node 版同狀態碼；本文形狀的統一待 Phase 3）。
        [HttpGet("db")]
        public async Task<IActionResult> GetDatabaseStatus()
        {
            var startedAt = System.Diagnostics.Stopwatch.StartNew();
            var connected = await _dbContext.Database.CanConnectAsync();
            if (!connected) return StatusCode(500, new { status = "error" });
            return Ok(new { status = "ok", database = new { connected }, durationMs = startedAt.ElapsedMilliseconds });
        }
    }
}