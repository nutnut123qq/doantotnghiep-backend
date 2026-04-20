using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
/// Request model for setting user status
/// </summary>
public class SetUserStatusRequest
{
    public bool IsActive { get; set; }
}


