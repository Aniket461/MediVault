using System.Security.Claims;
using MediVault.Api.Auth;
using MediVault.Api.Contracts;
using MediVault.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, MediVaultDbContext db) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [RequestSizeLimit(40 * 1024 * 1024)]
    public async Task<IActionResult> Register([FromForm] RegisterRequest request, [FromForm] List<IFormFile>? documents, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, documents ?? [], cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return result.Succeeded
            ? Ok(result.Value)
            : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var id))
        {
            return Unauthorized(new MessageResponse("Sign in again."));
        }

        var user = await db.Users.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
        if (user is null || user.Status != Models.AccountStatus.Approved)
        {
            return Unauthorized(new MessageResponse("Sign in again."));
        }

        return UserResponse.From(user);
    }

    [HttpPut("paypal-email")]
    [Authorize(Roles = nameof(Models.UserRole.Doctor))]
    public async Task<IActionResult> SetPayPalEmail(PayPalEmailRequest request, CancellationToken cancellationToken)
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var id))
        {
            return Unauthorized(new MessageResponse("Sign in again."));
        }

        var result = await auth.SetPayPalEmailAsync(id, request.PayPalEmail, cancellationToken);
        return result.Succeeded
            ? Ok(result.Value)
            : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var id))
        {
            return Unauthorized(new MessageResponse("Sign in again."));
        }

        var result = await auth.UpdateProfileAsync(id, request, cancellationToken);
        return result.Succeeded
            ? Ok(result.Value)
            : StatusCode(result.Status, new MessageResponse(result.Error!));
    }
}
