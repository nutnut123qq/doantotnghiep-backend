using Microsoft.EntityFrameworkCore.Storage;
using StockInvestment.Application.Interfaces;
using StockInvestment.Infrastructure.Data.Repositories;

namespace StockInvestment.Infrastructure.Data;

/// <summary>
/// Unit of Work implementation for managing transactions
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<Type, object> _repositories;
    private IDbContextTransaction? _transaction;
    private IUserRepository? _userRepository;
    private IWatchlistRepository? _watchlistRepository;
    private IAlertRepository? _alertRepository;
    private IUserPreferenceRepository? _userPreferenceRepository;
    private ICorporateEventRepository? _corporateEventRepository;
    private IDataSourceRepository? _dataSourceRepository;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        _repositories = new Dictionary<Type, object>();
    }

    public IUserRepository Users
    {
        get
        {
            _userRepository ??= new UserRepository(_context);
            return _userRepository;
        }
    }

    public IWatchlistRepository Watchlists
    {
        get
        {
            _watchlistRepository ??= new WatchlistRepository(_context);
            return _watchlistRepository;
        }
    }

    public IAlertRepository Alerts
    {
        get
        {
            _alertRepository ??= new AlertRepository(_context);
            return _alertRepository;
        }
    }

    public IUserPreferenceRepository UserPreferences
    {
        get
        {
            _userPreferenceRepository ??= new UserPreferenceRepository(_context);
            return _userPreferenceRepository;
        }
    }

    public ICorporateEventRepository CorporateEvents
    {
        get
        {
            _corporateEventRepository ??= new CorporateEventRepository(_context);
            return _corporateEventRepository;
        }
    }

    public IDataSourceRepository DataSources
    {
        get
        {
            _dataSourceRepository ??= new DataSourceRepository(_context);
            return _dataSourceRepository;
        }
    }

    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        
        if (!_repositories.ContainsKey(type))
        {
            _repositories[type] = new Repository<T>(_context);
        }

        return (IRepository<T>)_repositories[type];
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            
            if (_transaction != null)
            {
                await _transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}

