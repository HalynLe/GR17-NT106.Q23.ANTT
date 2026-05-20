using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
Console.OutputEncoding = System.Text.Encoding.UTF8;

// 1. Lấy cấu hình
var jwtKey = builder.Configuration["Jwt:Secret"] ?? "YourSuperSecretKeyHere1234567890";

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

var app = builder.Build();

// 4. Cấu hình Middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();