using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed record MaintenanceAssignmentInput(long AssigneeId, string? WorkVersion, string? Instruction, DateTime PlannedEndAt, long DepartmentId);
public sealed record MaintenanceAssignmentSaved(long WorkId, long AuditId, string Workflow);

public sealed partial class MaintenanceAssignmentService(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid || actor.IsRestricted || ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var result=await sessions.ValidateSessionAsync(ticket,ct);
        if(!result.IsValid || result.Session?.SessionId!=sid || result.Session.IsRestricted) throw new InternalRequestReadException(401,"SESSION_INVALID");
    }

    internal async Task AuthorizeAsync(long user, Incident incident, CancellationToken ct)
    {
        if (!await MaintenanceAssignmentAccess.AllowsAsync(db,user,incident.BranchId,incident.IncidentId,ct))
            throw new InternalRequestReadException(403,"SCOPED_MAINTENANCE_ASSIGN_REQUIRED");
        if (!await MaintenanceAssignmentStore.InstalledAsync(db,ct) || !await MaintenanceExecutionVersion.InstalledAsync(db,ct))
            throw new InternalRequestReadException(503,"ASSIGNMENT_STORAGE_UNAVAILABLE");
    }

    public async Task<MaintenanceAssignmentSaved> AssignAsync(SessionReference ticket,long sid,long id,string tag,MaintenanceAssignmentInput input,CancellationToken ct)
    {
        if(input.DepartmentId<=0) throw new ValidationException("Choose an active department in the incident branch.");
        if(input.AssigneeId<=0) throw new ValidationException("Choose an eligible technician.");
        MaintenanceAssignmentEligibility.RequirePlannedEnd(null,input.PlannedEndAt);
        if(db.Database.CurrentTransaction!=null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean assignment context required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var locked=await new UserEligibilityLock(db).LockAsync([ticket.UserId,input.AssigneeId],ct);
        UserEligibilityLock.RequireEligible(input.AssigneeId,locked);
        await SessionAsync(ticket,sid,ct);
        var incident=await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InternalRequestReadException(404,"INCIDENT_NOT_FOUND");
        await AuthorizeAsync(ticket.UserId,incident,ct);
        if(!await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==input.DepartmentId && d.BranchId==incident.BranchId && d.IsActive,ct))
            throw new ValidationException("Department must be active and belong to the incident branch.");
        var version=await IncidentTriageStore.TagAsync(db,id,ct) ?? throw new InternalRequestReadException(503,"INCIDENT_VERSION_UNAVAILABLE");
        if(version!=tag) throw new InternalRequestReadException(412,"INCIDENT_CHANGED");
        if(await IncidentAssignmentWorkflow.ReadAsync(db,incident,ct) is not ("ASSIGNED" or "PENDING_ASSIGNMENT"))
            throw new InternalRequestReadException(409,"TRIAGED_UNSTARTED_INCIDENT_REQUIRED");
        var cycle=await IncidentReopenStore.ReadAsync(db,id,ct);
        var cycleStart=cycle?.ReopenedAt ?? DateTime.MinValue;
        var works=await db.MaintenanceWorks.FromSqlInterpolated($"SELECT * FROM dbo.maintenance_works WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={id}")
            .Where(w=>w.CreatedAt>=cycleStart).AsTracking().ToArrayAsync(ct);
        if(works.Length>1) throw new InternalRequestReadException(409,"AMBIGUOUS_MAINTENANCE_WORK");
        var work=works.SingleOrDefault();
        if(work==null && input.WorkVersion!=null || work!=null && input.WorkVersion!=await MaintenanceExecutionVersion.TagAsync(db,work.MaintenanceWorkId,ct))
            throw new InternalRequestReadException(412,"WORK_CHANGED");
        if(work?.AssigneeId==input.AssigneeId) throw new ValidationException("Technician is already assigned.");
        MaintenanceAssignmentEligibility.RequirePlannedEnd(work?.PlannedStartAt,input.PlannedEndAt);
        await MaintenanceAssignmentEligibility.RequireAsync(db,input.AssigneeId,incident.BranchId,work?.MaintenanceWorkId ?? 0,ct);
        var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
        var previous=work?.AssigneeId;
        var old=work==null ? null : JsonSerializer.Serialize(new { department_id=work.DepartmentId,assignee_id=previous,instruction=work.Instruction,planned_end_at=work.PlannedEndAt,status=work.Status });
        var created=work==null;
        if(work==null)
        {
            var prefix="MW-"+now.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"-";
            var resource="HotelOps:MaintenanceWorkNo:"+prefix;
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51848,'Number allocation unavailable.',1;",ct);
            var maximum=await db.Database.SqlQuery<long>($"SELECT COALESCE(MAX(TRY_CONVERT(bigint,SUBSTRING(work_no,13,40))),CAST(0 AS bigint)) AS Value FROM dbo.maintenance_works WITH(UPDLOCK,HOLDLOCK) WHERE work_no LIKE {prefix+"%"}").SingleAsync(ct);
            work=new MaintenanceWork { WorkNo=prefix+checked(maximum+1).ToString("D3",CultureInfo.InvariantCulture),IncidentId=id,Title=incident.IncidentTitle,
                PriorityId=incident.PriorityId,Status="PENDING",CreatedBy=ticket.UserId,CreatedAt=now };
            db.MaintenanceWorks.Add(work);
        }
        work.DepartmentId=input.DepartmentId;work.AssigneeId=input.AssigneeId;work.Instruction=input.Instruction?.Trim();work.PlannedEndAt=input.PlannedEndAt;
        await db.SaveChangesAsync(ct);
        var audit=new AuditLog { EntityType="maintenance_works",EntityId=work.MaintenanceWorkId.ToString(CultureInfo.InvariantCulture),BranchId=incident.BranchId,
            ActorUserId=ticket.UserId,Action=created?"CREATE":"UPDATE",OccurredAt=now,OldValues=old,
            NewValues=JsonSerializer.Serialize(new { work_id=work.MaintenanceWorkId,incident_id=id,previous_assignee_id=previous,assignee_id=input.AssigneeId,department_id=work.DepartmentId,
                instruction=work.Instruction,planned_end_at=work.PlannedEndAt,status="PENDING",notification_kind="MAINTENANCE_ASSIGNED" }) };
        db.AuditLogs.Add(audit);await db.SaveChangesAsync(ct);
        await MaintenanceAssignmentStore.WriteAsync(db,new() { AuditId=audit.AuditId,WorkId=work.MaintenanceWorkId,IncidentId=id,BranchId=incident.BranchId,
            ActorId=ticket.UserId,PreviousAssigneeId=previous,AssigneeId=input.AssigneeId,OccurredAt=now },tag,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.incidents SET status=status WHERE incident_id={id}",ct);
        await SessionAsync(ticket,sid,ct);await AuthorizeAsync(ticket.UserId,incident,ct);
        await MaintenanceAssignmentEligibility.RequireAsync(db,input.AssigneeId,incident.BranchId,work.MaintenanceWorkId,ct);
        if(await MaintenanceAssignmentProof.ReadAsync(db,work.MaintenanceWorkId,ct)!="ASSIGNED") throw new InternalRequestReadException(409,"ASSIGNMENT_PROOF_CHANGED");
        await tx.CommitAsync(ct);
        return new(work.MaintenanceWorkId,audit.AuditId,"ASSIGNED");
    }
}
