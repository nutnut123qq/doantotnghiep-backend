using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController : ControllerBase
{
    // Intentionally left empty.
    // Endpoints were decomposed into bounded controllers under the same "api/admin/*" route.
}

/// <summary>
/// Request model for updating user role
/// </summary>
public class UpdateUserRoleRequest
{
    public UserRole NewRole { get; set; }
}

/// <summary>
/// Request model for setting user status
/// </summary>
public class SetUserStatusRequest
{
    public bool IsActive { get; set; }
}

/// <summary>
/// Request model for creating a user
/// </summary>
public class CreateUserRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FullName { get; set; }
    public UserRole Role { get; set; }
}

/// <summary>
/// Request model for updating a user
/// </summary>
public class UpdateUserRequest
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public UserRole? Role { get; set; }
}

/// <summary>
/// Request model for resetting password
/// </summary>
public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = null!;
}
