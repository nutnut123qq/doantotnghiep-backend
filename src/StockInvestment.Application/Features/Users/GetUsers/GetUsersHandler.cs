using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Specifications;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Users.GetUsers;

/// <summary>
/// Handler for getting paginated list of users using Specification pattern with caching
/// </summary>
public class GetUsersHandler : IRequestHandler<GetUsersQuery, GetUsersDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetUsersHandler> _logger;

    public GetUsersHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<GetUsersHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<GetUsersDto> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        // Generate cache key
        var cacheKey = $"users:page:{request.PageNumber}:size:{request.PageSize}:role:{request.Role}:active:{request.IsActive}";

        // Try to get from cache
        return await _cacheService.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                // Build specification based on filters
                ISpecification<Domain.Entities.User> specification;

                if (!string.IsNullOrEmpty(request.Role) && Enum.TryParse<UserRole>(request.Role, out var role))
                {
                    specification = new UsersByRoleSpecification(role, request.PageNumber, request.PageSize);
                }
                else if (request.IsActive.HasValue && request.IsActive.Value)
                {
                    specification = new ActiveUsersSpecification();
                }
                else
                {
                    // Default: get all users with pagination
                    specification = new AllUsersSpecification(request.PageNumber, request.PageSize);
                }

                // Get users using specification
                var users = await _unitOfWork.Users.GetAsync(specification, cancellationToken);

                // Get total count (without pagination)
                var totalCount = await _unitOfWork.Users.CountAsync(cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Retrieved {Count} users from database (Page {PageNumber}/{TotalPages})",
                    users.Count(),
                    request.PageNumber,
                    (int)Math.Ceiling(totalCount / (double)request.PageSize));

                return new GetUsersDto
                {
                    Users = users.Select(u => new UserItemDto
                    {
                        Id = u.Id,
                        Email = u.Email.Value,
                        Role = u.Role.ToString(),
                        IsActive = u.IsActive,
                        IsEmailVerified = u.IsEmailVerified,
                        CreatedAt = u.CreatedAt
                    }).ToList(),
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };
            },
            TimeSpan.FromMinutes(5), // Cache for 5 minutes
            cancellationToken);
    }
}

