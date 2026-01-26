using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Security.Claims;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Api.Attributes;

/// <summary>
/// Authorization attribute that restricts access to Admin users only
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Get user role from claims
        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
        
        if (string.IsNullOrEmpty(roleClaim))
        {
            context.Result = new ForbidResult();
            return;
        }

        var role = roleClaim.Trim();

        // Allow Admin or SuperAdmin (string-based for forward compatibility)
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Fallback: parse enum for legacy tokens
        if (!Enum.TryParse<UserRole>(role, true, out var userRole) || userRole != UserRole.Admin)
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}
