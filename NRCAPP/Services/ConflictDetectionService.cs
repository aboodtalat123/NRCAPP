using Microsoft.EntityFrameworkCore;
using NRCAPP.Api;
using NRCAPP.Data;

namespace NRCAPP.Services;

public sealed class ConflictDetectionService(ReliefDbContext db, ILogger<ConflictDetectionService> logger)
{
    private static readonly TimeSpan ConflictWindow = TimeSpan.FromHours(48);

    public async Task<DistributionPlanResponse> SubmitPlanAsync(DistributionPlanRequest request)
    {
        var targetSector = request.TargetSector.Trim();
        if (string.IsNullOrWhiteSpace(targetSector))
        {
            return new DistributionPlanResponse(
                false,
                null,
                DistributionPlanStatus.Warning,
                "يرجى اختيار القطاع المستهدف قبل حفظ خطة التوزيع.");
        }

        if (request.Quantity <= 0 || request.MaxBeneficiaryCapacity <= 0)
        {
            return new DistributionPlanResponse(
                false,
                null,
                DistributionPlanStatus.Warning,
                "يرجى إدخال كمية وسعة أكبر من صفر قبل حفظ خطة التوزيع.");
        }

        var organizationExists = await db.Organizations.AnyAsync(x => x.Id == request.OrganizationId && x.IsVerified);
        if (!organizationExists)
        {
            return new DistributionPlanResponse(
                false,
                null,
                DistributionPlanStatus.Warning,
                "تعذر حفظ الخطة: المؤسسة غير موثقة أو رقمها غير صحيح.");
        }

        var from = request.ScheduledDate.Subtract(ConflictWindow);
        var to = request.ScheduledDate.Add(ConflictWindow);

        var conflict = await db.DistributionPlans
            .Include(x => x.Organization)
            .Where(x => x.Status != DistributionPlanStatus.Cancelled)
            .Where(x => x.TargetSector == targetSector)
            .Where(x => x.AidType == request.AidType)
            .Where(x => x.ScheduledDate >= from && x.ScheduledDate <= to)
            .OrderBy(x => x.ScheduledDate)
            .FirstOrDefaultAsync();

        if (conflict is not null)
        {
            var message = $"تم حفظ الخطة كتحذير فقط: يوجد توزيع مشابه في قطاع {targetSector} لنفس نوع المساعدة ضمن نافذة 48 ساعة. غيّر التاريخ أو القطاع أو نوع المساعدة لاعتماد الخطة.";
            var blockedPlan = new DistributionPlan
            {
                AidType = request.AidType,
                ScheduledDate = request.ScheduledDate,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                TargetSector = targetSector,
                Quantity = request.Quantity,
                MaxBeneficiaryCapacity = request.MaxBeneficiaryCapacity,
                OrganizationId = request.OrganizationId,
                Status = DistributionPlanStatus.Warning,
                ConflictMessage = $"{message} الخطة المتعارضة رقم {conflict.Id} بواسطة {conflict.Organization?.NgoName ?? "مؤسسة غير معروفة"}."
            };

            db.DistributionPlans.Add(blockedPlan);
            await db.SaveChangesAsync();

            db.AuditLogs.Add(new AuditLog
            {
                ActorType = "Organization",
                Action = "تحذير خطة توزيع",
                EntityName = nameof(DistributionPlan),
                EntityId = blockedPlan.Id,
                Details = blockPlanDetail(targetSector, request, conflict)
            });
            await db.SaveChangesAsync();

            logger.LogWarning("Plan conflict detected: Sector={Sector}, AidType={AidType}, OrgId={OrgId}",
                targetSector, request.AidType, request.OrganizationId);

            return new DistributionPlanResponse(false, blockedPlan.Id, blockedPlan.Status, blockedPlan.ConflictMessage);
        }

        var plan = new DistributionPlan
        {
            AidType = request.AidType,
            ScheduledDate = request.ScheduledDate,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            TargetSector = targetSector,
            Quantity = request.Quantity,
            MaxBeneficiaryCapacity = request.MaxBeneficiaryCapacity,
            OrganizationId = request.OrganizationId,
            Status = DistributionPlanStatus.Authorized
        };

        db.DistributionPlans.Add(plan);
        await db.SaveChangesAsync();

        logger.LogInformation("Plan authorized: Id={PlanId}, Sector={Sector}, AidType={AidType}", plan.Id, targetSector, request.AidType);

        return new DistributionPlanResponse(true, plan.Id, plan.Status, "تم اعتماد التوزيع وحفظه في قاعدة البيانات. لا يوجد تكرار ضمن نافذة الفحص.");
    }

    private static string blockPlanDetail(string targetSector, DistributionPlanRequest request, DistributionPlan conflict)
    {
        return $"تم رفع تحذير على خطة قطاع {targetSector}، نوع المساعدة {request.AidType}. "
            + $"تتعارض مع خطة رقم {conflict.Id} بواسطة {conflict.Organization?.NgoName ?? "غير معروف"} "
            + $"بتاريخ {conflict.ScheduledDate:yyyy-MM-dd}.";
    }
}
