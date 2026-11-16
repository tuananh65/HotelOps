using System.ComponentModel.DataAnnotations;
using System.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed record MaintenanceDepartmentOption(long DepartmentId,string Name);
public sealed record MaintenanceTechnicianOption(long UserId,string Name,int? OpenIncidents,int? OpenOccurrences);
public sealed record MaintenanceAssignmentContext(long IncidentId,string IncidentVersion,long? WorkId,string? WorkVersion,long? AssigneeId,
    IReadOnlyList<MaintenanceTechnicianOption> Technicians,long? DepartmentId,IReadOnlyList<MaintenanceDepartmentOption> Departments);

public sealed partial class MaintenanceAssignmentService
{
    public async Task<MaintenanceAssignmentContext> ContextAsync(SessionReference ticket,long sid,long id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await SessionAsync(ticket,sid,ct);
        var incident=await db.Incidents.AsNoTracking().SingleOrDefaultAsync(i=>i.IncidentId==id,ct)
            ?? throw new InternalRequestReadException(404,"INCIDENT_NOT_FOUND");
        await AuthorizeAsync(ticket.UserId,incident,ct);
        if(await IncidentAssignmentWorkflow.ReadAsync(db,incident,ct) is not ("PENDING_ASSIGNMENT" or "ASSIGNED"))
            throw new InternalRequestReadException(409,"TRIAGED_UNSTARTED_INCIDENT_REQUIRED");
        var cycle=await IncidentReopenStore.ReadAsync(db,id,ct);
        var cycleStart=cycle?.ReopenedAt ?? DateTime.MinValue;
        var work=await db.MaintenanceWorks.AsNoTracking().SingleOrDefaultAsync(w=>w.IncidentId==id && w.CreatedAt>=cycleStart,ct);
        var departments=await db.Departments.AsNoTracking().Where(d=>d.BranchId==incident.BranchId && d.IsActive)
            .OrderBy(d=>d.DepartmentName).ThenBy(d=>d.DepartmentId).Select(d=>new MaintenanceDepartmentOption(d.DepartmentId,d.DepartmentName)).ToArrayAsync(ct);
        var candidates=await db.UserRoles.AsNoTracking().Where(r=>r.BranchId==incident.BranchId
            && (r.Role.RoleCode=="MAINTENANCE_STAFF" || r.Role.RoleCode=="MAINTENANCE_LEAD"))
            .Select(r=>new {r.UserId,r.User.FullName}).Distinct().OrderBy(r=>r.FullName).ThenBy(r=>r.UserId).ToArrayAsync(ct);
        var scope=await MaintenanceReadAccess.ReadAsync(db,ticket.UserId,incident.BranchId,ct);
        var population=scope.Apply(db.MaintenanceWorks.AsNoTracking()).Where(w=>w.Status!="CLOSED" && w.Status!="CANCELLED");
        var occurrenceStorage=await PreventiveMaintenance.PreventiveOccurrenceGeneration.InstalledAsync(db,ct);
        var options=new List<MaintenanceTechnicianOption>();
        foreach(var candidate in candidates)
        {
            try{await MaintenanceAssignmentEligibility.RequireAsync(db,candidate.UserId,incident.BranchId,work?.MaintenanceWorkId ?? 0,ct);}
            catch(ValidationException){continue;}
            var assigned=population.Where(w=>w.AssigneeId==candidate.UserId);
            var visible=scope.Lead || scope.Staff && candidate.UserId==ticket.UserId;
            int? openIncidents=visible?await assigned.CountAsync(w=>w.IncidentId!=null && w.Incident!.Status!="CLOSED" && w.Incident.Status!="CANCELLED",ct):null;
            int? openOccurrences=null;
            if(visible && occurrenceStorage)
            {
                var current=await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.preventive_occurrence_details d
                    JOIN dbo.preventive_maintenance_runs r ON r.run_id=d.run_id
                    JOIN dbo.preventive_maintenance_schedules s ON s.schedule_id=r.schedule_id
                    WHERE d.branch_id={incident.BranchId} AND d.assignee_id={candidate.UserId}
                      AND d.workflow_status IN('ASSIGNED','IN_PROGRESS') AND r.performed_at IS NULL
                      AND r.maintenance_work_id IS NULL
                      AND ((s.asset_id IS NOT NULL AND s.room_id IS NULL AND s.area_id IS NULL
                            AND EXISTS(SELECT 1 FROM dbo.assets t WHERE t.asset_id=s.asset_id AND t.branch_id={incident.BranchId}))
                        OR (s.room_id IS NOT NULL AND s.asset_id IS NULL AND s.area_id IS NULL
                            AND EXISTS(SELECT 1 FROM dbo.rooms t WHERE t.room_id=s.room_id AND t.branch_id={incident.BranchId}))
                        OR (s.area_id IS NOT NULL AND s.asset_id IS NULL AND s.room_id IS NULL
                            AND EXISTS(SELECT 1 FROM dbo.areas t WHERE t.area_id=s.area_id AND t.branch_id={incident.BranchId})))
                    """).SingleAsync(ct);
                var legacy=await assigned.CountAsync(w=>w.IncidentId==null && w.Status!="COMPLETED" && w.PreventiveMaintenanceRuns.Any(r=>r.PerformedAt==null),ct);
                openOccurrences=checked(current+legacy);
            }
            options.Add(new(candidate.UserId,candidate.FullName,openIncidents,openOccurrences));
        }
        var tag=await IncidentTriageStore.TagAsync(db,id,ct) ?? throw new InternalRequestReadException(503,"VERSION_UNAVAILABLE");
        var workTag=work==null?null:await MaintenanceExecutionVersion.TagAsync(db,work.MaintenanceWorkId,ct);
        await SessionAsync(ticket,sid,ct);await AuthorizeAsync(ticket.UserId,incident,ct);
        await tx.CommitAsync(ct);
        return new(id,tag,work?.MaintenanceWorkId,workTag,work?.AssigneeId,options,work?.DepartmentId,departments);
    }

    public async Task<MaintenanceAssignmentReceipt> ReceiptAsync(SessionReference ticket,long sid,long audit,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await SessionAsync(ticket,sid,ct);
        var receipt=await MaintenanceAssignmentStore.ReadAsync(db,audit,ct);
        if(receipt==null || !await MaintenanceAssignmentStore.CanReadReceiptAsync(db,ticket.UserId,receipt,ct))
            throw new InternalRequestReadException(404,"RECEIPT_UNAVAILABLE");
        await SessionAsync(ticket,sid,ct);
        if(!await MaintenanceAssignmentStore.CanReadReceiptAsync(db,ticket.UserId,receipt,ct)) throw new InternalRequestReadException(404,"RECEIPT_UNAVAILABLE");
        await tx.CommitAsync(ct);
        receipt.OccurredAt=DateTime.SpecifyKind(receipt.OccurredAt,DateTimeKind.Utc);
        return receipt;
    }
}
