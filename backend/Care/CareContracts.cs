using MediVault.Api.Models;

namespace MediVault.Api.Care;

public sealed record RecordResponse(
    Guid Id,
    string FileName,
    string Description,
    DateTimeOffset CreatedAt,
    bool Locked,
    bool Archived);

public sealed class UpdateRecordBody
{
    public string Description { get; set; } = "";
}

public sealed record DoctorOption(Guid Id, string FullName, string? Hospital, decimal Fee);

public sealed record OpinionFileResponse(Guid Id, string FileName, string Description);

public sealed record OpinionResponse(
    Guid Id,
    Guid DoctorId,
    string DoctorName,
    string? Hospital,
    Guid PatientId,
    string PatientName,
    decimal Fee,
    decimal? DoctorAmount,
    decimal? PlatformAmount,
    OpinionStatus Status,
    RefundCause? RefundCause,
    string Message,
    ClinicalIntakeData? Intake,
    ClinicalAidData? ClinicalAid,
    string? Opinion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReplyBy,
    IReadOnlyList<OpinionFileResponse> Files,
    string? Notice = null)
{
    public static OpinionResponse From(OpinionRequest request, bool includeAid = false) => new(
        request.Id,
        request.DoctorId,
        request.Doctor.FullName,
        request.Doctor.Hospital,
        request.PatientId,
        request.Patient.FullName,
        request.Fee,
        request.DoctorAmount,
        request.PlatformAmount,
        request.Status,
        request.RefundCause,
        request.Message,
        ClinicalIntakeData.Read(request.Intake),
        includeAid ? ClinicalAidData.Read(request.ClinicalAid) : null,
        request.Opinion,
        request.CreatedAt,
        request.ReplyBy,
        request.Files
            .Select(file => new OpinionFileResponse(file.Record.Id, file.Record.FileName, file.Record.Description))
            .ToList());
}

public sealed class CreateOpinionBody
{
    public Guid DoctorId { get; set; }

    public List<Guid> RecordIds { get; set; } = [];

    public string Message { get; set; } = "";

    public List<string> Symptoms { get; set; } = [];

    public string Feeling { get; set; } = "";

    public string Duration { get; set; } = "";

    public string Intensity { get; set; } = "";

    public string Consulted { get; set; } = "";

    public string ConsultedNote { get; set; } = "";
}

public sealed class AnswerBody
{
    public string Opinion { get; set; } = "";
}

public sealed record PayPalConfigResponse(bool Enabled, string ClientId, string Currency);

public sealed record PayPalOrderResponse(string OrderId);

public sealed class CapturePayPalBody
{
    public string OrderId { get; set; } = "";
}
