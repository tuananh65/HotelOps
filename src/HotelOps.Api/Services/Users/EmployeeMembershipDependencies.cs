using HotelOps.Api.Data;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public sealed record EmployeeMembershipDependency(string Kind,long Count);

internal static class EmployeeMembershipDependencies
{
    internal static async Task<IReadOnlyList<EmployeeMembershipDependency>> ReadAsync(HotelOpsDbContext db,
        long user,long branch,long department,DateTime now,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Retained membership transaction required.");
        var result=new List<EmployeeMembershipDependency>();
        await Add("DEPARTMENT_HEAD_TRANSFER_REQUIRED",db.Departments.Where(d=>d.DepartmentId==department&&d.BranchId==branch
            &&d.ManagerUserId==user).Select(d=>d.DepartmentId));
        await Add("WORKDAY",db.Workdays.Where(w=>w.UserId==user&&w.BranchId==branch&&w.DepartmentId==department
            &&(w.Status!="ENDED"||w.EndedAt==null||w.EndedAt<w.StartedAt)).Select(w=>w.WorkdayId));
        await Add("OPEN_INTERVAL",db.WorkIntervals.Where(i=>i.UserId==user&&i.BranchId==branch&&i.ClosedAt==null
            &&(!db.Workdays.Any(w=>w.WorkdayId==i.WorkdayId)||db.Workdays.Any(w=>w.WorkdayId==i.WorkdayId&&w.DepartmentId==department))).Select(i=>i.IntervalId));
        await Add("SCHEDULED_ASSIGNMENT",db.ShiftAssignments.Where(a=>a.UserId==user&&a.Shift.BranchId==branch
            &&a.Shift.DepartmentId==department&&a.AssignmentStatus!="CANCELLED"
            &&(a.Shift.ScheduledEndAt>now||a.Shift.ScheduledEndAt<=a.Shift.ScheduledStartAt)).Select(a=>a.ShiftAssignmentId));
        await Add("TASK_ASSIGNMENT",db.OperationalTaskAssignments.Where(a=>a.AssigneeId==user&&a.Task.BranchId==branch
            &&a.Task.DepartmentId==department&&(a.AssignmentStatus=="ACTIVE"||a.EndedAt==null)).Select(a=>a.AssignmentId));
        // Future occurrences retain this executor. Resolve the series through its
        // stop workflow before removing the membership needed by generation.
        if(!await TaskSeriesStore.InstalledAsync(db,ct))result.Add(new("TASK_SERIES_STORAGE_REQUIRED",1));
        else await Add("ACTIVE_TASK_SERIES",TaskSeriesStore.Query(db).Where(s=>s.DefaultAssigneeId==user
            &&s.BranchId==branch&&s.DepartmentId==department&&s.IsActive).Select(s=>s.SeriesId));
        await Add("REQUEST_ASSIGNMENT",db.InternalRequestAssignments.Where(a=>a.AssigneeId==user&&a.InternalRequest.BranchId==branch
            &&(a.InternalRequest.ReceivingDepartmentId==department||a.InternalRequest.ReceivingDepartmentId==null)
            &&(a.Status=="ACTIVE"||a.EndedAt==null)).Select(a=>a.AssignmentId));
        await Add("MAINTENANCE_ASSIGNMENT",db.MaintenanceWorks.Where(w=>w.AssigneeId==user
            &&(w.DepartmentId==department||w.DepartmentId==null&&w.Incident!=null&&w.Incident.BranchId==branch)
            &&w.Status!="CANCELLED"&&(w.Status!="CLOSED"||w.ReviewedAt==null)).Select(w=>w.MaintenanceWorkId));
        // Cleaning/inspection assignments have a room branch but no department key.
        // Keep an explicit unresolved scope dependency instead of guessing from the user's primary department.
        await Add("CLEANING_DEPARTMENT_SCOPE_UNRESOLVED",db.CleaningAssignments.Where(a=>a.AssigneeUserId==user
            &&a.CleaningTask.Room.BranchId==branch&&(a.AssignmentStatus=="ACTIVE"||a.EndedAt==null)).Select(a=>a.AssignmentId));
        await Add("REWORK_DEPARTMENT_SCOPE_UNRESOLVED",db.CleaningReworks.Where(r=>r.AssigneeUserId==user
            &&r.CleaningTask.Room.BranchId==branch&&r.Status!="CANCELLED"&&(r.Status!="COMPLETED"||r.CompletedAt==null)).Select(r=>r.ReworkId));
        await Add("INSPECTION_DEPARTMENT_SCOPE_UNRESOLVED",db.RoomInspections.Where(i=>i.InspectorUserId==user
            &&i.Room.BranchId==branch&&i.Status!="CANCELLED"&&(i.Status!="COMPLETED"||i.Result==null)).Select(i=>i.InspectionId));
        if(!await InternalRequestHandoverStore.InstalledAsync(db,ct))result.Add(new("HANDOVER_STORAGE_REQUIRED",1));
        else await Add("PENDING_HANDOVER",InternalRequestHandoverStore.Rows(db).Where(h=>h.BranchId==branch&&h.DepartmentId==department
            &&h.State=="PENDING_ACKNOWLEDGMENT"&&(h.ReceiverId==user||db.InternalRequestAssignments.Any(a=>a.AssignmentId==h.SenderAssignmentId&&a.AssigneeId==user))).Select(h=>h.Id));
        if(!await HotelOps.Api.Services.PreventiveMaintenance.PreventiveOccurrenceGeneration.InstalledAsync(db,ct))
            result.Add(new("PREVENTIVE_STORAGE_REQUIRED",1));
        else
        {
            var count=await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.preventive_occurrence_details d WITH(HOLDLOCK)
                LEFT JOIN dbo.preventive_maintenance_runs r WITH(HOLDLOCK) ON r.run_id=d.run_id
                WHERE d.assignee_id={user} AND d.branch_id={branch} AND
                  (d.workflow_status NOT IN('COMPLETED','CANCELLED') OR r.run_id IS NULL OR r.maintenance_work_id IS NOT NULL
                    OR (d.workflow_status='COMPLETED' AND (r.performed_at IS NULL OR r.performed_by IS NULL OR r.performed_by<>{user}))
                    OR (d.workflow_status='CANCELLED' AND (r.performed_at IS NOT NULL OR r.performed_by IS NOT NULL))
                    OR EXISTS(SELECT 1 FROM dbo.work_intervals i WHERE i.work_item_kind='OCCURRENCE' AND i.work_item_id=d.run_id AND i.closed_at IS NULL))
                """).SingleAsync(ct);
            if(count>0)result.Add(new("PREVENTIVE_DEPARTMENT_SCOPE_UNRESOLVED",count));
        }
        if(!await HotelOps.Api.Services.WorkTime.ShiftHandoverStore.InstalledAsync(db,ct))result.Add(new("SHIFT_HANDOVER_STORAGE_REQUIRED",1));
        else await Add("PENDING_SHIFT_HANDOVER",HotelOps.Api.Services.WorkTime.ShiftHandoverStore.Rows(db)
            .Where(h=>h.BranchId==branch&&h.DepartmentId==department&&h.State=="PENDING_ACKNOWLEDGMENT"&&(h.SenderId==user||h.ReceiverId==user)).Select(h=>h.Id));
        return result;

        async Task Add(string kind,IQueryable<long> query)
        {var count=await query.LongCountAsync(ct);if(count>0)result.Add(new(kind,count));}
    }
}
