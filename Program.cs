using Microsoft.EntityFrameworkCore;
using Web3D.Backend.Data;
using Web3D.Backend.Services;

var builder = WebApplication.CreateBuilder(args);
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
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// 掛上 HTTP 處理流程（middleware：請求進來後會依序經過的一串處理步驟）。
// 順序有意義：
// - UseHttpsRedirection：先把 http 導到 https。
//   本機只開 http 埠（3000）、沒配 https 埠時，這行會直接放行、不做事。
// - UseAuthorization：判斷「這個請求有沒有權限」。
//   必須放在掛路由之前，否則請求還沒檢查身分就直接進到 Controller。
// - MapControllers：最後才掛路由（相當於 Node 版 app.use('/assets', assetsRouter) 那三行）。
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
