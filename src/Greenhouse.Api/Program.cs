using System.Text.Json;
using Greenhouse.Api.Auth;
using Greenhouse.Api.Data;
using Greenhouse.Api.Endpoints;
using Greenhouse.Api.Infrastructure;
using Greenhouse.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Логи: консоль + файл logs/greenhouse-<дата>.log (критерій C2, пункт 5).
var logPath = builder.Configuration["Logging:FilePath"] ?? Path.Combine("logs", "greenhouse-.log");
builder.Services.AddSerilog(lc => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.DataProtection", LogEventLevel.Error)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true,
        formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.Services.AddDbContext<GreenhouseDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Greenhouse") ?? "Data Source=greenhouse.db"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SensorService>();
builder.Services.AddScoped<ReadingService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddSingleton<TokenService>();

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

// Безпека: датчики пишуть з API-ключем, оператор підтверджує тривоги з JWT.
var authSection = builder.Configuration.GetSection(AuthOptions.Section);
builder.Services.Configure<AuthOptions>(authSection);
var authOptions = authSection.Get<AuthOptions>() ?? new AuthOptions();
if (authOptions.JwtSigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Auth:JwtSigningKey має містити щонайменше 32 символи. Задайте його змінною середовища Auth__JwtSigningKey.");
}

builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthNames.ApiKeyScheme, null)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = authOptions.JwtIssuer,
            ValidAudience = authOptions.JwtIssuer,
            IssuerSigningKey = TokenService.SigningKey(authOptions),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthNames.SensorPolicy, p => p
        .AddAuthenticationSchemes(AuthNames.ApiKeyScheme)
        .RequireRole(AuthNames.SensorRole))
    .AddPolicy(AuthNames.OperatorPolicy, p => p
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireRole(AuthNames.OperatorRole));

var app = builder.Build();

// Міграції застосовуються під час запуску: БД створюється з нуля без ручних кроків.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>().Database.MigrateAsync();
}

// Логування запитів стоїть перед обробником винятків, щоб у лог потрапляв фінальний код відповіді.
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapGreenhouseApi();

await app.RunAsync();

/// <summary>Точка входу; partial — щоб інтеграційні тести могли використати WebApplicationFactory.</summary>
public partial class Program;
