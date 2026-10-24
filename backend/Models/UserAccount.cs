namespace MediVault.Api.Models;

public sealed class UserAccount
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string Phone { get; set; } = "";

    public string Address { get; set; } = "";

    public string? Hospital { get; set; }

    public string? PayPalEmail { get; set; }

    public string PasswordHash { get; set; } = "";

    public UserRole Role { get; set; }

    public AccountStatus Status { get; set; }

    public decimal? SecondOpinionFee { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<IdentityDocument> Documents { get; set; } = [];
}

public sealed class IdentityDocument
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "";

    public string ObjectKey { get; set; } = "";

    public UserAccount User { get; set; } = null!;
}

public enum UserRole
{
    Patient,
    Doctor,
    Admin
}

public enum AccountStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed class MedicalRecord
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "";

    public string Description { get; set; } = "";

    public string ObjectKey { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public UserAccount Patient { get; set; } = null!;

    public List<OpinionRequestFile> RequestFiles { get; set; } = [];
}

public enum OpinionStatus
{
    AwaitingPayment,
    Open,
    Answered,
    Refunded
}

public enum RefundCause
{
    Deadline,
    Declined
}

public sealed class OpinionRequest
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public string? PatientSeenStatus { get; set; }

    public Guid DoctorId { get; set; }

    public string? DoctorSeenStatus { get; set; }

    public string Message { get; set; } = "";

    public string? Intake { get; set; }

    public string? ClinicalAid { get; set; }

    public decimal Fee { get; set; }

    public OpinionStatus Status { get; set; }

    public RefundCause? RefundCause { get; set; }

    public string? Opinion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public DateTimeOffset? ReplyBy { get; set; }

    public string? PayPalOrderId { get; set; }

    public string? PayPalCaptureId { get; set; }

    public string? PayPalPayoutId { get; set; }

    public decimal? DoctorAmount { get; set; }

    public decimal? PlatformAmount { get; set; }

    public UserAccount Patient { get; set; } = null!;

    public UserAccount Doctor { get; set; } = null!;

    public List<OpinionRequestFile> Files { get; set; } = [];
}

public sealed class OpinionRequestFile
{
    public Guid RequestId { get; set; }

    public Guid RecordId { get; set; }

    public OpinionRequest Request { get; set; } = null!;

    public MedicalRecord Record { get; set; } = null!;
}
