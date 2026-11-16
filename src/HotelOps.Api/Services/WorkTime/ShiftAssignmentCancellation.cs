using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.WorkTime;
public sealed record CancelShiftAssignmentInput(string Reason,bool Confirmation);
public sealed record ShiftCancellationReceipt(string Operation,long AssignmentId,long EmployeeId,long ShiftId,long BranchId,long DepartmentId,string Reason);
internal static class ShiftAssignmentRevision
{
    internal static string Version(ShiftAssignment a,WorkShift s,long audit)=>'"'+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{
        a.ShiftAssignmentId,a.ShiftId,a.UserId,a.AssignmentStatus,a.AssignedBy,a.AssignedAt,a.ConfirmedAt,a.CancellationReason,
        s.BranchId,s.DepartmentId,s.ShiftTemplateId,s.ShiftDate,s.ScheduledStartAt,s.ScheduledEndAt,s.Status,audit}))))+'"';
}
public sealed class ShiftAssignmentCancellation(HotelOpsDbContext db,ShiftRosterService authority)
{
    public async Task CancelAsync(SessionReference ticket,long sid,long branch,long department,long id,string? version,CancelShiftAssignmentInput input,CancellationToken ct)
    {
        var reason=input.Reason?.Trim();
        if(branch<=0||department<=0||id<=0||!input.Confirmation||string.IsNullOrWhiteSpace(reason)||reason.Length<10||reason.Length>1000
            ||reason.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t'))throw new InternalRequestReadException(400,"INVALID_CANCELLATION");
        if(string.IsNullOrEmpty(version))throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");
        // Discover the lock key only; all identity/scope/state facts are read again under locks.
        var user=await db.ShiftAssignments.AsNoTracking().Where(a=>a.ShiftAssignmentId==id).Select(a=>(long?)a.UserId).SingleOrDefaultAsync(ct);
        if(user==null)throw new InternalRequestReadException(404,"ASSIGNMENT_UNAVAILABLE");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId,user.Value],ct);await authority.Gate(ticket,sid,branch,department,ct);
        var assignment=await db.ShiftAssignments.FromSqlInterpolated($"SELECT * FROM dbo.shift_assignments WITH(UPDLOCK,HOLDLOCK) WHERE shift_assignment_id={id}").SingleAsync(ct);
        var shift=await db.WorkShifts.FromSqlInterpolated($"SELECT * FROM dbo.work_shifts WITH(UPDLOCK,HOLDLOCK) WHERE shift_id={assignment.ShiftId}").AsNoTracking().SingleAsync(ct);
        if(assignment.UserId!=user||shift.BranchId!=branch||shift.DepartmentId!=department)throw new InternalRequestReadException(404,"ASSIGNMENT_UNAVAILABLE");
        var key=id.ToString(CultureInfo.InvariantCulture);
        var revision=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="shift_assignments"&&a.EntityId==key).Select(a=>(long?)a.AuditId).MaxAsync(ct)??0;
        if(ShiftAssignmentRevision.Version(assignment,shift,revision)!=version)throw new InternalRequestReadException(412,"ASSIGNMENT_CHANGED");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if(assignment.AssignmentStatus!="ASSIGNED"||shift.Status!="PLANNED"||shift.ScheduledStartAt<=now||shift.ScheduledEndAt<=shift.ScheduledStartAt
            ||await db.Workdays.AsNoTracking().AnyAsync(d=>d.ShiftAssignmentId==id,ct)
            ||await db.ShiftAttendances.AsNoTracking().AnyAsync(a=>a.ShiftAssignmentId==id,ct))throw new InternalRequestReadException(409,"FUTURE_UNSTARTED_ASSIGNMENT_REQUIRED");
        var before=JsonSerializer.Serialize(new{assignment.AssignmentStatus,assignment.CancellationReason});
        assignment.AssignmentStatus="CANCELLED";assignment.CancellationReason=reason;
        var receipt=new ShiftCancellationReceipt("SHIFT_CANCEL",id,user.Value,shift.ShiftId,branch,department,reason);
        var audit=new AuditLog{ActorUserId=ticket.UserId,BranchId=branch,EntityType="shift_assignments",EntityId=key,Action="UPDATE",OccurredAt=now,
            OldValues=before,NewValues=JsonSerializer.Serialize(receipt),ChangedFields="[\"assignment_status\",\"cancellation_reason\"]"};
        db.AuditLogs.Add(audit);await db.SaveChangesAsync(ct);
        var notice=new Notification{NotificationType="SHIFT_CANCELLED",SourceType="SHIFT_CANCELLED",SourceId=audit.AuditId,CreatedBy=ticket.UserId,CreatedAt=now,Priority="NORMAL",
            Title="Shift assignment cancelled",Message="A future shift assignment was cancelled. Open the notification to review your assignment."};
        notice.NotificationRecipients.Add(new NotificationRecipient{UserId=user.Value,DeliveryChannel="IN_APP",DeliveryStatus="PENDING"});
        db.Notifications.Add(notice);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();await authority.Gate(ticket,sid,branch,department,ct);
        var finalNow=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if(shift.ScheduledStartAt<=finalNow)throw new InternalRequestReadException(409,"FUTURE_UNSTARTED_ASSIGNMENT_REQUIRED");
        await tx.CommitAsync(ct);
    }
}
