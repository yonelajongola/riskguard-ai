using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;
using RiskGuard.API.Filters;
using RiskGuard.API.Middleware;
using RiskGuard.Application.Assessments;
using RiskGuard.Application.Interfaces;
using RiskGuard.Application.Services;
using RiskGuard.Application.Validation;
using RiskGuard.Infrastructure;
using RiskGuard.Persistence;
using RiskGuard.Persistence.Identity;
using RiskGuard.Persistence.Seed;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddRiskGuardPersistence(builder.Configuration);
builder.Services.AddRiskGuardInfrastructure();
var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(Path.GetTempPath(), "RiskGuardAI-DataProtectionKeys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .SetApplicationName("RiskGuardAI")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
builder.Services.AddScoped<IRiskScoringService, RiskScoringService>();
builder.Services.AddScoped<IAnswerScoringService, AnswerScoringService>();
builder.Services.AddScoped<IRecommendationEngine, RecommendationEngine>();
builder.Services.AddScoped<IVendorRiskService, VendorRiskService>();
builder.Services.AddScoped<IIncidentWorkflowService, IncidentWorkflowService>();
builder.Services.AddScoped<IComplianceGapFactory, ComplianceGapFactory>();
builder.Services.AddScoped<CreateAssessmentHandler>();
builder.Services.AddScoped<IValidator<RiskGuard.Application.DTOs.RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<RiskGuard.Application.DTOs.LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<RiskGuard.Application.DTOs.CreateAssessmentRequest>, CreateAssessmentRequestValidator>();
builder.Services.AddScoped<IValidator<RiskGuard.Application.DTOs.CreateIncidentRequest>, CreateIncidentRequestValidator>();
builder.Services.AddScoped<FluentValidationActionFilter>();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<RiskGuardDbContext>()
    .AddDefaultTokenProviders();

var configuredJwtSecret = builder.Configuration["Jwt:Secret"];
var jwtSecret = string.IsNullOrWhiteSpace(configuredJwtSecret) ||
                configuredJwtSecret.Equals("JWT_SECRET", StringComparison.Ordinal)
    ? Environment.GetEnvironmentVariable("JWT_SECRET")
    : configuredJwtSecret;
if (string.IsNullOrWhiteSpace(jwtSecret) && builder.Environment.IsDevelopment())
{
    jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    builder.Configuration["Jwt:Secret"] = jwtSecret;
}
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException("JWT_SECRET must be configured outside Development.");
}
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException("JWT secret must be at least 32 bytes.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "RiskGuardAI",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "RiskGuardAI.Web",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Administrators", policy => policy.RequireRole("Admin"))
    .AddPolicy("RiskProfessionals", policy => policy.RequireRole("Admin", "Risk Manager", "Compliance Officer", "Security Analyst"))
    .AddPolicy("ReadSensitive", policy => policy.RequireRole("Admin", "Executive", "Risk Manager", "Auditor", "Compliance Officer", "Security Analyst", "Department Manager"))
    .AddPolicy("AiExecutive", policy => policy.RequireRole("Admin", "Executive", "Risk Manager", "Auditor"))
    .AddPolicy("AiMitigation", policy => policy.RequireRole("Admin", "Risk Manager", "Security Analyst", "Compliance Officer", "Department Manager"))
    .AddPolicy("AiCybersecurity", policy => policy.RequireRole("Admin", "Risk Manager", "Security Analyst"))
    .AddPolicy("AiCompliance", policy => policy.RequireRole("Admin", "Risk Manager", "Compliance Officer", "Auditor"));

builder.Services.AddControllers(options => options.Filters.AddService<FluentValidationActionFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RiskGuard AI API",
        Version = "v1",
        Description = "Enterprise risk intelligence, assessment, compliance, incident, vendor, continuity, reporting, and AI services."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"])
        .WithHeaders("Authorization", "Content-Type")
        .WithMethods("GET", "POST", "PUT", "DELETE")
        .AllowCredentials()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddHealthChecks();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();
app.UseForwardedHeaders();
app.UseRateLimiter();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RiskGuard AI API v1");
        options.DisplayRequestDuration();
    });
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RiskGuardDbContext>();
    if (app.Configuration.GetValue<bool>("SeedData:Enabled"))
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await SeedData.InitializeAsync(db, users, roles);
    }
    else
    {
        await db.Database.MigrateAsync();
    }
}

await app.RunAsync();

public partial class Program;
