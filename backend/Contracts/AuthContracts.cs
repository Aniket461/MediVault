using MediVault.Api.Models;

namespace MediVault.Api.Contracts;

public sealed class RegisterRequest
{
    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string Password { get; set; } = "";

    public string Phone { get; set; } = "";

    public string Address { get; set; } = "";

    public string Role { get; set; } = "";

    public string? Hospital { get; set; }

    public string? PayPalEmail { get; set; }

    public decimal? SecondOpinionFee { get; set; }
}

public sealed record DocumentResponse(Guid Id, string FileName);

public sealed record LoginRequest(string Email, string Password);

public sealed record PayPalEmailRequest(string PayPalEmail);

public sealed class UpdateProfileRequest
{
    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string Phone { get; set; } = "";

    public string Address { get; set; } = "";

    public string? Hospital { get; set; }

    public string? PayPalEmail { get; set; }

    public decimal? SecondOpinionFee { get; set; }
}

public sealed record MessageResponse(string Message);

public sealed record AuthResponse(string Token, UserResponse User);

public sealed record UserResponse(
    Guid Id,
    string FullName,
    string Email,
    string Phone,
    string Address,
    string? Hospital,
    string? PayPalEmail,
    UserRole Role,
    AccountStatus Status,
    decimal? SecondOpinionFee)
{
    public static UserResponse From(UserAccount user) => new(
        user.Id,
        user.FullName,
        user.Email,
        user.Phone,
        user.Address,
        user.Hospital,
        user.PayPalEmail,
        user.Role,
        user.Status,
        user.SecondOpinionFee);
}

public sealed record UserDetailResponse(
    Guid Id,
    string FullName,
    string Email,
    string Phone,
    string Address,
    string? Hospital,
    string? PayPalEmail,
    UserRole Role,
    AccountStatus Status,
    decimal? SecondOpinionFee,
    decimal Spent,
    decimal Refunded,
    decimal Received,
    IReadOnlyList<DocumentResponse> Documents)
{
    public static UserDetailResponse From(UserAccount user, decimal spent, decimal refunded, decimal received) => new(
        user.Id,
        user.FullName,
        user.Email,
        user.Phone,
        user.Address,
        user.Hospital,
        user.PayPalEmail,
        user.Role,
        user.Status,
        user.SecondOpinionFee,
        spent,
        refunded,
        received,
        user.Documents.Select(document => new DocumentResponse(document.Id, document.FileName)).ToList());
}
