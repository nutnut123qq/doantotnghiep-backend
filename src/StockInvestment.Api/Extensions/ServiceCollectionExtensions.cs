using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Mappings;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Infrastructure.Data.Repositories;
using StockInvestment.Infrastructure.External;
using StockInvestment.Infrastructure.Messaging;
using StockInvestment.Infrastructure.Messaging.MessageHandlers;
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
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace StockInvestment.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add core API services (Controllers, Swagger, SignalR, CORS)
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // P0-4: Configure ForwardedHeadersOptions for trusted proxies
        // This ensures X-Forwarded-For and X-Real-IP headers are only trusted from known proxies
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XRealIp;
            
            // Clear default trusted proxies (security: don't trust all by default)
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
            
            // Allow configuration of trusted proxies via appsettings
            var trustedProxies = configuration.GetSection("TrustedProxies:IPs").Get<string[]>();
            if (trustedProxies != null)
            {
                foreach (var proxyIp in trustedProxies)
                {
                    if (IPAddress.TryParse(proxyIp, out var ip))
                    {
                        options.KnownProxies.Add(ip);
                    }
                }
            }
            
            // Allow configuration of trusted networks via CIDR notation
            var trustedNetworks = configuration.GetSection("TrustedProxies:Networks").Get<string[]>();
            if (trustedNetworks != null)
            {
                foreach (var network in trustedNetworks)
                {
                    var parts = network.Split('/');
                    if (parts.Length == 2 
                        && IPAddress.TryParse(parts[0], out var networkIp)
                        && int.TryParse(parts[1], out var prefixLength))
                    {
                        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(networkIp, prefixLength));
                    }
                }
            }
            
            // In development, allow localhost (for testing behind reverse proxy)
            if (configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
                options.KnownProxies.Add(IPAddress.Parse("::1"));
            }
            
            // Limit the number of entries in forwarded headers to prevent header injection
            options.ForwardLimit = 1;
            
            // Require that all forwarded headers are from known proxies
            options.RequireHeaderSymmetry = false; // Set to true if all headers must match
        });
        
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
        // P0-1: Fail-fast if JWT secret is missing or too short
        var jwtSecret = configuration["JWT:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException(
                "JWT:Secret is required. Please set it in configuration (appsettings.json or environment variable). " +
                "Minimum length: 32 characters for security.");
        }

        if (jwtSecret.Length < 32)
        {
            throw new InvalidOperationException(
                $"JWT:Secret must be at least 32 characters long. Current length: {jwtSecret.Length}. " +
                "Please set a stronger secret in configuration.");
        }

        // Check if using default/example secret (security risk)
        var defaultSecrets = new[]
        {
            "your-super-secret-key-change-in-production-min-32-chars",
            "YOUR_JWT_SECRET_KEY_MIN_32_CHARACTERS_LONG_CHANGE_THIS"
        };
        if (defaultSecrets.Contains(jwtSecret))
        {
            throw new InvalidOperationException(
                "JWT:Secret cannot use the default/example value. Please set a unique, strong secret in configuration.");
        }

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

        // P0-1: Add authorization policies
        services.AddAuthorization(options =>
        {
            // Admin policy - requires Admin role
            options.AddPolicy("Admin", policy =>
                policy.RequireRole("Admin", "SuperAdmin"));

            // AnalystOrAdmin policy - requires Admin or Analyst role (Analyst can be added later)
            options.AddPolicy("AnalystOrAdmin", policy =>
                policy.RequireRole("Admin", "SuperAdmin", "Analyst"));
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

        // P1-2: Register distributed lock factory (transient per job execution)
        services.AddTransient<Func<IDistributedLock>>(sp =>
        {
            return () => new Infrastructure.Services.RedisDistributedLock(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<ILogger<Infrastructure.Services.RedisDistributedLock>>());
        });

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
        services.AddScoped<IChartSettingsRepository, ChartSettingsRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
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
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<INotificationChannelService, NotificationChannelService>();
        
        // Analysis Reports Q&A service (V1 Minimal - NO RAG)
        services.AddScoped<IAnalysisReportQAService, AnalysisReportQAService>();
        services.AddScoped<IAnalysisReportService, AnalysisReportService>(); // P2-1: Add AnalysisReportService

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

        // Configure NotificationChannel Options
        services.Configure<Infrastructure.Configuration.NotificationChannelOptions>(
            configuration.GetSection("NotificationChannels"));

        // Configure HTTP Clients for Notification Channels with Polly retry
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(1));

        services.AddHttpClient("Slack")
            .AddPolicyHandler(retryPolicy)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });

        services.AddHttpClient("Telegram")
            .AddPolicyHandler(retryPolicy)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });

        // Configure HTTP Client for Event Crawler
        services.AddHttpClient("EventCrawler", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // P0-2 SSRF: Dedicated HttpClient for DataSource TestConnection with auto-redirect DISABLED
        // Redirects will be manually followed with validation at each step
        services.AddHttpClient("DataSourceTestConnection", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false // P0-2: Disable auto-redirect to validate each redirect URL
        });

        // Register notification channel senders
        services.AddTransient<INotificationChannelSender>(sp =>
            new Infrastructure.Services.NotificationChannels.SlackNotificationSender(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Slack"),
                sp.GetRequiredService<ILogger<Infrastructure.Services.NotificationChannels.SlackNotificationSender>>()));

        services.AddTransient<INotificationChannelSender>(sp =>
            new Infrastructure.Services.NotificationChannels.TelegramNotificationSender(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Telegram"),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Configuration.NotificationChannelOptions>>(),
                sp.GetRequiredService<ILogger<Infrastructure.Services.NotificationChannels.TelegramNotificationSender>>()));

        return services;
    }

    /// <summary>
    /// Add messaging services (RabbitMQ consumers)
    /// </summary>
    public static IServiceCollection AddMessagingServices(this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("RabbitMQ:Enabled");
        if (!enabled)
        {
            return services;
        }

        var connectionString = configuration.GetConnectionString("RabbitMQ");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("RabbitMQ is enabled but ConnectionStrings:RabbitMQ is not configured.");
        }

        services.AddSingleton<RabbitMQService>(sp =>
            new RabbitMQService(
                sp.GetRequiredService<ILogger<RabbitMQService>>(),
                connectionString));

        services.AddSingleton<NewsSummarizeHandler>(sp =>
            new NewsSummarizeHandler(
                sp.GetRequiredService<RabbitMQService>().Channel,
                sp.GetRequiredService<IAIService>(),
                sp.GetRequiredService<INewsService>(),
                sp.GetRequiredService<ILogger<NewsSummarizeHandler>>()));

        services.AddHostedService<NewsSummarizeConsumerService>();

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

        // P2-1: Add health checks with "ready" tag for /health/ready endpoint
        services.AddHealthChecks()
            .AddNpgSql(
                connectionString,
                name: "postgresql",
                tags: new[] { "db", "sql", "postgresql", "ready" }) // P2-1: Add "ready" tag
            .AddRedis(
                redisConnection,
                name: "redis",
                tags: new[] { "cache", "redis", "ready" }); // P2-1: Add "ready" tag

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
