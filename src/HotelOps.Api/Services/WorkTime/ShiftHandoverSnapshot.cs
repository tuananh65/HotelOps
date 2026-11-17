using HotelOps.Api.Data;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed record ShiftHandoverItem(string Kind,long Id,string State,string Label,string? Note=null);
internal static class ShiftHandoverSnapshot
{
    internal static async Task<ShiftHandoverItem[]> CaptureAsync(HotelOpsDbContext db,long user,long branch,long department,DateTime now,CancellationToken ct)
    {
        var items=new List<ShiftHandoverItem>();
        var taskStates=TaskWorkflowRead.Query(db,now);
        var activeTasks=db.OperationalTaskAssignments.AsNoTracking().Where(a=>a.AssigneeId==user&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null
            &&a.Task.BranchId==branch&&a.Task.DepartmentId==department);
        if(await activeTasks.AnyAsync(a=>a.AssignedAt>now||!taskStates.Any(t=>t.TaskId==a.TaskId&&t.State!="CLOSED"&&t.State!="CANCELLED"),ct))
            throw new InternalRequestReadException(409,"HANDOVER_TASK_STATE_UNPROVEN");
        items.AddRange(await (from a in db.OperationalTaskAssignments.AsNoTracking()
            join s in taskStates on a.TaskId equals s.TaskId
            where a.AssigneeId==user&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now
              &&a.Task.BranchId==branch&&a.Task.DepartmentId==department&&s.State!="CLOSED"&&s.State!="CANCELLED"
            select new ShiftHandoverItem("TASK",a.TaskId,s.State,a.Task.Title,null)).ToArrayAsync(ct));
        var housekeeping=await db.Database.SqlQuery<HousekeepingItem>($"""
            SELECT 'CLEANING' AS Kind,t.cleaning_task_id AS Id,t.workflow_status AS State,r.room_number AS Label
            FROM dbo.cleaning_tasks t JOIN dbo.rooms r ON r.room_id=t.room_id
            WHERE r.branch_id={branch} AND t.workflow_status NOT IN('COMPLETED','CANCELLED')
              AND EXISTS(SELECT 1 FROM dbo.cleaning_assignments a WHERE a.cleaning_task_id=t.cleaning_task_id
                AND a.assignee_user_id={user} AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL AND a.assigned_at<={now})
            UNION ALL
            SELECT 'REWORK',w.rework_id,w.workflow_status,r.room_number
            FROM dbo.cleaning_reworks w JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=w.cleaning_task_id
              JOIN dbo.rooms r ON r.room_id=t.room_id
            WHERE w.assignee_user_id={user} AND r.branch_id={branch} AND w.workflow_status NOT IN('COMPLETED','CANCELLED')
            """).ToArrayAsync(ct);
        items.AddRange(housekeeping.Select(x=>new ShiftHandoverItem(x.Kind,x.Id,x.State,x.Label)));
        var incidents=IncidentCurrentAssignment.AssignedTo(db.Incidents.AsNoTracking().Where(i=>i.BranchId==branch),user,false);
        items.AddRange(await incidents.Where(i=>i.Status!="CLOSED"&&i.Status!="CANCELLED")
            .Select(i=>new ShiftHandoverItem("INCIDENT",i.IncidentId,i.Status,i.IncidentTitle,null)).ToArrayAsync(ct));
        items.AddRange(await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.AssigneeId==user&&a.Status=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now
            &&a.InternalRequest.BranchId==branch&&a.InternalRequest.ReceivingDepartmentId==department&&a.InternalRequest.Status=="IN_PROGRESS")
            .Select(a=>new ShiftHandoverItem("INTERNAL_REQUEST",a.InternalRequestId,a.InternalRequest.Status,a.InternalRequest.Subject,null)).ToArrayAsync(ct));
        if(await ShiftHandoverAccess.RoleAsync(db,user,branch,now,ct,["HK_SUPERVISOR","MAINTENANCE_LEAD","DEPT_MANAGER"])
            &&await ShiftHandoverAccess.GrantAsync(db,user,branch,"ROOM_VIEW",false,now,ct))
        {
            var rooms=await db.Rooms.AsNoTracking().Where(r=>r.BranchId==branch&&r.IsActive).Select(r=>new{r.RoomId,r.RoomNumber}).ToArrayAsync(ct);
            var readiness=await new EffectiveRoomReadiness(db).ReadAsync(rooms.Select(r=>r.RoomId).ToArray(),now,ct);
            foreach(var room in rooms)
                if(readiness.TryGetValue(room.RoomId,out var state)&&!state.CanUse)
                    items.Add(new("ROOM",room.RoomId,state.Readiness,room.RoomNumber));
        }
        // Never let the feature permission stand in for access to a source record.
        foreach(var item in items)
            if(!await CanReadSourceAsync(db,user,branch,item,now,ct))
                throw new InternalRequestReadException(403,"HANDOVER_SOURCE_READ_REQUIRED");
        return items.DistinctBy(i=>(i.Kind,i.Id)).OrderBy(i=>i.Kind,StringComparer.Ordinal).ThenBy(i=>i.Id).ToArray();
    }
    internal static async Task<bool> CanReadSourceAsync(HotelOpsDbContext db,long user,long branch,ShiftHandoverItem item,DateTime now,CancellationToken ct)
    {
        if(item.Kind=="INCIDENT")
            return await (await IncidentReadAccess.ReadAsync(db,user,branch,ct,true)).Apply(db.Incidents.AsNoTracking())
                .AnyAsync(i=>i.IncidentId==item.Id,ct);
        if(item.Kind=="INTERNAL_REQUEST")
            return await (await new InternalRequestReadAccess(db).ReadAsync(user,now,ct)).Apply(db.InternalRequests.AsNoTracking(),now)
                .AnyAsync(r=>r.InternalRequestId==item.Id&&r.BranchId==branch,ct);
        var inBranch=item.Kind switch
        {
            "TASK"=>await db.OperationalTasks.AsNoTracking().AnyAsync(t=>t.TaskId==item.Id&&t.BranchId==branch,ct),
            "CLEANING"=>await db.CleaningTasks.AsNoTracking().AnyAsync(t=>t.CleaningTaskId==item.Id&&t.Room.BranchId==branch,ct),
            "REWORK"=>await db.CleaningReworks.AsNoTracking().AnyAsync(r=>r.ReworkId==item.Id&&r.CleaningTask.Room.BranchId==branch,ct),
            "ROOM"=>await db.Rooms.AsNoTracking().AnyAsync(r=>r.RoomId==item.Id&&r.BranchId==branch,ct),
            _=>false
        };
        if(!inBranch)return false;
        var own=item.Kind switch
        {
            "TASK"=>await db.OperationalTaskAssignments.AsNoTracking().AnyAsync(a=>a.TaskId==item.Id&&a.Task.BranchId==branch&&a.AssigneeId==user&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now,ct),
            "CLEANING"=>await db.CleaningAssignments.AsNoTracking().AnyAsync(a=>a.CleaningTaskId==item.Id&&a.CleaningTask.Room.BranchId==branch&&a.AssigneeUserId==user&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now,ct),
            "REWORK"=>await db.CleaningReworks.AsNoTracking().AnyAsync(r=>r.ReworkId==item.Id&&r.CleaningTask.Room.BranchId==branch&&r.AssigneeUserId==user,ct),
            _=>false
        };
        var code=item.Kind switch {"TASK"=>"TASK_VIEW","CLEANING" or "REWORK"=>"CLEANING_VIEW","ROOM"=>"ROOM_VIEW",_=>null};
        return code!=null&&await ShiftHandoverAccess.GrantAsync(db,user,branch,code,own,now,ct);
    }
    private sealed class HousekeepingItem
    { public string Kind{get;set;}=""; public long Id{get;set;} public string State{get;set;}=""; public string Label{get;set;}=""; }
}
