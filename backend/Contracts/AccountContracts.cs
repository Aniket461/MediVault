using MediVault.Api.Models;

namespace MediVault.Api.Contracts;

public sealed record AccountProfileResponse(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string? Address,
    string? Hospital,
    string? PayPalEmail,
    UserRole Role,
    decimal? SecondOpinionFee,
    bool Self,
    decimal Spent,
    decimal Refunded,
    decimal Received);
