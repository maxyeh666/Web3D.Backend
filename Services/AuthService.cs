using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

namespace Web3D.Backend.Services
{
    // 只做兩件事：驗管理員密碼、簽發登入用的 token。無狀態，不記任何請求資料。
    public class AuthService
    {
        // IConfiguration 是 ASP.NET Core 提供的介面，用來讀取 appsettings.json 或其他設定來源的設定值。
        private readonly IConfiguration _configuration;

        public AuthService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool ValidatePassword(string password)
        {
            var adminPassword = _configuration["ADMIN_PASSWORD"];
            if (adminPassword is null)
            {
                throw new InvalidOperationException("ADMIN_PASSWORD is not configured.");
            }

            var providedBytes = Encoding.UTF8.GetBytes(password);
            var expectedBytes = Encoding.UTF8.GetBytes(adminPassword);

            // 固定時間比對：一般相等判斷遇到不同就提早回傳，攻擊者可用回應時間差反推密碼；這裡每次都比完整段。
            // 長度不同直接回 false（與 Node 版先比長度再 timingSafeEqual 同一意思）。
            return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
        // 簽發登入 token：HS256 對稱簽章（簽發與驗證同一把 JWT_SECRET，證明是自己發的）。
        // claims 只放 role=admin；效期 15 分鐘，須與 Program.cs 的驗證設定成對。
        // iat（簽發時間）手動加是為了與 Node 版 jsonwebtoken 的 payload 一致。
        public string CreateToken ()
        {
            var jwtSecret = _configuration["JWT_SECRET"];
            if (jwtSecret is null)
            {
                throw new InvalidOperationException("JWT_SECRET is not configured.");
            }

            // 把密鑰字串包成金鑰物件：簽章 API 吃的是金鑰物件不是字串，所以先轉 UTF-8 位元組再包裝。
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            // HS256：與 Node 版同一種簽章演算法（SHA256 只是雜湊名稱，不能用在這裡）。
            // 把「金鑰＋演算法」綁成簽章設定：簽和驗都用同一份，兩邊才對得上。
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;

            // claim（token payload 裡的一個鍵值，一個 Claim 就是一對名字與值）。
            // JwtRegisteredClaimNames.Iat 是標準 claim 名 iat（issued at，簽發於）的常數：用常數避免手打拼錯。
            // JWT 的時間單位是 Unix 秒（從 1970-01-01 起算的秒數），所以用 DateTimeOffset(now).ToUnixTimeSeconds() 換算。
            // ClaimValueTypes.Integer64 宣告它是整數：不寫會被當字串存，兩版簽章就對不上了。
            var claims = new[]
            {
                new Claim("role", "admin"),
                new Claim(
                    JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64
                )
            };

            // 用物件描述一張 token：claims、生效時間（notBefore）、到期（15 分鐘）、簽章設定。此時它還只是記憶體中的物件。
            var token = new JwtSecurityToken(
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(15),
                signingCredentials: credentials
            );
            
            // 序列化成 xxx.yyy.zzz 三段字串：這才是回給前端、之後每個請求帶在標頭裡的東西。
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}