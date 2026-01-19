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
    public DbSet<Layout> Layouts { get; set; } = null!;
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
    public DbSet<ChartSettings> ChartSettings { get; set; } = null!;
    public DbSet<Workspace> Workspaces { get; set; } = null!;
    public DbSet<WorkspaceMember> WorkspaceMembers { get; set; } = null!;
    public DbSet<WorkspaceMessage> WorkspaceMessages { get; set; } = null!;
    public DbSet<WorkspaceWatchlist> WorkspaceWatchlists { get; set; } = null!;
    public DbSet<WorkspaceLayout> WorkspaceLayouts { get; set; } = null!;
    public DbSet<NotificationChannelConfig> NotificationChannelConfigs { get; set; } = null!;

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
        modelBuilder.Entity<News>().ToTable("News");
        modelBuilder.Entity<FinancialReport>().ToTable("FinancialReports");
        modelBuilder.Entity<Layout>().ToTable("Layouts");
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

        // Configure Workspace relationships
        modelBuilder.Entity<Workspace>()
            .HasOne(w => w.Owner)
            .WithMany()
            .HasForeignKey(w => w.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(wm => wm.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(wm => wm.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(wm => wm.User)
            .WithMany()
            .HasForeignKey(wm => wm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkspaceMessage>()
            .HasOne(wm => wm.Workspace)
            .WithMany(w => w.Messages)
            .HasForeignKey(wm => wm.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkspaceMessage>()
            .HasOne(wm => wm.User)
            .WithMany()
            .HasForeignKey(wm => wm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkspaceWatchlist>()
            .HasOne(ww => ww.Workspace)
            .WithMany(w => w.Watchlists)
            .HasForeignKey(ww => ww.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkspaceWatchlist>()
            .HasOne(ww => ww.Watchlist)
            .WithMany()
            .HasForeignKey(ww => ww.WatchlistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkspaceLayout>()
            .HasOne(wl => wl.Workspace)
            .WithMany(w => w.Layouts)
            .HasForeignKey(wl => wl.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkspaceLayout>()
            .HasOne(wl => wl.Layout)
            .WithMany()
            .HasForeignKey(wl => wl.LayoutId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Workspace indexes
        modelBuilder.Entity<Workspace>()
            .HasIndex(w => w.OwnerId);

        modelBuilder.Entity<WorkspaceMember>()
            .HasIndex(wm => new { wm.WorkspaceId, wm.UserId })
            .IsUnique();

        modelBuilder.Entity<WorkspaceMessage>()
            .HasIndex(wm => wm.WorkspaceId);

        modelBuilder.Entity<WorkspaceWatchlist>()
            .HasIndex(ww => new { ww.WorkspaceId, ww.WatchlistId })
            .IsUnique();

        modelBuilder.Entity<WorkspaceLayout>()
            .HasIndex(wl => new { wl.WorkspaceId, wl.LayoutId })
            .IsUnique();

        // Configure table names
        modelBuilder.Entity<Workspace>().ToTable("Workspaces");
        modelBuilder.Entity<WorkspaceMember>().ToTable("WorkspaceMembers");
        modelBuilder.Entity<WorkspaceMessage>().ToTable("WorkspaceMessages");
        modelBuilder.Entity<WorkspaceWatchlist>().ToTable("WorkspaceWatchlists");
        modelBuilder.Entity<WorkspaceLayout>().ToTable("WorkspaceLayouts");

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
            .HasIndex(i => new { i.TickerId, i.Type, i.GeneratedAt });
        
        modelBuilder.Entity<AIInsight>()
            .HasIndex(i => i.DismissedAt)
            .HasFilter("[DismissedAt] IS NULL");

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

        modelBuilder.Entity<NotificationChannelConfig>()
            .Property(c => c.RowVersion)
            .IsRowVersion();
    }
}

