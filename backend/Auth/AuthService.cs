using MediVault.Api.Contracts;
using MediVault.Api.Data;
using MediVault.Api.Models;
using MediVault.Api.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Api.Auth;

public sealed class AuthService(MediVaultDbContext db, TokenService tokens, ObjectStorage storage)
{
    private static readonly HashSet<string> AllowedImages = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly PasswordHasher<UserAccount> _hasher = new();

    public async Task<ServiceResult<MessageResponse>> RegisterAsync(
        RegisterRequest request,
        IReadOnlyList<IFormFile> documents,
        CancellationToken cancellationToken)
    {
        var fullName = request.FullName?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var password = request.Password ?? "";
        var phone = new string((request.Phone ?? "").Where(character => !char.IsWhiteSpace(character)).ToArray());
        var address = request.Address?.Trim() ?? "";
        var hospital = request.Hospital?.Trim();
        var payPalEmail = request.PayPalEmail?.Trim().ToLowerInvariant();

        if (fullName.Length is < 1 or > 120)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Enter your name.");
        }

        if (!email.Contains('@') || email.Length > 200)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Enter a valid email.");
        }

        if (password.Length < 8)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Password must be at least 8 characters.");
        }

        if (phone.Length is < 7 or > 20 || phone.Any(character => !char.IsDigit(character) && character != '+'))
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Enter a valid phone number.");
        }

        if (address.Length is < 5 or > 300)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Enter your address.");
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role) || role is UserRole.Admin)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Choose patient or doctor.");
        }

        decimal? fee = null;
        if (role != UserRole.Doctor)
        {
            documents = [];
        }

        if (role == UserRole.Doctor)
        {
            if (string.IsNullOrWhiteSpace(hospital) || hospital.Length > 160)
            {
                return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Enter the hospital you work with.");
            }

            if (string.IsNullOrWhiteSpace(payPalEmail) || !payPalEmail.Contains('@') || payPalEmail.Length > 200)
            {
                return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Enter the PayPal email that should receive your fee.");
            }

            if (request.SecondOpinionFee is null || request.SecondOpinionFee <= 0 || request.SecondOpinionFee > 100_000)
            {
                return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Enter a second-opinion fee greater than zero.");
            }

            if (documents.Count is < 1 or > 4)
            {
                return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Add one to four identification images.");
            }

            foreach (var document in documents)
            {
                if (document.Length is <= 0 or > 8 * 1024 * 1024 || !AllowedImages.Contains(document.ContentType))
                {
                    return ServiceResult<MessageResponse>.Fail(StatusCodes.Status400BadRequest, "Identification files must be JPEG, PNG, or WebP images under 8 MB.");
                }
            }

            fee = decimal.Round(request.SecondOpinionFee.Value, 2, MidpointRounding.AwayFromZero);
        }

        var taken = await db.Users.AnyAsync(user => user.Email == email, cancellationToken);
        if (taken)
        {
            return ServiceResult<MessageResponse>.Fail(StatusCodes.Status409Conflict, "An account with that email already exists.");
        }

        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Email = email,
            Phone = phone,
            Address = address,
            Hospital = role == UserRole.Doctor ? hospital : null,
            PayPalEmail = role == UserRole.Doctor ? payPalEmail : null,
            Role = role,
            Status = AccountStatus.Pending,
            SecondOpinionFee = fee,
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, password);

        foreach (var document in documents)
        {
            var record = new IdentityDocument
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FileName = Path.GetFileName(document.FileName),
                ContentType = document.ContentType,
                ObjectKey = $"identity/{user.Id}/{Guid.NewGuid()}"
            };
            await using var stream = document.OpenReadStream();
            await storage.UploadAsync(record.ObjectKey, stream, record.ContentType, cancellationToken);
            user.Documents.Add(record);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<MessageResponse>.Ok(new MessageResponse("Account created."));
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var password = request.Password ?? "";
        var user = await db.Users.SingleOrDefaultAsync(account => account.Email == email, cancellationToken);

        if (user is null || _hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status401Unauthorized, "Email or password is incorrect.");
        }

        if (user.Status == AccountStatus.Pending)
        {
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status403Forbidden, "This account is not approved yet.");
        }

        if (user.Status == AccountStatus.Rejected)
        {
            return ServiceResult<AuthResponse>.Fail(StatusCodes.Status403Forbidden, "This account was rejected.");
        }

        return ServiceResult<AuthResponse>.Ok(new AuthResponse(tokens.Create(user), UserResponse.From(user)));
    }

    public async Task<ServiceResult<UserResponse>> SetPayPalEmailAsync(Guid userId, string? payPalEmail, CancellationToken cancellationToken)
    {
        var email = payPalEmail?.Trim().ToLowerInvariant() ?? "";
        if (!email.Contains('@') || email.Length > 200)
        {
            return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter the PayPal email that should receive your fee.");
        }

        var user = await db.Users.SingleOrDefaultAsync(account => account.Id == userId, cancellationToken);
        if (user is null || user.Role != UserRole.Doctor)
        {
            return ServiceResult<UserResponse>.Fail(StatusCodes.Status404NotFound, "Account not found.");
        }

        user.PayPalEmail = email;
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<UserResponse>.Ok(UserResponse.From(user));
    }

    public async Task<ServiceResult<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(account => account.Id == userId, cancellationToken);
        if (user is null || user.Status != AccountStatus.Approved)
        {
            return ServiceResult<UserResponse>.Fail(StatusCodes.Status404NotFound, "Account not found.");
        }

        var fullName = request.FullName?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var phone = new string((request.Phone ?? "").Where(character => !char.IsWhiteSpace(character)).ToArray());
        var address = request.Address?.Trim() ?? "";

        if (fullName.Length is < 1 or > 120)
        {
            return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter your name.");
        }

        if (!email.Contains('@') || email.Length > 200)
        {
            return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter a valid email.");
        }

        var emailTaken = await db.Users.AnyAsync(account => account.Email == email && account.Id != user.Id, cancellationToken);
        if (emailTaken)
        {
            return ServiceResult<UserResponse>.Fail(StatusCodes.Status409Conflict, "An account with that email already exists.");
        }

        if (user.Role == UserRole.Admin)
        {
            if (phone.Length > 0 && (phone.Length is < 7 or > 20 || phone.Any(character => !char.IsDigit(character) && character != '+')))
            {
                return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter a valid phone number.");
            }

            if (address.Length > 300 || (address.Length > 0 && address.Length < 5))
            {
                return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter your address.");
            }
        }
        else
        {
            if (phone.Length is < 7 or > 20 || phone.Any(character => !char.IsDigit(character) && character != '+'))
            {
                return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter a valid phone number.");
            }

            if (address.Length is < 5 or > 300)
            {
                return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter your address.");
            }
        }

        string? hospital = null;
        string? payPalEmail = null;
        decimal? fee = null;
        if (user.Role == UserRole.Doctor)
        {
            hospital = request.Hospital?.Trim() ?? "";
            payPalEmail = request.PayPalEmail?.Trim().ToLowerInvariant() ?? "";
            if (hospital.Length is < 1 or > 160)
            {
                return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter the hospital you work with.");
            }

            if (!payPalEmail.Contains('@') || payPalEmail.Length > 200)
            {
                return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter the PayPal email that should receive your fee.");
            }

            if (request.SecondOpinionFee is null || request.SecondOpinionFee <= 0 || request.SecondOpinionFee > 100_000)
            {
                return ServiceResult<UserResponse>.Fail(StatusCodes.Status400BadRequest, "Enter a second-opinion fee greater than zero.");
            }

            fee = decimal.Round(request.SecondOpinionFee.Value, 2, MidpointRounding.AwayFromZero);
        }

        user.FullName = fullName;
        user.Email = email;
        user.Phone = phone;
        user.Address = address;
        if (user.Role == UserRole.Doctor)
        {
            user.Hospital = hospital;
            user.PayPalEmail = payPalEmail;
            user.SecondOpinionFee = fee;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<UserResponse>.Ok(UserResponse.From(user));
    }
}

public sealed class ServiceResult<T>
{
    public int Status { get; private init; }

    public string? Error { get; private init; }

    public T? Value { get; private init; }

    public bool Succeeded => Error is null;

    public static ServiceResult<T> Ok(T value) => new() { Status = StatusCodes.Status200OK, Value = value };

    public static ServiceResult<T> Fail(int status, string error) => new() { Status = status, Error = error };
}
