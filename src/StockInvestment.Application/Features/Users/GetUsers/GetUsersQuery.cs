using MediatR;

namespace StockInvestment.Application.Features.Users.GetUsers;

/// <summary>
/// Query to get paginated list of users
/// </summary>
public class GetUsersQuery : IRequest<GetUsersDto>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// DTO for user list response
/// </summary>
public class GetUsersDto
{
    public List<UserItemDto> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// DTO for individual user in list
/// </summary>
public class UserItemDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}

