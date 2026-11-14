using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Web3D.Backend.Data;
using Web3D.Backend.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// JWT_SECRET：簽發與驗證 token 共用的密鑰（對稱式金鑰：簽發和驗證用同一把，就能證明 token 是自己發的）。
var jwtSecret = builder.Configuration["JWT_SECRET"];
if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException("JWT_SECRET is not configured.");
}
// 註冊認證機制（Authentication：確認「你是誰」）。之後每個請求，框架會從 Authorization 標頭取出 Bearer token 來驗。
// JwtBearerDefaults.AuthenticationScheme 是字串 Bearer 的官方常數：不用手寫字串，避免拼錯。
// AddJwtBearer(...) 括號裡的 options => … 是啟動設定（委派：傳一段設定程式碼給框架，由框架在啟動時執行）。
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // 要求驗證 JWT 簽名：簽名能證明 token 沒被竄改（防偽造），簽名不對就拒收。
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        // 不驗發行者（issuer）與對象（audience）：自簽的管理員 token 沒有這兩個欄位。
        ValidateIssuer = false,
        ValidateAudience = false,
        // 檢查token過期，目前設定15分鐘
        ValidateLifetime = true,
        // token 過期緩衝時間：預設有 5 分鐘寬限，設成 0 與 Node 版對齊（Node 版過期即失效）。
        ClockSkew = TimeSpan.Zero,
        // 只接受 HS256 簽章（與 Node 版相同；SHA256 是雜湊演算法名稱，不能當簽章演算法用）。
        ValidAlgorithms = new[]
        {
            SecurityAlgorithms.HmacSha256
        },
        // 角色從哪個 claim 讀：token 裡的 role 欄位（需跟簽發端寫入的名稱一致）。
        RoleClaimType = "role"
    };

    // 關掉框架預設的 claim 改名：否則 token 的 role 會被改寫成長 URI，RoleClaimType 就對不上。
    options.MapInboundClaims = false;
});
// 註冊授權服務（Authorization：判斷有沒有權限，由 [Authorize] 使用；跟認證是兩件事）。
builder.Services.AddAuthorization();
// 取得連線字串；null 或空字串都視為沒設定（注意：空字串不會被 ?? 攔到，要用 IsNullOrWhiteSpace）
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "找不到連線字串 ConnectionStrings:DefaultConnection");
}

// 把 AppDbContext 加進 DI 容器（依賴注入容器：框架內建的物件工廠，負責建立並提供各類別的實例）。
// AddDbContext 預設註冊為 Scoped（每個 HTTP 請求一份）。
// 請求結束就釋放（連線歸還到 Npgsql 的連線池），所以絕對不要在 Service 之外保存 AppDbContext。
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
// 註冊 AssetService 為 Scoped 服務，方便在 Controller 中注入使用
// Scoped 服務的生命週期為每個請求，適合用於資料庫操作
builder.Services.AddScoped<AssetService>();
// AuthService 無狀態（只封裝驗密碼與簽 token），掛 Scoped 即可。
builder.Services.AddScoped<AuthService>();
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// 掛上 HTTP 處理流程（middleware：請求進來後會依序經過的一串處理步驟）。
// 順序有意義：
// - UseHttpsRedirection：先把 http 導到 https。
//   本機只開 http 埠（3000）、沒配 https 埠時，這行會直接放行、不做事。
// - UseAuthentication：確認你是誰（解析 Bearer token）。
// - UseAuthorization：判斷有沒有權限（執行 [Authorize] 規則）。
//   兩者都必須放在掛路由之前，否則請求還沒檢查就直接進到 Controller。
// - MapControllers：最後才掛路由（相當於 Node 版 app.use('/assets', assetsRouter) 那三行）。
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
