using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Web3D.Backend.Data;
using Web3D.Backend.Services;
using Web3D.Backend.Middleware;
using Web3D.Backend.Constants;
using System.Text;
using Npgsql;
using Web3D.Backend.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// PORT：部署平台（例如 Render）用環境變數指定要監聽的埠；本機不設就沿用預設值。
// 綁 0.0.0.0 而非 localhost：容器內的服務要接受來自外部（平台反向代理）的連線。
var portValue = builder.Configuration["PORT"];

if (!string.IsNullOrWhiteSpace(portValue))
{
    // 格式先在這裡驗：報錯時能指出是 PORT 的值不合法，而不是看到 Kestrel 綁定失敗的原始例外。
    if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
    {
        throw new InvalidOperationException("PORT must be a valid port number.");
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
// ADMIN_PASSWORD：管理者單一密碼，登入端點用它比對。沒設就啟動失敗，避免部署完才發現登入永遠回 401。
var adminPassword = builder.Configuration["ADMIN_PASSWORD"];
if (string.IsNullOrWhiteSpace(adminPassword))
{
    throw new InvalidOperationException("ADMIN_PASSWORD is not configured.");
}
// JWT_SECRET：簽發與驗證 token 共用的密鑰。
var jwtSecret = builder.Configuration["JWT_SECRET"];
if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException("JWT_SECRET is not configured.");
}
// CORS（跨來源資源共用：伺服器明確允許後，瀏覽器才會把跨來源的回應交給前端程式碼讀取）。
// 來源白名單寫在設定而非程式：本機與正式環境的來源不同，改設定就好。
// 這段委派在 AddCors 當下就會執行（不是等第一次請求），所以下面的 throw 屬於啟動期檢查。
builder.Services.AddCors(options =>
{
    // 政策名稱 "Frontend" 要與下面的 app.UseCors("Frontend") 一致；打錯字會在第一次請求時才拋 PolicyNotFound。
    options.AddPolicy("Frontend", policy =>
    {
        var corsOrigin = builder.Configuration["CORS_ORIGIN"];

        if (string.IsNullOrWhiteSpace(corsOrigin))
        {
            // 沒有設定就不限來源
            policy.AllowAnyOrigin();
        }
        else
        {
            var origins = corsOrigin
                .Split(",")
                .Select(origin => origin.Trim())
                .Where(origin => origin.Length > 0)
                .ToArray();

            // 有設定卻拆不出任何來源（例如 " , "）視為設定錯誤
            if (origins.Length == 0)
            {
                throw new InvalidOperationException("CORS_ORIGIN does not contain any valid origins.");
            }

            policy.WithOrigins(origins);
        }

        // 來源已用白名單控管；方法與標頭全開與 Node 版 cors() 的預設對齊。
        // 前端每個請求都帶 Content-Type，寫入再帶 Authorization，兩者都會觸發 preflight。
        policy.AllowAnyMethod();
        policy.AllowAnyHeader();
    });
});

// 註冊認證機制（Authentication：確認「你是誰」）。之後每個請求，框架會從 Authorization 標頭取出 Bearer token 來驗。
// JwtBearerDefaults.AuthenticationScheme 是字串 Bearer 的官方常數：不用手寫字串，避免拼錯。
// AddJwtBearer(...) 括號裡的 options 由框架在啟動時執行。
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // 簽名不對就拒收。
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        // 不驗發行者（issuer）與對象（audience）：自簽的管理員 token 沒有這兩個欄位。
        ValidateIssuer = false,
        ValidateAudience = false,
        // 開啟過期檢查（實際效期 15 分鐘寫在 AuthService.CreateToken）。
        ValidateLifetime = true,
        // token 過期緩衝時間：預設有 5 分鐘寬限，設成 0 與 Node 版對齊（Node 版過期即失效）。
        ClockSkew = TimeSpan.Zero,
        // 只接受 HS256 簽章（與 Node 版相同；雜湊常數不能當簽章演算法用）。
        ValidAlgorithms = new[]
        {
            SecurityAlgorithms.HmacSha256
        },
        // 角色從哪個 claim 讀：token 裡的 role 欄位（需跟簽發端寫入的名稱一致）。
        RoleClaimType = "role"
    };

    options.Events = new JwtBearerEvents
    {
        // Challenge：沒帶 token 或 token 無效時，框架預設會回空本文 401。
        OnChallenge = async context =>
        {
            // 攔下框架預設的空本文，改寫成統一的 401 JSON。
            context.HandleResponse();

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            await context.Response.WriteAsJsonAsync(ApiErrors.Unauthorized);
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(ApiErrors.Forbidden);
        }
    };

    // 關掉框架預設的 claim 改名：否則 token 的 role 會被改寫成長 URI，RoleClaimType 就對不上。
    options.MapInboundClaims = false;
});
// 註冊授權服務（跟認證是兩件事，由 [Authorize] 使用）。
builder.Services.AddAuthorization();
// 取得連線字串；null 或空字串都視為沒設定（注意：空字串不會被 ?? 攔到，要用 IsNullOrWhiteSpace）
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "找不到連線字串 ConnectionStrings:DefaultConnection");
}

