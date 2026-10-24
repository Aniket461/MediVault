using System.Security.Claims;
using MediVault.Api.Care;
using MediVault.Api.Contracts;
using MediVault.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediVault.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Patient))]
[Route("api/records")]
public sealed class RecordsController(CareService care) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<RecordResponse>> List(CancellationToken cancellationToken)
    {
        return await care.ListRecordsAsync(CurrentId(), cancellationToken);
    }

    [HttpPost]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] string? description, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var result = await care.UploadAsync(CurrentId(), description, file, cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Open(Guid id, CancellationToken cancellationToken)
    {
        var result = await care.OpenRecordAsync(CurrentId(), id, cancellationToken);
        return result.Succeeded
            ? File(result.Value.Content, result.Value.ContentType)
            : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateRecordBody body, CancellationToken cancellationToken)
    {
        var result = await care.UpdateDescriptionAsync(CurrentId(), id, body.Description, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await care.ArchiveRecordAsync(CurrentId(), id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var result = await care.RestoreRecordAsync(CurrentId(), id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await care.DeleteRecordAsync(CurrentId(), id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    private Guid CurrentId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
