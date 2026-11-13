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
                // 映射到資料表名稱 "assets"
                entity.ToTable("assets");
                // 設定主鍵為 Id 欄位
                entity.HasKey(asset => asset.Id);
                // 設定欄位名稱與屬性
                // Id 欄位對應到資料表的 "id" 欄位，並使用自動增量主鍵
                // Name 欄位對應到資料表的 "name" 欄位，資料型別為 text，且為必填欄位
                // Config 欄位對應到資料表的 "config" 欄位，資料型別為 jsonb，且為必填欄位
                // UpdatedAt 欄位對應到資料表的 "updated_at" 欄位，資料型別為 timestamp with time zone，且為必填欄位
                // CreatedAt 欄位對應到資料表的 "created_at" 欄位，資料型別為 timestamp with time zone，且為必填欄位
                // ModelKey 欄位對應到資料表的 "model_key" 欄位，資料型別為 text，且為必填欄位
                // 對應 SQL：id BIGINT GENERATED ALWAYS AS IDENTITY（自動遞增主鍵），
                // 所以程式新增資料時不要自己塞 Id——資料庫會自己編號（把你塞的值直接忽略掉）。
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