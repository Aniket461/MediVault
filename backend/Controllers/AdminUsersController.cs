using MediVault.Api.Auth;
using MediVault.Api.Contracts;
using MediVault.Api.Data;
using MediVault.Api.Models;
using MediVault.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/users")]
public sealed class AdminUsersController(MediVaultDbContext db, ObjectStorage storage, AccountService accounts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken cancellationToken)
    {
        var users = await db.Users
            .OrderBy(user => user.Status == AccountStatus.Pending ? 0 : 1)
            .ThenByDescending(user => user.CreatedAt)
            .ToListAsync(cancellationToken);

        return users.Select(UserResponse.From).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(account => account.Documents)
            .SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound(new MessageResponse("Account not found."));
        }

        var (spent, refunded, received) = await accounts.LifetimeAsync(user.Id, user.Role, cancellationToken);
        return UserDetailResponse.From(user, spent, refunded, received);
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> Document(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.Documents.SingleOrDefaultAsync(
            item => item.Id == documentId && item.UserId == id,
            cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var content = await storage.DownloadAsync(document.ObjectKey, cancellationToken);
        return File(content, document.ContentType);
    }

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken) =>
        SetStatus(id, AccountStatus.Approved, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken) =>
        SetStatus(id, AccountStatus.Rejected, cancellationToken);

    private async Task<IActionResult> SetStatus(Guid id, AccountStatus status, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound(new MessageResponse("Account not found."));
        }

        if (user.Role == UserRole.Admin)
        {
            return BadRequest(new MessageResponse("The admin account cannot be changed here."));
        }

        user.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(UserResponse.From(user));
    }
}
