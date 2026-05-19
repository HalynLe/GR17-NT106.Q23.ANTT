using MySql.Data.MySqlClient;
using Dapper;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using System;

public class AuthService
{
    private readonly string _connectionString;
    private readonly JwtHelper _jwtHelper;

    public AuthService(IConfiguration configuration, JwtHelper jwtHelper)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
                            ?? throw new InvalidOperationException("Không tìm thấy cấu hình 'DefaultConnection' trong appsettings.json.");
        _jwtHelper = jwtHelper;
    }

    public bool Register(RegisterRequest req)
    {
        using var conn = new MySqlConnection(_connectionString);

        var existing = conn.QueryFirstOrDefault<User>(
    "SELECT * FROM users WHERE username = @username",
    new { req.username }
);
        if (existing != null) return false;

        string hash = BCrypt.Net.BCrypt.HashPassword(req.password);

        conn.Execute(
            "INSERT INTO Users(username, password_hash, email) VALUES(@username, @password, @email)",
            new
            {
                username = req.username,
                password = hash,
                email = req.email
            });

        return true;
    }

    public (User?, string) Login(LoginRequest req)
    {
        try
        {
            using var conn = new MySqlConnection(_connectionString);

            var user = conn.QueryFirstOrDefault<User>(
                "SELECT * FROM Users WHERE username = @username",
                new { req.username });

            if (user == null)
                return (null, "Tài khoản không tồn tại.");

            bool valid = BCrypt.Net.BCrypt.Verify(req.password, user.password_hash);

            if (!valid)
                return (null, "Mật khẩu không chính xác.");

            var jwt = _jwtHelper.GenerateToken(user);

            return (user, jwt);
        }
        catch (MySql.Data.MySqlClient.MySqlException mySqlEx)
        {
            Console.WriteLine($"[DB ERROR] Lỗi Database tại hàm Login: {mySqlEx.Message}");
            return (null, "Lỗi hệ thống: Không thể kết nối hoặc truy vấn cơ sở dữ liệu.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Lỗi hệ thống tại hàm Login: {ex.Message}");
            return (null, "Đã xảy ra lỗi không xác định. Vui lòng thử lại sau.");
        }
    }
}