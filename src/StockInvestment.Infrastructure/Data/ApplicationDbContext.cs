using Microsoft.EntityFrameworkCore;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<StockTicker> StockTickers { get; set; } = null!;
    public DbSet<Watchlist> Watchlists { get; set; } = null!;
    public DbSet<Alert> Alerts { get; set; } = null!;
    public DbSet<News> News { get; set; } = null!;
    public DbSet<FinancialReport> FinancialReports { get; set; } = null!;
    public DbSet<TechnicalIndicator> TechnicalIndicators { get; set; } = null!;
    public DbSet<UserPreference> UserPreferences { get; set; } = null!;
    public DbSet<CorporateEvent> CorporateEvents { get; set; } = null!;
    public DbSet<AnalyticsEvent> AnalyticsEvents { get; set; } = null!;
    public DbSet<DataSource> DataSources { get; set; } = null!;
    public DbSet<AIModelConfig> AIModelConfigs { get; set; } = null!;
    public DbSet<AIModelPerformance> AIModelPerformances { get; set; } = null!;
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public DbSet<PushNotificationConfig> PushNotificationConfigs { get; set; } = null!;
    public DbSet<AIInsight> AIInsights { get; set; } = null!;
    public DbSet<Portfolio> Portfolios { get; set; } = null!;
    public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; } = null!;
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
    public DbSet<ChartSettings> ChartSettings { get; set; } = null!;
    public DbSet<NotificationChannelConfig> NotificationChannelConfigs { get; set; } = null!;
    public DbSet<AnalysisReport> AnalysisReports { get; set; } = null!;
    public DbSet<AdminAuditLog> AdminAuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships
        modelBuilder.Entity<Watchlist>()
            .HasMany(w => w.Tickers)
            .WithMany(t => t.Watchlists)
            .UsingEntity(j => j.ToTable("WatchlistTickers"));

        // Configure value objects - Email stored as string
        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasConversion(
                e => e.Value,
                v => Domain.ValueObjects.Email.Create(v))
            .HasColumnName("Email")
            .IsRequired()
            .HasMaxLength(255);

        // Configure indexes
        modelBuilder.Entity<StockTicker>()
            .HasIndex(t => t.Symbol)
            .IsUnique();

        // Note: Index on Email.Value needs to be configured differently
        // Since Email is a value object, we'll create index on the converted value
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        // Configure table names
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<StockTicker>().ToTable("StockTickers");
        modelBuilder.Entity<Watchlist>().ToTable("Watchlists");
        modelBuilder.Entity<Alert>().ToTable("Alerts");
        
        // P1-1: Configure indexes for Alerts table for performance
        // Index for AlertMonitorJob query: WHERE IsActive = true AND TickerId IS NOT NULL
        modelBuilder.Entity<Alert>()
            .HasIndex(a => new { a.IsActive, a.TriggeredAt })
            .HasDatabaseName("IX_Alerts_IsActive_TriggeredAt");
        
        // Index for user queries: WHERE UserId = X AND IsActive = Y
        modelBuilder.Entity<Alert>()
            .HasIndex(a => new { a.UserId, a.IsActive })
            .HasDatabaseName("IX_Alerts_UserId_IsActive");
        
        // Index for ticker queries: WHERE TickerId = X AND IsActive = Y
        modelBuilder.Entity<Alert>()
            .HasIndex(a => new { a.TickerId, a.IsActive })
            .HasDatabaseName("IX_Alerts_TickerId_IsActive");
        
        modelBuilder.Entity<News>().ToTable("News");
        
        // P1-1: Unique index on News.Url for multi-instance dedupe
        // Note: For case-insensitive unique index on lower("Url"), we'll use raw SQL in migration
        // This configuration creates a regular unique index; migration will enhance it with lower()
        modelBuilder.Entity<News>()
            .HasIndex(n => n.Url)
            .IsUnique()
            .HasFilter("\"Url\" IS NOT NULL")
            .HasDatabaseName("IX_News_Url_Unique");
        
        // P1-1: Index on News.PublishedAt for query performance (sorting/filtering by date)
        modelBuilder.Entity<News>()
            .HasIndex(n => n.PublishedAt)
            .HasDatabaseName("IX_News_PublishedAt");
        
        modelBuilder.Entity<FinancialReport>().ToTable("FinancialReports");
        modelBuilder.Entity<FinancialReport>()
            .HasIndex(fr => fr.IsDeleted);
        modelBuilder.Entity<TechnicalIndicator>().ToTable("TechnicalIndicators");
        modelBuilder.Entity<UserPreference>().ToTable("UserPreferences");
        modelBuilder.Entity<DataSource>().ToTable("DataSources");
        modelBuilder.Entity<AIModelConfig>().ToTable("AIModelConfigs");
        modelBuilder.Entity<AIModelPerformance>().ToTable("AIModelPerformances");
        modelBuilder.Entity<NotificationTemplate>().ToTable("NotificationTemplates");
        modelBuilder.Entity<PushNotificationConfig>().ToTable("PushNotificationConfigs");
        modelBuilder.Entity<AIInsight>().ToTable("AIInsights");
        modelBuilder.Entity<Portfolio>().ToTable("Portfolios");
        modelBuilder.Entity<ChartSettings>().ToTable("ChartSettings");

        // Configure Portfolio relationship with User
        modelBuilder.Entity<Portfolio>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Portfolio indexes
        modelBuilder.Entity<Portfolio>()
            .HasIndex(p => p.UserId);
        
        modelBuilder.Entity<Portfolio>()
            .HasIndex(p => p.Symbol);
        
        modelBuilder.Entity<Portfolio>()
            .HasIndex(p => new { p.UserId, p.Symbol });

        // Configure ChartSettings relationship with User
        modelBuilder.Entity<ChartSettings>()
            .HasOne(cs => cs.User)
            .WithMany()
            .HasForeignKey(cs => cs.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ChartSettings indexes
        modelBuilder.Entity<ChartSettings>()
            .HasIndex(cs => cs.UserId);
        
        modelBuilder.Entity<ChartSettings>()
            .HasIndex(cs => new { cs.UserId, cs.Symbol })
            .IsUnique();

        // Configure CorporateEvent inheritance (TPH - Table Per Hierarchy)
        modelBuilder.Entity<CorporateEvent>()
            .ToTable("CorporateEvents")
            .HasDiscriminator<CorporateEventType>("EventType")
            .HasValue<EarningsEvent>(CorporateEventType.Earnings)
            .HasValue<DividendEvent>(CorporateEventType.Dividend)
            .HasValue<StockSplitEvent>(CorporateEventType.StockSplit)
            .HasValue<AGMEvent>(CorporateEventType.AGM)
            .HasValue<RightsIssueEvent>(CorporateEventType.RightsIssue);

        // Configure CorporateEvent indexes
        modelBuilder.Entity<CorporateEvent>()
            .HasIndex(e => e.StockTickerId);
        
        modelBuilder.Entity<CorporateEvent>()
            .HasIndex(e => e.EventDate);
        
        modelBuilder.Entity<CorporateEvent>()
            .HasIndex(e => new { e.StockTickerId, e.EventDate });

        modelBuilder.Entity<CorporateEvent>()
            .HasIndex(e => e.IsDeleted);

        // Configure UserPreference indexes
        modelBuilder.Entity<UserPreference>()
            .HasIndex(p => new { p.UserId, p.PreferenceKey })
            .IsUnique();

        // Configure AIInsight indexes
        modelBuilder.Entity<AIInsight>()
            .HasIndex(i => i.TickerId);
        
        modelBuilder.Entity<AIInsight>()
            .HasIndex(i => i.Type);
        
        modelBuilder.Entity<AIInsight>()
            .HasIndex(i => i.GeneratedAt);

        modelBuilder.Entity<AIInsight>()
            .HasIndex(i => i.IsDeleted);
        
        modelBuilder.Entity<AIInsight>()
            .HasIndex(i => new { i.TickerId, i.Type, i.GeneratedAt });
        
        modelBuilder.Entity<AIInsight>()
            .HasIndex(i => i.DismissedAt)
            .HasFilter("\"DismissedAt\" IS NULL");

        // Configure EmailVerificationToken
        modelBuilder.Entity<EmailVerificationToken>()
            .ToTable("EmailVerificationTokens");

        modelBuilder.Entity<EmailVerificationToken>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmailVerificationToken>()
            .HasIndex(t => t.Token)
            .IsUnique();

        modelBuilder.Entity<EmailVerificationToken>()
            .HasIndex(t => t.UserId);

        modelBuilder.Entity<EmailVerificationToken>()
            .HasIndex(t => t.ExpiresAt);

        // Configure PasswordResetToken
        modelBuilder.Entity<PasswordResetToken>()
            .ToTable("PasswordResetTokens");

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.Token)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.UserId);

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.ExpiresAt);

        // Configure NotificationChannelConfig
        modelBuilder.Entity<NotificationChannelConfig>()
            .ToTable("NotificationChannelConfigs");

        modelBuilder.Entity<NotificationChannelConfig>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NotificationChannelConfig>()
            .HasIndex(c => c.UserId)
            .IsUnique();

        // PostgreSQL-native optimistic concurrency token.
        // Using xmin avoids maintaining a custom bytea row version column.
        modelBuilder.Entity<NotificationChannelConfig>()
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Configure AnalysisReport table and indexes
        modelBuilder.Entity<AnalysisReport>()
            .ToTable("AnalysisReports");

        // Content column as TEXT (unlimited length)
        modelBuilder.Entity<AnalysisReport>()
            .Property(ar => ar.Content)
            .HasColumnType("text");

        // Index on Symbol for filtering
        modelBuilder.Entity<AnalysisReport>()
            .HasIndex(ar => ar.Symbol);

        // Index on PublishedAt for sorting
        modelBuilder.Entity<AnalysisReport>()
            .HasIndex(ar => ar.PublishedAt);

        // Composite index on Symbol + PublishedAt (descending) for list queries
        modelBuilder.Entity<AnalysisReport>()
            .HasIndex(ar => new { ar.Symbol, ar.PublishedAt })
            .IsDescending(false, true); // Symbol ascending, PublishedAt descending

        // Configure AdminAuditLog
        modelBuilder.Entity<AdminAuditLog>()
            .ToTable("AdminAuditLogs");

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(a => a.AdminUserId);

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(a => a.TargetUserId);

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(a => a.CreatedAt);
    }
}


