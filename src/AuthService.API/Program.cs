using System.Threading.RateLimiting;
using System.Text;
using AuthService.API.Middleware;
using AuthService.Application.Common;
using AuthService.Application.UseCases.LoginUser;
using AuthService.Application.UseCases.RegisterUser;
using AuthService.Domain.Entities;
using AuthService.Domain.Interfaces;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? throw new InvalidOperationException("JWT_SECRET is required.");
var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "http://localhost:5173";
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5433;Database=auth_db;Username=auth_user;Password=auth_pass";

builder.Services.Configure<JwtOptions>(options =>
{
    options.Secret = jwtSecret;
    options.ExpiryHours = int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIRY_HOURS"), out var hours) ? hours : 8;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserAccess.AdminRole));
});

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(databaseUrl));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<LoginUserHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSecurityHeaders();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await dbContext.Database.ExecuteSqlRawAsync(
        """
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "Role" character varying(20) NOT NULL DEFAULT 'user';
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "Permissions" character varying(300) NOT NULL DEFAULT 'dashboard,reservations,calendar';
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "AvatarUrl" text NOT NULL DEFAULT '';
        """);

    const string defaultUserEmail = "jefferson@teste.com";
    var hasDefaultUser = await dbContext.Users.AnyAsync(user => user.Email == defaultUserEmail);
    if (!hasDefaultUser)
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        dbContext.Users.Add(User.Create("Usuário Teste", defaultUserEmail, passwordHasher.Hash("teste123")));
        await dbContext.SaveChangesAsync();
    }

    var seededDefaultUser = await dbContext.Users.FirstAsync(user => user.Email == defaultUserEmail);
    if (seededDefaultUser.Role != UserAccess.AdminRole)
    {
        seededDefaultUser.UpdateAccess("Usuario Teste", defaultUserEmail, UserAccess.AdminRole, UserAccess.AllPermissions);
        await dbContext.SaveChangesAsync();
    }
}

app.MapControllers();
app.Run();

internal static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
            await next();
        });
    }
}
