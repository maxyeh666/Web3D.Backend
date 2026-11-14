using Microsoft.AspNetCore.Mvc;
using Web3D.Backend.Models;
using Web3D.Backend.Services;

namespace Web3D.Backend.Controllers
{
    [ApiController]
    [Route("auth")]
    // 登入入口：POST /auth/login。成功回 token（15 分鐘有效），密碼錯誤回 401 INVALID_CREDENTIALS。
    // 錯誤本文包一層 error，與 Node 版及 README 的錯誤格式一致。
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public IActionResult Login(LoginDto loginDto)
        {
            var isValid = _authService.ValidatePassword(loginDto.Password);

            if (!isValid)
            {
                return Unauthorized(new
                {
                    error = new
                    {
                        code = "INVALID_CREDENTIALS",
                        message = "Invalid credentials"
                    }
                });
            }

            var token = _authService.CreateToken();

            return Ok(new
            {
                token
            });
        }
    }
};
