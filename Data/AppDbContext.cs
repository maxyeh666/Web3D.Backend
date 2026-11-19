using Microsoft.EntityFrameworkCore;
using Web3D.Backend.Models;

namespace Web3D.Backend.Data
{
    // AppDbContext 是 EF Core 裡「這次資料庫操作」的概念（相當於 Node 版打開一次連線、做完查詢再歸還的概念，打包成一個物件）。
    // 每個 HTTP 請求由 DI 建立一份新的，用完即丟；不要在 Service 之外保存它。
    // 下面的實體對應（Entity 對應資料表）都在建構子注入的 options 裡備好，這裡只負責描述「類別對資料表」這層對照。
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Asset> Assets { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // 設定 Asset entity 的資料表映射名稱、主鍵、欄位名稱與欄位屬性
            modelBuilder.Entity<Asset>(entity =>
            {
                entity.ToTable("assets");
                // Id 是自動遞增主鍵（新增不要自己塞，資料庫會忽略）。
                entity.HasKey(asset => asset.Id);
                // 欄位名、型別、必填直接看下面的程式碼（HasColumnName / HasColumnType / IsRequired），這裡只記程式碼看不出來的：
                // - Id 是 GENERATED ALWAYS（自動遞增主鍵）：新增資料不要自己塞，資料庫會忽略你塞的值。
                // - UpdatedAt 由程式寫入（Service 設 UtcNow）；CreatedAt 靠資料庫預設值，只在新增時產生。
                entity.Property(asset => asset.Id).HasColumnName("id").UseIdentityAlwaysColumn();
                entity.Property(asset => asset.Name).HasColumnName("name").HasColumnType("text").IsRequired();
                // jsonb 在 C# 這邊先用 string 接整包 JSON（整包存、整包取，不拆欄位）。
                // 理由：config 的內容會隨資產類型變化；以後需要強型別再換成 JsonDocument，屆時只要改這個屬性型別。
                entity.Property(asset => asset.Config)
                    .HasColumnName("config")
                    .HasColumnType("jsonb")
                    .IsRequired();
                entity.Property(asset => asset.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .IsRequired();
                entity.Property(asset => asset.CreatedAt)
                    .HasColumnName("created_at")
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .ValueGeneratedOnAdd()
                    .IsRequired();
                entity.Property(asset => asset.ModelKey)
                    .HasColumnName("model_key")
                    .HasColumnType("text")
                    .IsRequired();
            });
        }
    }
}