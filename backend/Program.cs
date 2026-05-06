using System.Threading.RateLimiting;
using Backend.Services;
using DotNetEnv;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ── Request body size limit (512 KB) ──────────────────────────────────────
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<IArchitectureAnalysisService, OpenRouterArchitectureAnalysisService>();

// ── CORS: restrito a origens conhecidas (localhost em dev) ─────────────────
var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(",")
    ?? ["http://localhost:5173", "http://localhost:6274", "http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalOnly", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ── Rate limiting: 20 req/min por IP (janela deslizante) ──────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("analysis", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                PermitLimit = 20,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                message = "An internal error occurred."
            });
        });
    });
}

app.UseCors("LocalOnly");
app.UseRateLimiter();

// ── API Key middleware (opt-in: só ativo se API_KEY estiver configurada) ───
var requiredApiKey = app.Configuration["API_KEY"] ?? app.Configuration["ApiKey"];
if (!string.IsNullOrWhiteSpace(requiredApiKey))
{
    app.Use(async (context, next) =>
    {
        // /health fica livre para monitoramento
        if (!context.Request.Path.StartsWithSegments("/health"))
        {
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var providedKey)
                || providedKey != requiredApiKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Unauthorized." });
                return;
            }
        }

        await next(context);
    });
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
