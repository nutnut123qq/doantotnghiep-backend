using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StockInvestment.Api.Hubs;
using StockInvestment.Api.Middleware;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Mappings;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Infrastructure.External;
using StockInvestment.Infrastructure.Services;
using StockInvestment.Infrastructure.Messaging;
using StackExchange.Redis;
using System.Text;
using MediatR;
using System.Reflection;
using FluentValidation;
using Serilog;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting StockInvestment.Api application...");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Response Compression (Gzip + Brotli)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Add SignalR
builder.Services.AddSignalR();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add MediatR with Pipeline Behaviors
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.RegisterServicesFromAssembly(typeof(StockInvestment.Application.Mappings.MappingProfile).Assembly);
});

// Add MediatR Pipeline Behaviors
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(StockInvestment.Application.Behaviors.ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(StockInvestment.Application.Behaviors.LoggingBehavior<,>));

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(StockInvestment.Application.Mappings.MappingProfile).Assembly);

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add JWT Authentication
var jwtSecret = builder.Configuration["JWT:Secret"] ?? "your-super-secret-key-change-in-production-min-32-chars";
var key = Encoding.ASCII.GetBytes(jwtSecret);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=stock_investment;Username=postgres;Password=postgres";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add Redis
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// Add Response Caching
builder.Services.AddResponseCaching();

// Add Unit of Work
builder.Services.AddScoped<IUnitOfWork, StockInvestment.Infrastructure.Data.UnitOfWork>();

// Add Repositories
builder.Services.AddScoped<StockInvestment.Application.Interfaces.IUserRepository, StockInvestment.Infrastructure.Data.Repositories.UserRepository>();
builder.Services.AddScoped<StockInvestment.Application.Interfaces.IWatchlistRepository, StockInvestment.Infrastructure.Data.Repositories.WatchlistRepository>();
builder.Services.AddScoped<StockInvestment.Application.Interfaces.IAlertRepository, StockInvestment.Infrastructure.Data.Repositories.AlertRepository>();
builder.Services.AddScoped<StockInvestment.Application.Interfaces.IUserPreferenceRepository, StockInvestment.Infrastructure.Data.Repositories.UserPreferenceRepository>();
builder.Services.AddScoped<StockInvestment.Application.Interfaces.ICorporateEventRepository, StockInvestment.Infrastructure.Data.Repositories.CorporateEventRepository>();
builder.Services.AddScoped<StockInvestment.Application.Interfaces.IDataSourceRepository, StockInvestment.Infrastructure.Data.Repositories.DataSourceRepository>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(StockInvestment.Infrastructure.Data.Repositories.Repository<>));

// Add Application Services
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IVNStockService, VNStockService>();
builder.Services.AddScoped<IStockDataService, StockDataService>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<INewsCrawlerService, NewsCrawlerService>();
builder.Services.AddScoped<IFinancialReportService, FinancialReportService>();
builder.Services.AddScoped<IFinancialReportCrawlerService, FinancialReportCrawlerService>();
builder.Services.AddScoped<IEventCrawlerService, EventCrawlerService>();
builder.Services.AddScoped<IAIService, AIServiceClient>();
builder.Services.AddScoped<ITechnicalIndicatorService, TechnicalIndicatorService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IDataSourceService, DataSourceService>();
builder.Services.AddScoped<StockInvestment.Application.Interfaces.IAIModelConfigRepository, StockInvestment.Infrastructure.Data.Repositories.AIModelConfigRepository>();
builder.Services.AddScoped<IAIModelConfigService, AIModelConfigService>();
builder.Services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

// Configure HTTP Client for AI Service
builder.Services.AddHttpClient("AIService", client =>
{
    var aiServiceUrl = builder.Configuration["AIService:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(aiServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<AIServiceClient>(client =>
{
    var aiServiceUrl = builder.Configuration["AIService:Url"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(aiServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure HTTP Client for News Crawler
builder.Services.AddHttpClient("NewsCrawler", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
});

// Configure HTTP Client for Financial Report Crawler
builder.Services.AddHttpClient("FinancialReportCrawler", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
});

// Add RabbitMQ connection string
var rabbitMQConnection = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? "amqp://guest:guest@localhost:5672/";

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "postgresql",
        tags: new[] { "db", "sql", "postgresql" })
    .AddRedis(
        redisConnection,
        name: "redis",
        tags: new[] { "cache", "redis" });

// RabbitMQ temporarily disabled due to version incompatibility
// TODO: Update RabbitMQ.Client to compatible version
/*
builder.Services.AddSingleton<RabbitMQService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RabbitMQService>>();
    return new RabbitMQService(logger, rabbitMQConnection);
});
*/

// Add Background Jobs
builder.Services.AddHostedService<StockInvestment.Infrastructure.BackgroundJobs.StockPriceUpdateJob>();
builder.Services.AddHostedService<StockInvestment.Infrastructure.BackgroundJobs.AlertMonitorJob>();
builder.Services.AddHostedService<StockInvestment.Infrastructure.BackgroundJobs.TechnicalIndicatorCalculationJob>();
builder.Services.AddHostedService<StockInvestment.Infrastructure.BackgroundJobs.NewsCrawlerJob>();
builder.Services.AddHostedService<StockInvestment.Infrastructure.BackgroundJobs.EventCrawlerJob>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Use Correlation ID Middleware (first to track all requests)
app.UseMiddleware<CorrelationIdMiddleware>();

// Use Serilog Request Logging
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
    };
});

// Use Response Compression (must be before other middleware)
app.UseResponseCompression();

app.UseHttpsRedirection();

// Use Response Caching
app.UseResponseCaching();

// Use new Global Exception Handler Middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Analytics middleware
app.UseAnalytics();

// Map Health Checks endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    AllowCachingResponses = false
});

// Map detailed health checks endpoint
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // Exclude all checks, just return 200 if app is running
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapControllers();
app.MapHub<StockPriceHub>("/hubs/stock-price");
app.MapHub<StockInvestment.Infrastructure.Hubs.TradingHub>("/hubs/trading");

try
{
    Log.Information("Starting web application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