// 連線字串以程式碼為準：Timeout = 15（Neon 免費方案喚醒常超過 5 秒，與 Node 版 connectionTimeoutMillis: 15_000 對應）、
// CommandTimeout = 5（客戶端的查詢等待上限，與 Node 版 statement_timeout: 5_000 對應，兩層不並設）、
// MaxPoolSize = 5（兩版接同一顆資料庫，連線數一起算）、ConnectionIdleLifetime = 30。
// 機制差異與回收節奏（約 30～40 秒、非精確 30 秒）見 docs/learning/ef-core.md。

var npgsqlConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
{
    Timeout = 15,
    MaxPoolSize = 5,
    ConnectionIdleLifetime = 30
}.ConnectionString;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(npgsqlConnectionString, npgsql => npgsql.CommandTimeout(5)));

// 註冊 AssetService，為 Scoped（每個請求一份，用完釋放、連線歸還到池）。
builder.Services.AddScoped<AssetService>();
// AuthService 無狀態（只封裝驗密碼與簽 token），掛 Scoped 即可。
builder.Services.AddScoped<AuthService>();

builder.Services.AddControllers();
// 設定[ApiController]的預設行為：Model validation 失敗時的統一回應。
// JSON 解析失敗與內容不合規，Node 版都回 400 INVALID_REQUEST_BODY
// （routers/assets.ts 與 routers/auth.ts 的請求驗證失敗分支），這裡照同一套。
// 位置必須在 AddControllers() 之後：選項物件照註冊順序套用，放前面會被框架內建的預設值蓋掉。
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = _ =>
        new BadRequestObjectResult(ApiErrors.InvalidRequestBody);
});
// 開發環境才掛 /openapi（見下面的 IsDevelopment 判斷），正式環境不掛。
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<AuthorizeOperationTransformer>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 掛上 HTTP 處理流程（middleware：請求進來後依序經過的一串處理步驟），順序有意義：
// - ApiRequestLoggingMiddleware 放最外層：例外被內層轉成 500 之後才回到這裡，
//   記到的才是實際回給前端的狀態碼，而不是拋出前的 200。
// - UseCors 必須在 UseHttpsRedirection 之前，否則 preflight（OPTIONS）會先被 307 轉到 https，
//   瀏覽器會直接判定 CORS 失敗。
// - UseAuthentication / UseAuthorization：先確認你是誰，再判斷有沒有權限；
//   兩者都必須放在掛路由之前，否則請求還沒檢查就直接進到 Controller。
// - MapFallback：所有路由都沒匹配才會進來（統一 404），所以放最後。

app.UseMiddleware<ApiRequestLoggingMiddleware>();

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("Frontend");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// 未知路徑一律回同一份 404 JSON。
// 預設的 MapFallback 會排除像檔名的路徑（/nope.json 會回空本文），所以自己指定 "{*path}"。
app.MapFallback("{*path}", async context =>
{
    await Results.Json(
        ApiErrors.RouteNotFound,
        statusCode: StatusCodes.Status404NotFound
    ).ExecuteAsync(context);
});

app.Run();
