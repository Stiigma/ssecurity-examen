using System.Text;
using ExamenSecurity.Api;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Middleware;
using ExamenSecurity.Api.Options;
using ExamenSecurity.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.Configure<AccountLockoutOptions>(builder.Configuration.GetSection(AccountLockoutOptions.SectionName));
builder.Services.Configure<LogIntegrityOptions>(builder.Configuration.GetSection(LogIntegrityOptions.SectionName));
builder.Services.Configure<ExternalLogForwardingOptions>(builder.Configuration.GetSection(ExternalLogForwardingOptions.SectionName));
builder.Services.Configure<HoneytokenOptions>(builder.Configuration.GetSection(HoneytokenOptions.SectionName));
builder.Services.Configure<ImpossibleTravelOptions>(builder.Configuration.GetSection(ImpossibleTravelOptions.SectionName));

builder.Services.AddMemoryCache();

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseInMemoryDatabase("TestDb");
    });
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    });
}

builder.Services.AddScoped<DbSeeder>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentRecordService, StudentRecordService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddScoped<IDemoScenarioService, DemoScenarioService>();
builder.Services.AddScoped<ISecurityAuditService, SecurityAuditService>();
builder.Services.AddScoped<ISecurityAlertService, SecurityAlertService>();
builder.Services.AddScoped<IAccountLockoutService, AccountLockoutService>();
builder.Services.AddScoped<ISecurityLogIntegrityService, SecurityLogIntegrityService>();
builder.Services.AddSingleton<IExternalLogForwarder, ExternalLogForwarder>();
builder.Services.AddScoped<IGeoLocationService, GeoLocationService>();
builder.Services.AddScoped<IImpossibleTravelService, ImpossibleTravelService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Fixed A09 demo:
        // Authentication and authorization failures now become durable security events.
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = async context =>
            {
                var auditService = context.HttpContext.RequestServices.GetRequiredService<ISecurityAuditService>();
                await auditService.AuditAsync(
                    new SecurityAuditRequest(
                        SecurityEventType.TokenAuthenticationFailed,
                        SecuritySeverity.Warning,
                        "Rejected",
                        "Token JWT invalido, expirado o no verificable.",
                        StatusCode: StatusCodes.Status401Unauthorized,
                        Metadata: new Dictionary<string, object?>
                        {
                            ["failureType"] = context.Exception.GetType().Name
                        }),
                    context.HttpContext.RequestAborted);
            },
            OnChallenge = async context =>
            {
                if (context.AuthenticateFailure is not null)
                {
                    return;
                }

                var auditService = context.HttpContext.RequestServices.GetRequiredService<ISecurityAuditService>();
                await auditService.AuditAsync(
                    new SecurityAuditRequest(
                        SecurityEventType.UnauthorizedRequest,
                        SecuritySeverity.Warning,
                        "Rejected",
                        "Solicitud sin autenticacion valida contra recurso protegido.",
                        StatusCode: StatusCodes.Status401Unauthorized),
                    context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                var auditService = context.HttpContext.RequestServices.GetRequiredService<ISecurityAuditService>();
                await auditService.AuditAsync(
                    new SecurityAuditRequest(
                        SecurityEventType.AccessDenied,
                        SecuritySeverity.Warning,
                        "Forbidden",
                        "Usuario autenticado intento acceder a un recurso sin permisos suficientes.",
                        StatusCode: StatusCodes.Status403Forbidden),
                    context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("StudentOnly", policy => policy.RequireRole("Student", "Admin"));
});

builder.Services.AddCors(options =>
{
    var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(corsOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

app.Run();

public partial class Program;
