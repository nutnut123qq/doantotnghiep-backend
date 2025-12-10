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
    }
}

