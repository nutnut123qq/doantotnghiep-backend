namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Unit of Work pattern for managing transactions across multiple repositories
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// User repository
    /// </summary>
    IUserRepository Users { get; }

    /// <summary>
    /// Generic repository for any entity type
    /// </summary>
    IRepository<T> Repository<T>() where T : class;

    /// <summary>
    /// Save all changes in a single transaction
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin a database transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

