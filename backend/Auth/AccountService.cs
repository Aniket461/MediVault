using MediVault.Api.Contracts;
using MediVault.Api.Data;
using MediVault.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Api.Auth;

public sealed class AccountService(MediVaultDbContext db)
{
    public async Task<AccountProfileResponse?> ProfileAsync(Guid callerId, UserRole callerRole, Guid targetId, CancellationToken cancellationToken)
    {
        var target = await db.Users.SingleOrDefaultAsync(user => user.Id == targetId, cancellationToken);
        if (target is null)
        {
            return null;
        }

        var self = callerId == targetId;
        if (!self)
        {
            var linked = await db.Opinions.AnyAsync(
                opinion => (opinion.PatientId == callerId && opinion.DoctorId == targetId)
                    || (opinion.DoctorId == callerId && opinion.PatientId == targetId),
                cancellationToken);
            if (!linked)
            {
                return null;
            }
        }

        var (spent, refunded, received) = self
            ? await LifetimeAsync(target.Id, target.Role, cancellationToken)
            : await SharedAsync(callerId, callerRole, target, cancellationToken);

        return new AccountProfileResponse(
            target.Id,
            target.FullName,
            self ? target.Email : null,
            self ? target.Phone : null,
            self ? target.Address : null,
            target.Role == UserRole.Doctor ? target.Hospital : null,
            self ? target.PayPalEmail : null,
            target.Role,
            target.Role == UserRole.Doctor ? target.SecondOpinionFee : null,
            self,
            spent,
            refunded,
            received);
    }

    public Task<(decimal Spent, decimal Refunded, decimal Received)> LifetimeAsync(Guid userId, UserRole role, CancellationToken cancellationToken) =>
        role switch
        {
            UserRole.Patient => PatientMoneyAsync(userId, null, cancellationToken),
            UserRole.Doctor => DoctorMoneyAsync(userId, null, cancellationToken),
            _ => Task.FromResult((0m, 0m, 0m))
        };

    private async Task<(decimal Spent, decimal Refunded, decimal Received)> SharedAsync(
        Guid callerId,
        UserRole callerRole,
        UserAccount target,
        CancellationToken cancellationToken)
    {
        if (callerRole == UserRole.Patient && target.Role == UserRole.Doctor)
        {
            return await PatientMoneyAsync(callerId, target.Id, cancellationToken);
        }

        if (callerRole == UserRole.Doctor && target.Role == UserRole.Patient)
        {
            return await DoctorMoneyAsync(callerId, target.Id, cancellationToken);
        }

        return (0m, 0m, 0m);
    }

    private async Task<(decimal Spent, decimal Refunded, decimal Received)> PatientMoneyAsync(Guid patientId, Guid? doctorId, CancellationToken cancellationToken)
    {
        var opinions = db.Opinions.Where(opinion => opinion.PatientId == patientId);
        if (doctorId is Guid id)
        {
            opinions = opinions.Where(opinion => opinion.DoctorId == id);
        }

        var spent = await opinions
            .Where(opinion => opinion.Status == OpinionStatus.Open || opinion.Status == OpinionStatus.Answered)
            .SumAsync(opinion => opinion.Fee, cancellationToken);
        var refunded = await opinions
            .Where(opinion => opinion.Status == OpinionStatus.Refunded)
            .SumAsync(opinion => opinion.Fee, cancellationToken);
        return (spent, refunded, 0m);
    }

    private async Task<(decimal Spent, decimal Refunded, decimal Received)> DoctorMoneyAsync(Guid doctorId, Guid? patientId, CancellationToken cancellationToken)
    {
        var opinions = db.Opinions.Where(opinion => opinion.DoctorId == doctorId && opinion.PayPalPayoutId != null);
        if (patientId is Guid id)
        {
            opinions = opinions.Where(opinion => opinion.PatientId == id);
        }

        var received = await opinions.SumAsync(opinion => opinion.DoctorAmount ?? 0m, cancellationToken);
        return (0m, 0m, received);
    }
}
