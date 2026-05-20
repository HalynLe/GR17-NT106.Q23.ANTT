using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest req)
    {
        var result = _authService.Register(req);

        if (!result)
            return BadRequest(new { message = "Username already exists" });

        return Ok(new { message = "Register success" });
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[MASTER - AUTH] Tài khoản [{req.username}] đang yêu cầu xác thực đăng nhập...");
        Console.ResetColor();

        var (user, token) = _authService.Login(req);

        if (user == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[MASTER - AUTH] XÁC THỰC THẤT BẠI: Tài khoản [{req.username}] cung cấp sai thông tin.");
            Console.ResetColor();
            return Unauthorized(new { message = "Invalid credentials" });
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MASTER - AUTH] ĐĂNG NHẬP THÀNH CÔNG -> User ID: {user.user_id} | Email: {user.email}");
        Console.ResetColor();

        return Ok(new
        {
            token = token,
            user = new
            {
                user.user_id,
                user.username,
                user.email
            }
        });
    }
    [HttpGet("test-db")]
    public IActionResult TestDb([FromServices] DbConnection dbConfig)
    {
        try
        {
            using var conn = dbConfig.GetConnection();
            conn.Open(); // Thực hiện kết nối
            return Ok(new { message = "Kết nối Database thành công!", status = "OK" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi kết nối DB", error = ex.Message });
        }
    }
}