using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MasterServer; 

var builder = WebApplication.CreateBuilder(args);

// 1. Lấy cấu hình
var jwtKey = builder.Configuration["Jwt:Secret"] ?? "YourSuperSecretKeyHere1234567890";
// Bạn có thể thêm port của Master Server vào appsettings.json hoặc mặc định ở đây
int masterPort = int.Parse(builder.Configuration["MasterServer:Port"] ?? "5000");

// 2. Đăng ký các Controllers và Services hiện có
builder.Services.AddControllers();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// Các Service logic của bạn
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<NodeService>();
builder.Services.AddScoped<DbConnection>();
builder.Services.AddScoped<RoomService>();

// 3. ĐĂNG KÝ MASTER SERVER CHẠY NGẦM (TCP)
// Việc này giúp MasterServer khởi động cùng lúc với Web API
builder.Services.AddHostedService<MasterServerBackgroundService>(sp =>
{
    return new MasterServerBackgroundService(masterPort);
});

var app = builder.Build();

// 4. Cấu hình Middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// --- LỚP WRAPPER ĐỂ CHẠY MASTER SERVER TRONG BACKGROUND ---
public class MasterServerBackgroundService : BackgroundService
{
    private readonly int _port;
    private readonly MasterServer.MasterServer _server;

    public MasterServerBackgroundService(int port)
    {
        _port = port;
        _server = new MasterServer.MasterServer();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chạy MasterServer trên một luồng riêng biệt để không chặn Web API
        try
        {
            // Bật server lên (chạy độc lập dưới nền)
            _server.Start(_port);

            // Giữ cho BackgroundService sống mãi chừng nào ứng dụng Web API còn chạy
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"[CRITICAL] MasterServer gặp lỗi: {ex.Message}");
        }
    }
}