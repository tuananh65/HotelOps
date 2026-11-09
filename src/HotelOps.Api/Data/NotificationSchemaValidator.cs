using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public static class NotificationSchemaValidator
{
    public static async Task ValidateAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        var marker = await db.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128),value) AS [Value] FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.notification_intents_c04') AND name=N'HotelOps.Notification.Contract'").SingleOrDefaultAsync(ct);
        if (marker != "C04-TRIAL-V1") throw new InvalidOperationException("Closure04 Notification trial package is unavailable. No automatic DDL is performed.");
        // Validate the actual new projection before binding a listener, including rowversion and all fields.
        _ = await db.NotificationIntents.AsNoTracking().OrderBy(x => x.IntentId).Take(1).ToArrayAsync(ct);
        var keys = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.notification_intents_c04') AND is_unique=1 AND is_disabled=0 AND name=N'IX_notification_intents_c04_EventType_EventVersion_OccurrenceId_RecipientUserId'").SingleAsync(ct);
        var version = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.notification_intents_c04') AND name=N'RowVersion' AND system_type_id=189 AND max_length=8 AND is_nullable=0").SingleAsync(ct);
        if (keys != 1 || version != 1) throw new InvalidOperationException("Closure04 notification uniqueness/concurrency schema unavailable.");
    }
}
