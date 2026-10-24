using MediVault.Api.Auth;
using MediVault.Api.Contracts;
using MediVault.Api.Data;
using MediVault.Api.Models;
using MediVault.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Api.Care;

public sealed class CareService(MediVaultDbContext db, ObjectStorage storage, PayPalService paypal, ClinicalAidService clinicalAid, ILogger<CareService> logger)
{
    private const decimal PlatformFeeRate = 0.05m;
    private static readonly HashSet<string> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public async Task<IReadOnlyList<RecordResponse>> ListRecordsAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var records = await db.Records
            .Where(record => record.PatientId == patientId)
            .OrderByDescending(record => record.CreatedAt)
            .Select(record => new RecordResponse(
                record.Id,
                record.FileName,
                record.Description,
                record.CreatedAt,
                record.RequestFiles.Any(),
                record.ArchivedAt != null))
            .ToListAsync(cancellationToken);
        return records;
    }

    public async Task<ServiceResult<RecordResponse>> UploadAsync(
        Guid patientId,
        string? description,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var text = description?.Trim() ?? "";
        if (text.Length is < 1 or > 400)
        {
            return ServiceResult<RecordResponse>.Fail(StatusCodes.Status400BadRequest, "Describe this file.");
        }

        if (file is null || file.Length is <= 0 or > 5 * 1024 * 1024 || !AllowedFiles.Contains(file.ContentType))
        {
            return ServiceResult<RecordResponse>.Fail(StatusCodes.Status400BadRequest, "Upload a PDF or image under 5 MB.");
        }

        var record = new MedicalRecord
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            Description = text,
            ObjectKey = $"records/{patientId}/{Guid.NewGuid()}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await using var stream = file.OpenReadStream();
        await storage.UploadAsync(record.ObjectKey, stream, record.ContentType, cancellationToken);
        db.Records.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<RecordResponse>.Ok(ToResponse(record, false));
    }

    public async Task<ServiceResult<(MemoryStream Content, string ContentType)>> OpenRecordAsync(
        Guid patientId,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        var record = await db.Records.SingleOrDefaultAsync(
            item => item.Id == recordId && item.PatientId == patientId,
            cancellationToken);
        if (record is null)
        {
            return ServiceResult<(MemoryStream, string)>.Fail(StatusCodes.Status404NotFound, "File not found.");
        }

        var content = await storage.DownloadAsync(record.ObjectKey, cancellationToken);
        return ServiceResult<(MemoryStream, string)>.Ok((content, record.ContentType));
    }

    public async Task<ServiceResult<MessageResponse>> DeleteRecordAsync(Guid patientId, Guid recordId, CancellationToken cancellationToken)
    {
        var record = await db.Records
            .Include(item => item.RequestFiles)
            .SingleOrDefaultAsync(item => item.Id == recordId && item.PatientId == patientId, cancellationToken);
        if (record is null)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status404NotFound, "File not found.");
        }

        if (record.RequestFiles.Count > 0)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status409Conflict, "This file is part of a request.");
        }

        db.Records.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(record.ObjectKey, cancellationToken);
        return ServiceResult<MessageResponse>.Ok(new MessageResponse("Deleted."));
    }

    public async Task<ServiceResult<RecordResponse>> UpdateDescriptionAsync(
        Guid patientId,
        Guid recordId,
        string? description,
        CancellationToken cancellationToken)
    {
        var record = await OwnedRecordAsync(patientId, recordId, cancellationToken);
        if (record is null)
        {
            return ServiceResult<RecordResponse>.Fail(StatusCodes.Status404NotFound, "File not found.");
        }

        var text = description?.Trim() ?? "";
        if (text.Length is < 1 or > 400)
        {
            return ServiceResult<RecordResponse>.Fail(StatusCodes.Status400BadRequest, "Describe this file.");
        }

        record.Description = text;
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<RecordResponse>.Ok(ToResponse(record, record.RequestFiles.Count > 0));
    }

    public async Task<ServiceResult<RecordResponse>> ArchiveRecordAsync(Guid patientId, Guid recordId, CancellationToken cancellationToken)
    {
        var record = await OwnedRecordAsync(patientId, recordId, cancellationToken);
        if (record is null)
        {
            return ServiceResult<RecordResponse>.Fail(StatusCodes.Status404NotFound, "File not found.");
        }

        if (record.RequestFiles.Count == 0)
        {
            return ServiceResult<RecordResponse>.Fail(StatusCodes.Status409Conflict, "This file is not part of a request. You can delete it.");
        }

        record.ArchivedAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<RecordResponse>.Ok(ToResponse(record, true));
    }

    public async Task<ServiceResult<RecordResponse>> RestoreRecordAsync(Guid patientId, Guid recordId, CancellationToken cancellationToken)
    {
        var record = await OwnedRecordAsync(patientId, recordId, cancellationToken);
        if (record is null)
        {
            return ServiceResult<RecordResponse>.Fail(StatusCodes.Status404NotFound, "File not found.");
        }

        record.ArchivedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<RecordResponse>.Ok(ToResponse(record, record.RequestFiles.Count > 0));
    }

    public async Task<IReadOnlyList<DoctorOption>> ListDoctorsAsync(CancellationToken cancellationToken)
    {
        return await db.Users
            .Where(user => user.Role == UserRole.Doctor && user.Status == AccountStatus.Approved && user.SecondOpinionFee != null)
            .OrderBy(user => user.FullName)
            .Select(user => new DoctorOption(user.Id, user.FullName, user.Hospital, user.SecondOpinionFee!.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OpinionResponse>> ListOpinionsAsync(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        await ExpireOverdueAsync(cancellationToken);
        var query = Details();
        query = role == UserRole.Doctor
            ? query.Where(request => request.DoctorId == userId && request.Status != OpinionStatus.AwaitingPayment)
            : query.Where(request => request.PatientId == userId);

        var requests = await query.OrderByDescending(request => request.CreatedAt).ToListAsync(cancellationToken);
        var includeAid = role == UserRole.Doctor;
        return requests.Select(request => OpinionResponse.From(request, includeAid) with { Notice = NoticeFor(request, role) }).ToList();
    }

    public async Task<ServiceResult<OpinionResponse>> CreateOpinionAsync(Guid patientId, CreateOpinionBody body, CancellationToken cancellationToken)
    {
        var message = body.Message?.Trim() ?? "";
        var recordIds = body.RecordIds.Distinct().ToList();
        if (message.Length is < 1 or > 2000)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status400BadRequest, "Write what you want the doctor to know.");
        }

        var intake = new ClinicalIntakeData
        {
            Symptoms = body.Symptoms,
            Feeling = body.Feeling,
            Duration = body.Duration,
            Intensity = body.Intensity,
            Consulted = body.Consulted,
            ConsultedNote = body.ConsultedNote
        };
        var intakeError = ClinicalIntakeData.Validate(intake);
        if (intakeError is not null)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status400BadRequest, intakeError);
        }

        if (recordIds.Count is < 1 or > 10)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status400BadRequest, "Choose one to ten files.");
        }

        var doctor = await db.Users.SingleOrDefaultAsync(
            user => user.Id == body.DoctorId && user.Role == UserRole.Doctor && user.Status == AccountStatus.Approved,
            cancellationToken);
        if (doctor?.SecondOpinionFee is null or <= 0)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status400BadRequest, "Choose an approved doctor.");
        }

        var records = await db.Records
            .Where(record => record.PatientId == patientId && recordIds.Contains(record.Id))
            .ToListAsync(cancellationToken);
        if (records.Count != recordIds.Count || records.Any(record => record.ArchivedAt is not null))
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status400BadRequest, "Choose files from your records.");
        }

        var request = new OpinionRequest
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctor.Id,
            Message = message,
            Intake = intake.Write(),
            Fee = decimal.Round(doctor.SecondOpinionFee.Value, 2, MidpointRounding.AwayFromZero),
            Status = OpinionStatus.AwaitingPayment,
            PatientSeenStatus = nameof(OpinionStatus.AwaitingPayment),
            DoctorSeenStatus = nameof(OpinionStatus.AwaitingPayment),
            CreatedAt = DateTimeOffset.UtcNow,
            Doctor = doctor,
            Files = records.Select(record => new OpinionRequestFile { RecordId = record.Id, Record = record }).ToList()
        };
        var patient = await db.Users.SingleAsync(user => user.Id == patientId, cancellationToken);
        request.Patient = patient;
        db.Opinions.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<OpinionResponse>.Ok(OpinionResponse.From(request));
    }

    public async Task<ServiceResult<MessageResponse>> DeleteUnpaidAsync(Guid patientId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.Opinions.SingleOrDefaultAsync(
            item => item.Id == requestId && item.PatientId == patientId,
            cancellationToken);
        if (request is null)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status404NotFound, "Request not found.");
        }

        if (request.Status != OpinionStatus.AwaitingPayment || request.PayPalCaptureId is not null)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status409Conflict, "Only an unpaid request can be deleted.");
        }

        db.Opinions.Remove(request);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<MessageResponse>.Ok(new MessageResponse("Request deleted."));
    }

    public PayPalConfigResponse PayPalConfig() =>
        new(paypal.IsConfigured, paypal.IsConfigured ? paypal.ClientId : "", paypal.Currency);

    public async Task<ServiceResult<PayPalOrderResponse>> CreatePayPalOrderAsync(Guid patientId, Guid requestId, CancellationToken cancellationToken)
    {
        if (!paypal.IsConfigured)
        {
            return ServiceResult<PayPalOrderResponse>.Fail(StatusCodes.Status503ServiceUnavailable, "PayPal is not configured.");
        }

        var request = await OwnedAwaitingAsync(patientId, requestId, cancellationToken);
        if (request is null)
        {
            return ServiceResult<PayPalOrderResponse>.Fail(StatusCodes.Status404NotFound, "Request not found.");
        }

        try
        {
            var orderId = await paypal.CreateOrderAsync(request.Id, request.Fee, cancellationToken);
            request.PayPalOrderId = orderId;
            await db.SaveChangesAsync(cancellationToken);
            return ServiceResult<PayPalOrderResponse>.Ok(new PayPalOrderResponse(orderId));
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult<PayPalOrderResponse>.Fail(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    public async Task<ServiceResult<OpinionResponse>> CapturePayPalAsync(
        Guid patientId,
        Guid requestId,
        string? orderId,
        CancellationToken cancellationToken)
    {
        if (!paypal.IsConfigured)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status503ServiceUnavailable, "PayPal is not configured.");
        }

        var request = await OwnedAwaitingAsync(patientId, requestId, cancellationToken);
        if (request is null)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status404NotFound, "Request not found.");
        }

        if (string.IsNullOrWhiteSpace(orderId) || orderId != request.PayPalOrderId)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status400BadRequest, "This PayPal payment does not match the request.");
        }

        PayPalCapture capture;
        try
        {
            capture = await paypal.CaptureAsync(orderId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status502BadGateway, ex.Message);
        }

        var amountMatches = decimal.Round(capture.Amount, 2, MidpointRounding.AwayFromZero) == request.Fee;
        var currencyMatches = capture.Currency.Equals(paypal.Currency, StringComparison.OrdinalIgnoreCase);
        var referenceMatches = capture.ReferenceId == request.Id.ToString();
        if (!amountMatches || !currencyMatches || !referenceMatches)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status400BadRequest, "This PayPal payment does not match the request.");
        }

        request.Status = OpinionStatus.Open;
        request.PatientSeenStatus = nameof(OpinionStatus.Open);
        request.PaidAt = DateTimeOffset.UtcNow;
        request.ReplyBy = request.PaidAt.Value.AddDays(7);
        request.PayPalCaptureId = capture.CaptureId;
        await db.SaveChangesAsync(cancellationToken);
        await TryPrepareAidAsync(request, cancellationToken);
        return ServiceResult<OpinionResponse>.Ok(OpinionResponse.From(request));
    }

    public async Task<ServiceResult<MessageResponse>> AcknowledgeAsync(Guid userId, UserRole role, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.Opinions.SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status404NotFound, "Request not found.");
        }

        if (role == UserRole.Patient && request.PatientId == userId)
        {
            request.PatientSeenStatus = request.Status.ToString();
        }
        else if (role == UserRole.Doctor && request.DoctorId == userId && request.Status != OpinionStatus.AwaitingPayment)
        {
            request.DoctorSeenStatus = request.Status.ToString();
        }
        else
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status404NotFound, "Request not found.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<MessageResponse>.Ok(new MessageResponse("Noted."));
    }

    private static string? NoticeFor(OpinionRequest request, UserRole role)
    {
        if (role == UserRole.Patient)
        {
            if (request.PatientSeenStatus == request.Status.ToString())
            {
                return null;
            }

            if (request.Status == OpinionStatus.Answered)
            {
                return $"{request.Doctor.FullName} replied.";
            }

            if (request.Status == OpinionStatus.Refunded && request.RefundCause == RefundCause.Declined)
            {
                return $"{request.Doctor.FullName} declined this request. Your payment was refunded.";
            }

            if (request.Status == OpinionStatus.Refunded)
            {
                return $"{request.Doctor.FullName} did not reply within 7 days. Your payment was refunded.";
            }
        }

        if (role == UserRole.Doctor && request.DoctorSeenStatus != request.Status.ToString())
        {
            if (request.Status == OpinionStatus.Open)
            {
                var replyBy = request.ReplyBy?.ToString("MMMM d, yyyy") ?? "the deadline";
                return $"{request.Patient.FullName} paid. Reply by {replyBy}.";
            }

            if (request.Status == OpinionStatus.Refunded && request.RefundCause != RefundCause.Declined)
            {
                return $"The 7 days to reply to {request.Patient.FullName} have passed. The payment was refunded.";
            }
        }

        return null;
    }

    private Task<OpinionRequest?> OwnedAwaitingAsync(Guid patientId, Guid requestId, CancellationToken cancellationToken) =>
        Details().SingleOrDefaultAsync(
            item => item.Id == requestId && item.PatientId == patientId && item.Status == OpinionStatus.AwaitingPayment,
            cancellationToken);

    public async Task<ServiceResult<OpinionResponse>> AnswerAsync(Guid doctorId, Guid requestId, string? opinion, CancellationToken cancellationToken)
    {
        var text = opinion?.Trim() ?? "";
        if (text.Length is < 1 or > 4000)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status400BadRequest, "Write your opinion.");
        }

        await ExpireOverdueAsync(cancellationToken);
        var request = await Details().SingleOrDefaultAsync(item => item.Id == requestId && item.DoctorId == doctorId, cancellationToken);
        if (request is null || request.Status == OpinionStatus.AwaitingPayment)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status404NotFound, "Request not found.");
        }

        if (request.Status == OpinionStatus.Refunded)
        {
            var message = request.RefundCause == RefundCause.Declined
                ? "This request was declined."
                : "The 7 days to reply have passed. This request was cancelled.";
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status409Conflict, message);
        }

        if (request.Status != OpinionStatus.Open)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status409Conflict, "This request already has an opinion.");
        }

        await TryPrepareAidAsync(request, cancellationToken);
        request.Opinion = text;
        request.Status = OpinionStatus.Answered;
        request.DoctorSeenStatus = nameof(OpinionStatus.Answered);
        await db.SaveChangesAsync(cancellationToken);
        await PayDoctorAsync(request, cancellationToken);
        return ServiceResult<OpinionResponse>.Ok(OpinionResponse.From(request, includeAid: true));
    }

    public async Task<ServiceResult<OpinionResponse>> DeclineAsync(Guid doctorId, Guid requestId, CancellationToken cancellationToken)
    {
        await ExpireOverdueAsync(cancellationToken);
        var request = await Details().SingleOrDefaultAsync(item => item.Id == requestId && item.DoctorId == doctorId, cancellationToken);
        if (request is null || request.Status == OpinionStatus.AwaitingPayment)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status404NotFound, "Request not found.");
        }

        if (request.Status == OpinionStatus.Refunded)
        {
            var message = request.RefundCause == RefundCause.Declined
                ? "This request was already declined."
                : "The 7 days to reply have passed. This request was cancelled.";
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status409Conflict, message);
        }

        if (request.Status != OpinionStatus.Open)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status409Conflict, "This request already has an opinion.");
        }

        if (string.IsNullOrWhiteSpace(request.PayPalCaptureId))
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status409Conflict, "This request has no payment to refund.");
        }

        try
        {
            await paypal.RefundAsync(request.PayPalCaptureId, request.Fee, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Could not refund declined opinion {OpinionId}", request.Id);
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status502BadGateway, "PayPal could not refund the payment.");
        }

        request.Status = OpinionStatus.Refunded;
        request.RefundCause = RefundCause.Declined;
        request.DoctorSeenStatus = nameof(OpinionStatus.Refunded);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<OpinionResponse>.Ok(OpinionResponse.From(request, includeAid: true));
    }

    public async Task<ServiceResult<OpinionResponse>> PrepareAidAsync(Guid doctorId, Guid requestId, bool refresh, CancellationToken cancellationToken)
    {
        var request = await Details().SingleOrDefaultAsync(item => item.Id == requestId && item.DoctorId == doctorId, cancellationToken);
        if (request is null || request.Status == OpinionStatus.AwaitingPayment)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status404NotFound, "Request not found.");
        }

        if (!refresh && ClinicalAidData.Read(request.ClinicalAid) is not null)
        {
            return ServiceResult<OpinionResponse>.Ok(OpinionResponse.From(request, includeAid: true));
        }

        if (request.Status is not (OpinionStatus.Open or OpinionStatus.Answered))
        {
            return ServiceResult<OpinionResponse>.Ok(OpinionResponse.From(request, includeAid: true));
        }

        if (!clinicalAid.IsConfigured)
        {
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status503ServiceUnavailable, "A reading could not be prepared.");
        }

        try
        {
            var aid = await clinicalAid.WriteAsync(request, await LoadAidFilesAsync(request, cancellationToken), cancellationToken);
            if (aid is null)
            {
                return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status503ServiceUnavailable, "A reading could not be prepared.");
            }

            request.ClinicalAid = aid.Write();
            await db.SaveChangesAsync(cancellationToken);
            return ServiceResult<OpinionResponse>.Ok(OpinionResponse.From(request, includeAid: true));
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not prepare clinical aid for opinion {OpinionId}", request.Id);
            return ServiceResult<OpinionResponse>.Fail(StatusCodes.Status502BadGateway, "A reading could not be prepared.");
        }
    }

    private async Task TryPrepareAidAsync(OpinionRequest request, CancellationToken cancellationToken)
    {
        if (ClinicalAidData.Read(request.ClinicalAid) is not null || !clinicalAid.IsConfigured)
        {
            return;
        }

        if (request.Status is not (OpinionStatus.Open or OpinionStatus.Answered))
        {
            return;
        }

        try
        {
            var aid = await clinicalAid.WriteAsync(request, await LoadAidFilesAsync(request, cancellationToken), cancellationToken);
            if (aid is null)
            {
                return;
            }

            request.ClinicalAid = aid.Write();
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not prepare clinical aid for opinion {OpinionId}", request.Id);
        }
    }

    public async Task ExpireOverdueAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await db.Opinions
            .Where(request => request.Status == OpinionStatus.Open && request.ReplyBy != null && request.ReplyBy < now)
            .ToListAsync(cancellationToken);

        foreach (var request in due)
        {
            if (string.IsNullOrWhiteSpace(request.PayPalCaptureId))
            {
                continue;
            }

            try
            {
                await paypal.RefundAsync(request.PayPalCaptureId, request.Fee, cancellationToken);
                request.Status = OpinionStatus.Refunded;
                request.RefundCause = RefundCause.Deadline;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Could not refund opinion {OpinionId}", request.Id);
            }
        }
    }

    private async Task PayDoctorAsync(OpinionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Doctor.PayPalEmail) || request.PayPalPayoutId is not null)
        {
            return;
        }

        var platformFee = decimal.Round(request.Fee * PlatformFeeRate, 2, MidpointRounding.AwayFromZero);
        var doctorAmount = request.Fee - platformFee;
        if (doctorAmount <= 0)
        {
            return;
        }

        try
        {
            request.PayPalPayoutId = await paypal.PayoutAsync(request.Id, request.Doctor.PayPalEmail, doctorAmount, cancellationToken);
            request.DoctorAmount = doctorAmount;
            request.PlatformAmount = platformFee;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Could not pay doctor for opinion {OpinionId}", request.Id);
        }
    }

    public async Task<ServiceResult<(MemoryStream Content, string ContentType)>> OpenOpinionFileAsync(
        Guid userId,
        UserRole role,
        Guid requestId,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        var request = await Details().SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        var allowed = request is not null && (
            (role == UserRole.Patient && request.PatientId == userId)
            || (role == UserRole.Doctor && request.DoctorId == userId && request.Status != OpinionStatus.AwaitingPayment));
        var file = request?.Files.SingleOrDefault(item => item.RecordId == recordId);
        if (!allowed || file is null)
        {
            return ServiceResult<(MemoryStream, string)>.Fail(StatusCodes.Status404NotFound, "File not found.");
        }

        var content = await storage.DownloadAsync(file.Record.ObjectKey, cancellationToken);
        return ServiceResult<(MemoryStream, string)>.Ok((content, file.Record.ContentType));
    }

    private async Task<MedicalRecord?> OwnedRecordAsync(Guid patientId, Guid recordId, CancellationToken cancellationToken) =>
        await db.Records
            .Include(record => record.RequestFiles)
            .SingleOrDefaultAsync(record => record.Id == recordId && record.PatientId == patientId, cancellationToken);

    private static RecordResponse ToResponse(MedicalRecord record, bool locked) =>
        new(record.Id, record.FileName, record.Description, record.CreatedAt, locked, record.ArchivedAt is not null);

    private async Task<IReadOnlyList<AidAttachment>> LoadAidFilesAsync(OpinionRequest request, CancellationToken cancellationToken)
    {
        const int maxEach = 15 * 1024 * 1024;
        const int maxTotal = 50 * 1024 * 1024;
        var attachments = new List<AidAttachment>();
        var total = 0;
        foreach (var file in request.Files)
        {
            var record = file.Record;
            if (record.ContentType is not ("application/pdf" or "image/jpeg" or "image/png" or "image/webp"))
            {
                continue;
            }

            try
            {
                await using var content = await storage.DownloadAsync(record.ObjectKey, cancellationToken);
                if (content.Length is <= 0 or > maxEach || total + content.Length > maxTotal)
                {
                    logger.LogWarning("Skipped record {RecordId} for the clinical reading", record.Id);
                    continue;
                }

                var bytes = content.ToArray();
                total += bytes.Length;
                attachments.Add(new AidAttachment(record.FileName, record.ContentType, record.Description, bytes));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not read record {RecordId} for the clinical reading", record.Id);
            }
        }

        return attachments;
    }

    private IQueryable<OpinionRequest> Details() => db.Opinions
        .Include(request => request.Doctor)
        .Include(request => request.Patient)
        .Include(request => request.Files)
        .ThenInclude(file => file.Record);
}
