using Microsoft.EntityFrameworkCore;
using NRCAPP.Api;
using NRCAPP.Data;

namespace NRCAPP.Services;

public sealed class SyncQueueService(ReliefDbContext db)
{
    public async Task<SyncPacketResponse> QueuePacketAsync(SyncPacketRequest request)
    {
        var existing = await db.SyncQueueItems
            .SingleOrDefaultAsync(x => x.LocalDeviceActionId == request.LocalDeviceActionId);

        if (existing is not null)
        {
            return new SyncPacketResponse(
                existing.LocalDeviceActionId,
                existing.SyncStatus,
                "الحزمة موجودة مسبقاً في قائمة المزامنة على الخادم.");
        }

        var item = new SyncQueueItem
        {
            LocalDeviceActionId = request.LocalDeviceActionId,
            PayloadJson = request.PayloadJson,
            Timestamp = request.Timestamp,
            SyncStatus = SyncStatus.Pending
        };

        db.SyncQueueItems.Add(item);
        await db.SaveChangesAsync();

        return new SyncPacketResponse(item.LocalDeviceActionId, item.SyncStatus, "تمت إضافة الحزمة إلى قائمة المزامنة.");
    }

    public async Task<IReadOnlyList<SyncQueueItem>> GetPendingAsync()
    {
        return await db.SyncQueueItems
            .AsNoTracking()
            .Where(x => x.SyncStatus == SyncStatus.Pending)
            .OrderBy(x => x.Timestamp)
            .ToListAsync();
    }
}
