using System.Linq.Expressions;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Specification pattern interface for building complex queries
/// </summary>
public interface ISpecification<T>
{
    /// <summary>
    /// Criteria for filtering entities
    /// </summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>
    /// Include expressions for eager loading
    /// </summary>
    List<Expression<Func<T, object>>> Includes { get; }

    /// <summary>
    /// Include strings for eager loading (for nested properties)
    /// </summary>
    List<string> IncludeStrings { get; }

    /// <summary>
    /// Order by expression
    /// </summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>
    /// Order by descending expression
    /// </summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>
    /// Pagination: number of items to skip
    /// </summary>
    int Skip { get; }

    /// <summary>
    /// Pagination: number of items to take
    /// </summary>
    int Take { get; }

    /// <summary>
    /// Whether pagination is enabled
    /// </summary>
    bool IsPagingEnabled { get; }
}

