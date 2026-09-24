using System.Text;
using System.Text.Json.Serialization;
using BCKash.Api.Authorization;
using BCKash.Api.Infrastructure;
using BCKash.Api.OpenApi;
using BCKash.Application.Auth;
using BCKash.Infrastructure;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Loads BCKashServer2.0/.env into the process environment for local `dotnet run`, walking up
// from the current directory to find it regardless of whether it's run from the repo root or
// from BCKash.Api/. In docker-compose the same keys arrive as real container env vars via
// `env_file`, so there's no .env on disk inside the container and this is a no-op there.
try
{
    DotNetEnv.Env.TraversePath().Load();
}
catch (FileNotFoundException)
{
}

var builder = WebApplication.CreateBuilder(args);

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured — set Jwt__SigningKey in BCKashServer2.0/.env (copy .env.example) for local development.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentHttpUserContext>();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new()
        {
            Title = "BCKash API",
            Version = "v1",
            Description = "REST API for BCKash — a microfinance and loan management platform.",
        };
        return Task.CompletedTask;
    });
});

const string SpaDevCorsPolicy = "SpaDev";
builder.Services.AddCors(options =>
{
    // Dev-only: the SPA's own CORS origin is set via configuration once a real
    // deployment target exists; this keeps `npm run dev` working against the API
    // without hardcoding a port here.
    options.AddPolicy(SpaDevCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];

        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        // The default "invalid_token" challenge gives no clue which check failed (expired vs.
        // wrong signing key vs. wrong issuer/audience) — log the actual exception so a rejected
        // token is debuggable from the server log instead of just the generic 401.
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtBearer")
                    .LogWarning(context.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

var app = builder.Build();

// Applies any pending EF Core migrations on startup — idempotent, so it's a no-op once the DB
// is up to date. Without this, a fresh database (e.g. the first time a docker-composed MySQL
// container boots against an empty volume) has no tables at all, and IdentityBootstrapSeeder /
// ReferenceDataSeeder crash the app trying to query them. Skipped under Testing:UseSqlite,
// where BCKashWebApplicationFactory creates the SQLite schema directly via EnsureCreated()
// instead — mixing that with Migrate() isn't supported (no migrations history table exists).
if (!app.Configuration.GetValue<bool>("Testing:UseSqlite"))
{
    using var migrationScope = app.Services.CreateScope();
    migrationScope.ServiceProvider.GetRequiredService<BCKashDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    // Docs live under /api alongside the versioned routes (/api/v1/...): the UI at
    // /api/swagger, one OpenAPI document per API version at /api/openapi/{version}.json.
    app.MapOpenApi("/api/openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/api/openapi/v1.json", "BCKash API v1");
        options.RoutePrefix = "api/swagger";
    });
}

app.UseCors(SpaDevCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "BCKash API").ExcludeFromDescription();

app.Run();
