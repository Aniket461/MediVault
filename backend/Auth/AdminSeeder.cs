using MediVault.Api.Data;
using MediVault.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Api.Auth;

public sealed class AdminSeeder(MediVaultDbContext db, IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var email = configuration["Admin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["Admin:Password"];
        var fullName = configuration["Admin:FullName"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidOperationException("Admin email, password, and name must be set.");
        }

        var hasher = new PasswordHasher<UserAccount>();
        var admin = await db.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
        if (admin is null)
        {
            admin = await db.Users.SingleOrDefaultAsync(user => user.Email == "admin@medivault.local", cancellationToken);
        }

        if (admin is null)
        {
            admin = new UserAccount
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(admin);
        }

        var passwordMatches = admin.PasswordHash.Length > 0
            && hasher.VerifyHashedPassword(admin, admin.PasswordHash, password) is PasswordVerificationResult.Success;
        if (passwordMatches
            && admin.Email == email
            && admin.FullName == fullName
            && admin.Role == UserRole.Admin
            && admin.Status == AccountStatus.Approved)
        {
            return;
        }

        admin.FullName = fullName;
        admin.Email = email;
        admin.Role = UserRole.Admin;
        admin.Status = AccountStatus.Approved;
        admin.PasswordHash = hasher.HashPassword(admin, password);
        await db.SaveChangesAsync(cancellationToken);
    }
}
