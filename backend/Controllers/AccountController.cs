using System.Security.Claims;
using MediVault.Api.Auth;
using MediVault.Api.Contracts;
using MediVault.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediVault.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController(AccountService accounts) : ControllerBase
{
    [HttpGet("me")]
    public Task<IActionResult> Me(CancellationToken cancellationToken) =>
        Profile(CallerId() ?? Guid.Empty, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        Profile(id, cancellationToken);

    private async Task<IActionResult> Profile(Guid targetId, CancellationToken cancellationToken)
    {
        if (CallerId() is not Guid callerId || !Enum.TryParse<UserRole>(User.FindFirstValue(ClaimTypes.Role), out var role))
        {
            return Unauthorized(new MessageResponse("Sign in again."));
        }

        var profile = await accounts.ProfileAsync(callerId, role, targetId, cancellationToken);
        return profile is null
            ? NotFound(new MessageResponse("Account not found."))
            : Ok(profile);
    }

    private Guid? CallerId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
