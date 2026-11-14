using System.Text;
using Asms.Api.Tenancy;
using Asms.Api.Configuration;
using Asms.Application.Abstractions;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<DatabaseOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        options.Provider = configuration["Database:Provider"] ?? string.Empty;
        options.ConnectionString = configuration.GetConnectionString("Default") ?? string.Empty;
    })
    .Validate(options =>
    {
        options.Validate();
        return true;
    }, "Database configuration is invalid.")
    .ValidateOnStart();

Action<DbContextOptionsBuilder> configureDb =
    options => ConfigureDatabase(builder.Configuration, options);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, HttpContextCurrentTenant>();
builder.Services.AddDbContext<AsmsDbContext>(configureDb);
builder.Services.AddAsmsIdentity(configureDb);
builder.Services.AddAsmsAuthentication(builder.Configuration);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var settings = jwtOptions.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static void ConfigureDatabase(IConfiguration configuration, DbContextOptionsBuilder options)
{
    var provider = configuration["Database:Provider"];
    var connectionString = configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Database configuration is invalid.");
    }

    switch (provider)
    {
        case "Sqlite":
            options.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"));
            break;
        case "PostgreSql":
            options.UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsAssembly("Asms.Infrastructure.Migrations.PostgreSql"));
            break;
        default:
            throw new InvalidOperationException("Database:Provider must be Sqlite or PostgreSql.");
    }
}

public partial class Program
{
}
