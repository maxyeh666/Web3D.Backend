using System.ComponentModel.DataAnnotations;

namespace Web3D.Backend.Models
{
    // 登入請求的形狀：只收密碼。缺欄位或空字串時，框架自動回 400，不會進到 Login。
    public class LoginDto
    {
        // AllowEmptyStrings 讓空字串通過 Required，再由 MinLength(1) 擋下：兩種情況都回 400。
        [Required(AllowEmptyStrings = true)]
        [MinLength(1)]
        public string Password { get; set; } = string.Empty;
    }
} 