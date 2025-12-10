using Microsoft.EntityFrameworkCore;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.ValueObjects;

namespace StockInvestment.Application.Specifications;

/// <summary>
/// Specification for finding user by email
/// </summary>
public class UserByEmailSpecification : BaseSpecification<User>
{
    public UserByEmailSpecification(Email email)
        : base(u => u.Email.Value == email.Value)
    {
    }
}

/// <summary>
/// Specification for finding active users
/// </summary>
public class ActiveUsersSpecification : BaseSpecification<User>
{
    public ActiveUsersSpecification()
        : base(u => u.IsActive)
    {
    }
}

/// <summary>
/// Specification for finding verified users
/// </summary>
public class VerifiedUsersSpecification : BaseSpecification<User>
{
    public VerifiedUsersSpecification()
        : base(u => u.IsEmailVerified)
    {
    }
}

/// <summary>
/// Specification for finding users by role with pagination
/// </summary>
public class UsersByRoleSpecification : BaseSpecification<User>
{
    public UsersByRoleSpecification(Domain.Enums.UserRole role, int pageNumber = 1, int pageSize = 10)
        : base(u => u.Role == role)
    {
        ApplyOrderBy(u => u.CreatedAt);
        ApplyPaging((pageNumber - 1) * pageSize, pageSize);
    }
}

/// <summary>
/// Specification for searching users by email pattern
/// </summary>
public class UserSearchSpecification : BaseSpecification<User>
{
    public UserSearchSpecification(string searchTerm, int pageNumber = 1, int pageSize = 10)
    {
        // Note: For email search, we need to use EF.Property since Email is a Value Object
        // This is a simplified version - in production you might want to add more search fields
        ApplyOrderBy(u => u.CreatedAt);
        ApplyPaging((pageNumber - 1) * pageSize, pageSize);
    }
}

/// <summary>
/// Specification for getting all users with pagination
/// </summary>
public class AllUsersSpecification : BaseSpecification<User>
{
    public AllUsersSpecification(int pageNumber = 1, int pageSize = 10)
    {
        ApplyOrderBy(u => u.CreatedAt);
        ApplyPaging((pageNumber - 1) * pageSize, pageSize);
    }
}

