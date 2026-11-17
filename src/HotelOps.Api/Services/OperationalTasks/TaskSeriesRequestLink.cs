using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal static class TaskSeriesRequestLink
{
    internal static async Task<bool> AllowedAsync(HotelOpsDbContext db,long actor,long branch,long request,DateTime now,CancellationToken ct)
    {
        if(!await InternalRequestDecisionStore.InstalledAsync(db,ct)||!await InternalRequestTaskLinks.InstalledAsync(db,ct)
            ||!await new InternalRequestProcessAuthority(db).AllowsCurrentRequestAsync(actor,request,ct))return false;
        var scope=await new InternalRequestReadAccess(db).ReadAsync(actor,now,ct);
        return await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==request&&r.BranchId==branch
            &&(r.Status=="ACCEPTED"||r.Status=="IN_PROGRESS"),ct);
    }

    // Called by terminal IR workflows in their existing serializable transaction.
    // Only future generation changes; occurrence links and execution history are untouched.
    internal static async Task StopAsync(HotelOpsDbContext db,long request,long actor,DateTime now,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("IR series stop requires a transaction.");
        if(!await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.operational_task_series','U') IS NULL THEN 0 ELSE 1 END AS Value").AnyAsync(x=>x==1,ct))return;
        var ids=await db.Database.SqlQuery<long>($"""
            SELECT series_id AS Value FROM dbo.operational_task_series WITH(UPDLOCK,HOLDLOCK)
            WHERE is_active=1 AND TRY_CONVERT(bigint,JSON_VALUE(template_json,'$.InternalRequestId'))={request}
            """).ToArrayAsync(ct);
        foreach(var id in ids)
        {
            var row=await TaskSeriesStore.Query(db).SingleAsync(s=>s.SeriesId==id,ct);
            if(TaskSeriesStore.Template(row).InternalRequestId!=request)throw new InvalidOperationException("Series source mismatch.");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_series SET is_active=0,stopped_at={now},stopped_by={actor} WHERE series_id={id} AND is_active=1",ct);
            db.AuditLogs.Add(new AuditLog{ActorUserId=actor,BranchId=row.BranchId,EntityType="operational_task_series",EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues="{\"isActive\":true}",NewValues=JsonSerializer.Serialize(new{operation="STOP_TERMINAL_IR",internalRequestId=request,isActive=false}),ChangedFields="[\"isActive\",\"stoppedAt\",\"stoppedBy\"]"});
        }
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
    }
}
