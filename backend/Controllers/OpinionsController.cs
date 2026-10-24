using System.Security.Claims;
using MediVault.Api.Care;
using MediVault.Api.Contracts;
using MediVault.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediVault.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/opinions")]
public sealed class OpinionsController(CareService care) : ControllerBase
{
    [HttpGet("~/api/doctors")]
    [Authorize(Roles = nameof(UserRole.Patient))]
    public async Task<IReadOnlyList<DoctorOption>> Doctors(CancellationToken cancellationToken)
    {
        return await care.ListDoctorsAsync(cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryCurrent(out var id, out var role))
        {
            return Forbid();
        }

        return Ok(await care.ListOpinionsAsync(id, role, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Patient))]
    public async Task<IActionResult> Create(CreateOpinionBody body, CancellationToken cancellationToken)
    {
        var result = await care.CreateOpinionAsync(CurrentId(), body, cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPost("{id:guid}/seen")]
    public async Task<IActionResult> Seen(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCurrent(out var userId, out var role))
        {
            return Forbid();
        }

        var result = await care.AcknowledgeAsync(userId, role, id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Patient))]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await care.DeleteUnpaidAsync(CurrentId(), id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpGet("~/api/paypal/config")]
    [Authorize(Roles = nameof(UserRole.Patient))]
    public PayPalConfigResponse PayPalConfig() => care.PayPalConfig();

    [HttpPost("{id:guid}/paypal/order")]
    [Authorize(Roles = nameof(UserRole.Patient))]
    public async Task<IActionResult> PayPalOrder(Guid id, CancellationToken cancellationToken)
    {
        var result = await care.CreatePayPalOrderAsync(CurrentId(), id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPost("{id:guid}/paypal/capture")]
    [Authorize(Roles = nameof(UserRole.Patient))]
    public async Task<IActionResult> PayPalCapture(Guid id, CapturePayPalBody body, CancellationToken cancellationToken)
    {
        var result = await care.CapturePayPalAsync(CurrentId(), id, body.OrderId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPost("{id:guid}/aid")]
    [Authorize(Roles = nameof(UserRole.Doctor))]
    public async Task<IActionResult> Aid(Guid id, [FromQuery] bool refresh, CancellationToken cancellationToken)
    {
        var result = await care.PrepareAidAsync(CurrentId(), id, refresh, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPost("{id:guid}/decline")]
    [Authorize(Roles = nameof(UserRole.Doctor))]
    public async Task<IActionResult> Decline(Guid id, CancellationToken cancellationToken)
    {
        var result = await care.DeclineAsync(CurrentId(), id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpPost("{id:guid}/opinion")]
    [Authorize(Roles = nameof(UserRole.Doctor))]
    public async Task<IActionResult> Answer(Guid id, AnswerBody body, CancellationToken cancellationToken)
    {
        var result = await care.AnswerAsync(CurrentId(), id, body.Opinion, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    [HttpGet("{id:guid}/records/{recordId:guid}")]
    public async Task<IActionResult> OpenFile(Guid id, Guid recordId, CancellationToken cancellationToken)
    {
        if (!TryCurrent(out var userId, out var role))
        {
            return Forbid();
        }

        var result = await care.OpenOpinionFileAsync(userId, role, id, recordId, cancellationToken);
        return result.Succeeded
            ? File(result.Value.Content, result.Value.ContentType)
            : StatusCode(result.Status, new MessageResponse(result.Error!));
    }

    private Guid CurrentId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool TryCurrent(out Guid id, out UserRole role)
    {
        id = Guid.Empty;
        role = default;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id)
            && Enum.TryParse(User.FindFirstValue(ClaimTypes.Role), out role)
            && role is UserRole.Patient or UserRole.Doctor;
    }
}
