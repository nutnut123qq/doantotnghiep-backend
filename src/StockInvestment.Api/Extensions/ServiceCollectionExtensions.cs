using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Mappings;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Infrastructure.Data.Repositories;
using StockInvestment.Infrastructure.External;
using StockInvestment.Infrastructure.Services;
using StackExchange.Redis;
using System.Text;
using MediatR;
using System.Reflection;
using FluentValidation;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using ICacheKeyGenerator = StockInvestment.Application.Interfaces.ICacheKeyGenerator;

namespace StockInvestment.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add core API services (Controllers, Swagger, SignalR, CORS)
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        
        // Add Response Compression (Gzip + Brotli)
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        });

        services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });

        services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });

        // Add SignalR with Redis backplane for scaling
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSignalR()
            .AddStackExchangeRedis(redisConnection, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal("StockInvestment");
            });

        // Add CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        // Add Response Caching
        services.AddResponseCaching();

        return services;
    }

    /// <summary>
    /// Add MediatR and validation services
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add MediatR with Pipeline Behaviors
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.RegisterServicesFromAssembly(typeof(MappingProfile).Assembly);
        });

        // Add MediatR Pipeline Behaviors
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(StockInvestment.Application.Behaviors.ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(StockInvestment.Application.Behaviors.LoggingBehavior<,>));

        // Add FluentValidation
        services.AddValidatorsFromAssembly(typeof(MappingProfile).Assembly);

        // Add AutoMapper
        services.AddAutoMapper(typeof(MappingProfile));

        return services;
    }

    /// <summary>
    /// Add JWT Authentication services
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = configuration["JWT:Secret"] ?? "your-super-secret-key-change-in-production-min-32-chars";
        var jwtIssuer = configuration["JWT:Issuer"] ?? "StockInvestmentApi";
        var jwtAudience = configuration["JWT:Audience"] ?? "StockInvestmentClient";
        var isProduction = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Production";
        var key = Encoding.ASCII.GetBytes(jwtSecret);
        
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = isProduction;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = isProduction, // Validate in production
                ValidIssuer = jwtIssuer,
                ValidateAudience = isProduction, // Validate in production
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }

    /// <summary>
    /// Add database and caching services
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Database Context
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Database=stock_investment;Username=postgres;Password=postgres";
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Add Redis
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnection));
        // Register as Singleton since it's used in middleware (which is resolved from root provider)
        // and RedisCacheService is stateless and thread-safe
        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Add repositories and unit of work
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Add Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Add Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWatchlistRepository, WatchlistRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
        services.AddScoped<ICorporateEventRepository, CorporateEventRepository>();
        services.AddScoped<IDataSourceRepository, DataSourceRepository>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IAIModelConfigRepository, AIModelConfigRepository>();
        services.AddScoped<IAIInsightRepository, AIInsightRepository>();
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IStockTickerRepository, StockTickerRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();

        return services;
    }

    /// <summary>
    /// Add application business services
    /// </summary>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<ICacheKeyGenerator, CacheKeyGenerator>();
        services.AddScoped<ITechnicalDataService, TechnicalDataService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IVNStockService, VNStockService>();
        services.AddScoped<IStockDataService, StockDataService>();
        services.AddScoped<INewsService, NewsService>();
        services.AddScoped<INewsCrawlerService, NewsCrawlerService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();
        services.AddScoped<IFinancialReportCrawlerService, FinancialReportCrawlerService>();
        services.AddScoped<IEventCrawlerService, EventCrawlerService>();
        services.AddScoped<ITechnicalIndicatorService, TechnicalIndicatorService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ISystemHealthService, SystemHealthService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IDataSourceService, DataSourceService>();
        services.AddScoped<IAIModelConfigService, AIModelConfigService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<IPushNotificationService, PushNotificationService>();
        services.AddScoped<IAIInsightService, AIInsightService>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }

    /// <summary>
    /// Add HTTP clients configuration
    /// </summary>
    public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        // Register resilience policy service
        services.AddSingleton<Infrastructure.Services.ResiliencePolicyService>();

        // Configure HTTP Client for AI Service with resilience policies
        services.AddHttpClient("AIService", client =>
        {
            var aiServiceUrl = configuration["AIService:BaseUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(aiServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(120); // Increase timeout to 120 seconds for AI processing
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var policyService = serviceProvider.GetRequiredService<Infrastructure.Services.ResiliencePolicyService>();
            return policyService.CreateCombinedPolicy(
                retryCount: 3,
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
        });

        // Register AIServiceClient with HttpClient configuration and resilience policies
        services.AddHttpClient<AIServiceClient>(client =>
        {
            var aiServiceUrl = configuration["AIService:BaseUrl"] ?? configuration["AIService:Url"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(aiServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(120); // Increase timeout to 120 seconds for AI processing
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var policyService = serviceProvider.GetRequiredService<Infrastructure.Services.ResiliencePolicyService>();
            return policyService.CreateCombinedPolicy(
                retryCount: 3,
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
        });

        // Register IAIService to use AIServiceClient
        services.AddScoped<IAIService>(sp => sp.GetRequiredService<AIServiceClient>());

        // Configure HTTP Client for News Crawler with resilience policies
        services.AddHttpClient("NewsCrawler", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var policyService = serviceProvider.GetRequiredService<Infrastructure.Services.ResiliencePolicyService>();
            return policyService.CreateCombinedPolicy(
                retryCount: 2,
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(15));
        });

        // Configure HTTP Client for Financial Report Crawler with resilience policies
        services.AddHttpClient("FinancialReportCrawler", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var policyService = serviceProvider.GetRequiredService<Infrastructure.Services.ResiliencePolicyService>();
            return policyService.CreateCombinedPolicy(
                retryCount: 2,
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(15));
        });

        return services;
    }

    /// <summary>
    /// Add background jobs
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        services.AddHostedService<Infrastructure.BackgroundJobs.StockPriceUpdateJob>();
        services.AddHostedService<Infrastructure.BackgroundJobs.AlertMonitorJob>();
        services.AddHostedService<Infrastructure.BackgroundJobs.TechnicalIndicatorCalculationJob>();
        services.AddHostedService<Infrastructure.BackgroundJobs.NewsCrawlerJob>();
        services.AddHostedService<Infrastructure.BackgroundJobs.EventCrawlerJob>();
        // AI Insight Generation Job - DISABLED to save tokens
        // Insights are now generated on-demand when users click the "Generate" button
        // services.AddHostedService<Infrastructure.BackgroundJobs.AIInsightGenerationJob>();

        return services;
    }

    /// <summary>
    /// Add health checks
    /// </summary>
    public static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Database=stock_investment;Username=postgres;Password=postgres";
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString,
                name: "postgresql",
                tags: new[] { "db", "sql", "postgresql" })
            .AddRedis(
                redisConnection,
                name: "redis",
                tags: new[] { "cache", "redis" });

        return services;
    }

    /// <summary>
    /// Configure middleware options
    /// </summary>
    public static IServiceCollection AddMiddlewareOptions(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Security Headers
        services.Configure<Middleware.SecurityHeadersOptions>(
            configuration.GetSection("SecurityHeaders"));

        // Configure Rate Limiting
        services.Configure<Middleware.RateLimitingOptions>(
            configuration.GetSection("RateLimiting"));

        return services;
    }
}
