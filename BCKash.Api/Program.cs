using System.IO.Compression;
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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
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

// The EF second-level cache talks to Redis synchronously (EFCoreSecondLevelCacheInterceptor's
// provider API is sync), so every query briefly blocks a thread-pool thread. The pool only adds
// threads slowly once its minimum is used up, so under a burst of staff requests they queued behind
// each other with CPU to spare. Starting with more threads available removes that queue.
var minWorkerThreads = builder.Configuration.GetValue("ThreadPool:MinWorkerThreads", 200);
ThreadPool.GetMinThreads(out var currentWorkers, out var currentIo);
ThreadPool.SetMinThreads(Math.Max(minWorkerThreads, currentWorkers), Math.Max(minWorkerThreads, currentIo));

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
            OnTokenValidated = SessionEnforcement.RejectReplacedSessionAsync,
            OnChallenge = SessionEnforcement.ExplainReplacedSessionAsync,
        };
    });

// Staff work from all over the country, often on mobile data: compressing JSON cuts list and
// detail responses to a fraction of their size. Files (images, PDFs) are already compressed and
// aren't in the MIME list, so downloads pass through untouched.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// Behind a reverse proxy (nginx, a load balancer, Cloudflare) every request arrives from the proxy's
// address, so the per-IP sign-in limit would treat all staff as one visitor. With
// ForwardedHeaders:Enabled=true the API takes the caller's address from X-Forwarded-For instead.
// Only switch it on when the API is reachable solely through that proxy — otherwise anyone could
// send a fake X-Forwarded-For.
var trustForwardedHeaders = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (trustForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = builder.Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options => options.AddBCKashPolicies());

var app = builder.Build();

if (trustForwardedHeaders)
{
    app.UseForwardedHeaders();
}

app.UseResponseCompression();

// Applies any pending EF Core migrations on startup — idempotent, so it's a no-op once the DB
// is up to date. Without this, a fresh database (e.g. the first time a docker-composed MySQL
// container boots against an empty volume) has no tables at all, and IdentityBootstrapSeeder /
// ReferenceDataSeeder crash the app trying to query them. Skipped under Testing:UseSqlite,
// where BCKashWebApplicationFactory creates the SQLite schema directly via EnsureCreated()
// instead — mixing that with Migrate() isn't supported (no migrations history table exists).
if (!app.Configuration.GetValue<bool>("Testing:UseSqlite"))
{
    using var migrationScope = app.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<BCKashDbContext>();
    // Index builds on the multi-million-row loan tables outlast the 30s default command timeout
    // on the test/production hosts; timing out there crashes startup and the container restart-loops.
    migrationDb.Database.SetCommandTimeout(TimeSpan.FromMinutes(15));
    migrationDb.Database.Migrate();
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
app.Use(SessionEnforcement.RequirePasswordChangeFirstAsync);
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "BCKash API").ExcludeFromDescription();

app.Run();
