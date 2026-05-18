using Microsoft.EntityFrameworkCore;
using NRCAPP.Api;
using NRCAPP.Data;

namespace NRCAPP.Services;

public sealed class ConflictDetectionService(ReliefDbContext db)
{
    private static readonly TimeSpan ConflictWindow = TimeSpan.FromHours(48);

    public async Task<DistributionPlanResponse> SubmitPlanAsync(DistributionPlanRequest request)
    {
        var organizationExists = await db.Organizations.AnyAsync(x => x.Id == request.OrganizationId && x.IsVerified);
        if (!organizationExists)
        {
            return new DistributionPlanResponse(
                false,
                null,
                DistributionPlanStatus.Warning,
                "تحذير: المؤسسة المنفذة غير موثقة.");
        }

        var from = request.ScheduledDate.Subtract(ConflictWindow);
        var to = request.ScheduledDate.Add(ConflictWindow);

        var conflict = await db.DistributionPlans
            .Include(x => x.Organization)
            .Where(x => x.Status != DistributionPlanStatus.Cancelled)
            .Where(x => x.TargetSector == request.TargetSector)
            .Where(x => x.AidType == request.AidType)
            .Where(x => x.ScheduledDate >= from && x.ScheduledDate <= to)
            .OrderBy(x => x.ScheduledDate)
            .FirstOrDefaultAsync();

        if (conflict is not null)
        {
            var message = $"تحذير: تم رصد توزيع مشابه في قطاع {request.TargetSector}. يفضل تحويل الموارد إلى فجوة قريبة أقل خدمة.";
            var blockedPlan = new DistributionPlan
            {
                AidType = request.AidType,
                ScheduledDate = request.ScheduledDate,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                TargetSector = request.TargetSector,
                Quantity = request.Quantity,
                MaxBeneficiaryCapacity = request.MaxBeneficiaryCapacity,
                OrganizationId = request.OrganizationId,
                Status = DistributionPlanStatus.Warning,
                ConflictMessage = $"{message} الخطة الموجودة رقم {conflict.Id} بواسطة {conflict.Organization?.NgoName}."
            };

            db.DistributionPlans.Add(blockedPlan);
            await db.SaveChangesAsync();

            return new DistributionPlanResponse(false, blockedPlan.Id, blockedPlan.Status, blockedPlan.ConflictMessage);
        }

        var plan = new DistributionPlan
        {
            AidType = request.AidType,
            ScheduledDate = request.ScheduledDate,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            TargetSector = request.TargetSector,
            Quantity = request.Quantity,
            MaxBeneficiaryCapacity = request.MaxBeneficiaryCapacity,
            OrganizationId = request.OrganizationId,
            Status = DistributionPlanStatus.Authorized
        };

        db.DistributionPlans.Add(plan);
        await db.SaveChangesAsync();

        return new DistributionPlanResponse(true, plan.Id, plan.Status, "تم اعتماد التوزيع. لا يوجد تكرار ضمن نافذة الفحص.");
    }
}
