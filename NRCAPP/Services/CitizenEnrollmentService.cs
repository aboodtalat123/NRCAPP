using System.Data;
using Microsoft.EntityFrameworkCore;
using NRCAPP.Data;

namespace NRCAPP.Services;

public sealed record CitizenEnrollmentResult(bool Success, bool AlreadyRegistered, string Message, int? RegistrationId = null);

public sealed class CitizenEnrollmentService(ReliefDbContext db, ILogger<CitizenEnrollmentService> logger)
{
    public async Task<CitizenEnrollmentResult> EnrollAsync(
        int beneficiaryId,
        int distributionPlanId,
        CancellationToken cancellationToken = default)
    {
        if (beneficiaryId <= 0 || distributionPlanId <= 0)
        {
            return new(false, false, "بيانات طلب التسجيل غير صالحة.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var beneficiary = await db.Beneficiaries
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == beneficiaryId, cancellationToken);

        if (beneficiary is null || beneficiary.VerificationStatus != VerificationStatus.Verified)
        {
            return new(false, false, "حساب المواطن غير موجود أو غير موثق.");
        }

        var plan = await db.DistributionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == distributionPlanId, cancellationToken);

        if (plan is null)
        {
            return new(false, false, "الكوبونة المطلوبة غير موجودة.");
        }

        if (plan.Status != DistributionPlanStatus.Authorized)
        {
            return new(false, false, "هذه الكوبونة غير متاحة للتسجيل حالياً.");
        }

        if (plan.ScheduledDate <= DateTimeOffset.UtcNow)
        {
            return new(false, false, "انتهى موعد التسجيل في هذه الكوبونة.");
        }

        if (!string.Equals(plan.TargetSector.Trim(), beneficiary.CurrentSector.Trim(), StringComparison.Ordinal))
        {
            return new(false, false, $"هذه الكوبونة مخصصة لقطاع {plan.TargetSector}، بينما ملفك في قطاع {beneficiary.CurrentSector}.");
        }

        var existing = await db.DistributionRegistrations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.BeneficiaryId == beneficiaryId && x.DistributionPlanId == distributionPlanId,
                cancellationToken);

        if (existing is not null)
        {
            return new(true, true, "أنت مسجل مسبقاً في هذه الكوبونة.", existing.Id);
        }

        var reservedPlaces = await db.DistributionRegistrations.CountAsync(
            x => x.DistributionPlanId == distributionPlanId && x.Status != RegistrationStatus.Rejected,
            cancellationToken);

        if (reservedPlaces >= plan.MaxBeneficiaryCapacity)
        {
            return new(false, false, "اكتملت سعة هذه الكوبونة. اختر كوبونة أخرى متاحة.");
        }

        var registration = new DistributionRegistration
        {
            BeneficiaryId = beneficiaryId,
            DistributionPlanId = distributionPlanId,
            Status = RegistrationStatus.Requested,
            RequestedAt = DateTimeOffset.UtcNow
        };

        db.DistributionRegistrations.Add(registration);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            db.AuditLogs.Add(new AuditLog
            {
                UserId = beneficiaryId,
                ActorType = "Individual",
                Action = "طلب تسجيل كوبونة",
                EntityName = nameof(DistributionRegistration),
                EntityId = registration.Id,
                Details = $"طلب المواطن التسجيل في خطة رقم {distributionPlanId} بقطاع {plan.TargetSector}."
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Citizen enrollment created: BeneficiaryId={BeneficiaryId}, PlanId={PlanId}, RegistrationId={RegistrationId}",
                beneficiaryId,
                distributionPlanId,
                registration.Id);

            return new(true, false, "تم إرسال طلب التسجيل للمؤسسة بنجاح.", registration.Id);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(exception,
                "Citizen enrollment conflict: BeneficiaryId={BeneficiaryId}, PlanId={PlanId}",
                beneficiaryId,
                distributionPlanId);

            var duplicate = await db.DistributionRegistrations
                .AsNoTracking()
                .AnyAsync(x => x.BeneficiaryId == beneficiaryId && x.DistributionPlanId == distributionPlanId, cancellationToken);

            return duplicate
                ? new(true, true, "أنت مسجل مسبقاً في هذه الكوبونة.")
                : new(false, false, "تعذر حفظ الطلب بسبب تحديث متزامن. حاول مرة أخرى.");
        }
    }
}
