using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ReactivateTaskSeriesAsync(SessionReference ticket,long sid,long branch,long id,string expected,StopTaskSeriesRequest input,CancellationToken ct)
    {
        var reason=TaskPlanningReason(input.Reason);
        if(id<=0||!WorkflowStrongTag(expected)||string.IsNullOrWhiteSpace(reason)||input.TaskId!=null)
            throw new ValidationException("Series version and reactivation reason required.");
        return BatchAsync(ticket,sid,branch,async(actor,token)=>
        {
            if(!await TaskSeriesStore.InstalledAsync(db,token)||!await db.Database.SqlQuery<int>($"SELECT 1 AS Value FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSeries.Reactivation.V1' AND CONVERT(nvarchar(20),value)='1'").AnyAsync(token))return new(RoomTypeBatchStatus.SchemaRequired);
            var hint=await TaskSeriesStore.Query(db).SingleOrDefaultAsync(s=>s.SeriesId==id&&s.BranchId==branch,token);
            if(hint==null)return new(RoomTypeBatchStatus.NotFound);
            var template=TaskSeriesStore.Template(hint);
            var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            // Original initiating manager remains accountable for scheduled work.
            if(hint.CreatedBy!=actor)return new(RoomTypeBatchStatus.Forbidden);
            if(template.InternalRequestId is {} source&&!await TaskSeriesRequestLink.AllowedAsync(db,actor,branch,source,now,token))return new(RoomTypeBatchStatus.Forbidden);
            await db.Database.SqlQuery<long>($"SELECT series_id AS Value FROM dbo.operational_task_series WITH(UPDLOCK,HOLDLOCK) WHERE series_id={id}").ToArrayAsync(token);
            var row=await TaskSeriesStore.Query(db).SingleAsync(s=>s.SeriesId==id,token);
            if('"'+Convert.ToHexString(row.Revision)+'"'!=expected)return new(RoomTypeBatchStatus.Stale);
            if(row.IsActive||row.NextDueAt==null)return new(RoomTypeBatchStatus.Inactive);
            if(!await TaskAssignmentEligibility.Candidates(db,branch,row.DepartmentId,now).AnyAsync(u=>u.UserId==row.DefaultAssigneeId,token)
                ||!await TaskCreationReferencesAsync(db,branch,template,token))return new(RoomTypeBatchStatus.InvalidReference);
            var rule=TaskSeriesStore.Rule(row);
            var next=row.NextDueAt;
            // Never backfill periods skipped while stopped, including a planned start
            // already in the past even when that occurrence's deadline is still future.
            while(next is {} due&&(due<=now||template.PlannedStartAt is {} start&&start.UtcDateTime+(due-row.FirstDueAt)<=now))
                next=TaskRecurrenceSchedule.Next(due,rule);
            if(next==null)return new(RoomTypeBatchStatus.Inactive);
            db.AuditLogs.Add(new AuditLog{ActorUserId=actor,BranchId=branch,EntityType="operational_task_series",EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues=JsonSerializer.Serialize(new{isActive=false,row.StoppedAt,row.StoppedBy,row.NextDueAt}),
                NewValues=JsonSerializer.Serialize(new{operation="REACTIVATE_SERIES",isActive=true,reason,previousVersion=Convert.ToHexString(row.Revision),nextDueAt=next,internalRequestId=template.InternalRequestId}),ChangedFields="[\"isActive\",\"nextDueAt\",\"stoppedAt\",\"stoppedBy\"]"});
            await db.SaveChangesAsync(token);db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_series SET is_active=1,stopped_at=NULL,stopped_by=NULL,next_due_at={next} WHERE series_id={id} AND row_version={row.Revision}",token);
            var dto=await TaskSeriesStore.DtoAsync(db,await TaskSeriesStore.Query(db).SingleAsync(s=>s.SeriesId==id,token),token);
            return new(RoomTypeBatchStatus.Success,new{series=dto,messageCode="MSG08"},dto.ETag);
        },ct,requireRoomTypeSchema:false,featurePermission:token=>VerificationManager(ticket.UserId,branch,token));
    }
}
